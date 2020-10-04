using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Storage.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.Search;
using Codex.Search;
using Codex.Serialization;
using static Codex.ObjectModel.Mappings;
using System.IO;
using Lucene.Net.Util;
using Lucene.Net.QueryParsers.Simple;
using Lucene.Net.Analysis.Standard;
using Codex.Lucene.Framework;

namespace Codex.Lucene.Search
{
    public class LuceneCodex : CodexBase<LuceneClient, LuceneConfiguration>
    {
        public LuceneCodex(LuceneConfiguration configuration)
            : base(configuration)
        {
            FieldMappingCodec.EnsureRegistered();

            //Reader = DirectoryReader.Open(FSDirectory.Open(configuration.Directory));
            //Searcher = new IndexSearcher(Reader);
            Client = new LuceneClient(this);
        }

        public LuceneClient Client { get; }

        protected override async Task<StoredFilterSearchContext<LuceneClient>> GetStoredFilterContextAsync(ContextCodexArgumentsBase arguments)
        {
            return new StoredFilterSearchContext<LuceneClient>(Client, "", "", "");
        }
    }

    public class LuceneClient : ClientBase
    {
        private Mappings mappings;
        private LuceneCodex codex;

        public LuceneClient(LuceneCodex codex)
        {
            mappings = new Mappings();
            this.codex = codex;
        }

        public override IIndex<T> CreateIndex<T>(SearchType<T> searchType)
        {
            return new LuceneIndex<T>(this, searchType);
        }

        public class LuceneIndex<T> : IIndex<T>
            where T : class, ISearchEntity
        {
            private readonly LuceneClient client;
            private readonly SearchType<T> searchType;
            private readonly SearchEntityMapping<T> mapping;
            private readonly IndexReader Reader;
            private readonly IndexSearcher Searcher;
            private readonly CodexQueryBuilder<T> queryBuilder = new CodexQueryBuilder<T>();

            public LuceneIndex(LuceneClient client, SearchType<T> searchType)
            {
                this.client = client;
                this.searchType = searchType;
                mapping = (SearchEntityMapping<T>)client.mappings[searchType];

                Reader = DirectoryReader.Open(client.codex.Configuration.OpenIndexDirectory(searchType));
                Searcher = new IndexSearcher(Reader);
            }

            public async Task<IReadOnlyList<T>> GetAsync(IStoredFilterInfo storedFilterInfo, params string[] ids)
            {
                var results = await QueryAsync<T>(storedFilterInfo, cqb => cqb.Terms(mapping.EntityContentId, ids));
                return results.Hits.SelectArray(h => h.Source);
            }

            private static readonly TopDocs EmptyTopDocs = new TopDocs(0, Array.Empty<ScoreDoc>(), 0);

            public async Task<IIndexSearchResponse<TResult>> QueryAsync<TResult>(IStoredFilterInfo storedFilterInfo, Func<CodexQueryBuilder<T>, CodexQuery<T>> filter, OneOrMany<Mapping<T>> sort = null, int? take = null, Func<CodexQueryBuilder<T>, CodexQuery<T>> boost = null) where TResult : T
            {
                var query = filter(queryBuilder);
                var luceneQuery = FromCodexQuery(query);

                await Task.Yield();

                //if (searchType == SearchTypes.Definition)
                //{
                //    var terms = Reader.Leaves[0].AtomicReader.GetTerms(client.mappings.Definition.Definition.ShortName.MappingInfo.FullName);
                //    var te = terms?.GetIterator(null);
                //    while (true)
                //    {
                //        var term = te?.Next();
                //        if (term == null) break;
                //    }
                //    bool found = te.SeekExact(new BytesRef("xedocbase"));
                //}

                var topDocs = luceneQuery == null ? EmptyTopDocs : Searcher.Search(luceneQuery, take ?? 1000);

                var results = topDocs.ScoreDocs
                    .Select(sd => Reader.Document(sd.Doc).GetField(LuceneConstants.SourceFieldName))
                    .Select(f => new StringReader(f.GetStringValue()).DeserializeEntity<TResult>())
                    .Select(r => new SearchHit<TResult>()
                    {
                        Source = r
                    });

                return new IndexSearchResponse<TResult>()
                {
                    Hits = results.ToList(),
                    Total = topDocs.TotalHits
                };
            }

            public Query FromCodexQuery(CodexQuery<T> query)
            {
                if (query == null) return null;

                const bool flattenQueries = true;
                switch (query.Kind)
                {
                    case CodexQueryKind.And:
                    case CodexQueryKind.Or:
                        {
                            var bq = new BooleanQuery();
                            var binaryQuery = (BinaryCodexQuery<T>)query;

                            addClauses(binaryQuery.LeftQuery);
                            addClauses(binaryQuery.RightQuery);

                            void addClauses(CodexQuery<T> q)
                            {
                                Query lq = FromCodexQuery(q);
                                if (lq == null) return;

                                if (flattenQueries
                                    && (q.Kind == query.Kind || (q.Kind == CodexQueryKind.Negate && query.Kind == CodexQueryKind.And)) 
                                    && lq is BooleanQuery boolQuery)
                                {
                                    foreach (var clause in boolQuery.Clauses)
                                    {
                                        // Only include MUST_NOT clauses from Negate queries
                                        if (q.Kind != CodexQueryKind.Negate || clause.Occur == Occur.MUST_NOT)
                                        {
                                            bq.Add(clause);
                                        }
                                    }
                                }
                                else
                                {
                                    bq.Add(lq, query.Kind == CodexQueryKind.And ? Occur.MUST : Occur.SHOULD);
                                }
                            }
                            return bq;
                        }
                    case CodexQueryKind.Term:
                        var tq = (ITermQuery)query;
                        return tq.CreateQuery(QueryFactory.Instance);
                    case CodexQueryKind.Negate:
                        {
                            var nq = (NegateCodexQuery<T>)query;
                            var bq = new BooleanQuery();
                            bq.Add(FromCodexQuery(nq.InnerQuery), Occur.MUST_NOT);
                            bq.Add(new MatchAllDocsQuery(), Occur.MUST);
                            return bq;
                        }
                    case CodexQueryKind.MatchPhrase:
                        var mq = (MatchPhraseCodexQuery<T>)query;

                        var fieldName = mq.Mapping.MappingInfo.FullName;
                        var simpleQueryParser = new SimpleQueryParser(
                            new StandardAnalyzer(LuceneVersion.LUCENE_48),
                            fieldName);

                        string phrase = mq.Phrase;
                        if (mq.MaxExpansions > 0)
                        {
                            phrase += "*";
                        }

                        var parsedQuery = simpleQueryParser.CreatePhraseQuery(fieldName, phrase);
                        return parsedQuery;
                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }
}
