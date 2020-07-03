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
using static Codex.ObjectModel.Mappings;
using System.IO;

namespace Codex.Lucene.Search
{
    public class LuceneCodex : CodexBase<LuceneClient, LuceneConfiguration>
    {
        public LuceneCodex(LuceneConfiguration configuration)
            : base(configuration)
        {
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

            public async Task<IIndexSearchResponse<TResult>> QueryAsync<TResult>(IStoredFilterInfo storedFilterInfo, Func<CodexQueryBuilder<T>, CodexQuery<T>> filter, OneOrMany<Mapping<T>> sort = null, int? take = null, Func<CodexQueryBuilder<T>, CodexQuery<T>> boost = null) where TResult : T
            {
                await Task.Yield();

                var query = filter(queryBuilder);
                var luceneQuery = FromCodexQuery(query);

                var topDocs = Searcher.Search(luceneQuery, take ?? 1000);

                throw new NotImplementedException();
            }

            public Query FromCodexQuery(CodexQuery<T> query)
            {
                switch (query.Kind)
                {
                    case CodexQueryKind.And:
                    case CodexQueryKind.Or:
                        {
                            var bq = new BooleanQuery();
                            var binaryQuery = (BinaryCodexQuery<T>)query;
                            bq.Add(FromCodexQuery(binaryQuery.LeftQuery), query.Kind == CodexQueryKind.And ? Occur.MUST : Occur.SHOULD);
                            bq.Add(FromCodexQuery(binaryQuery.RightQuery), query.Kind == CodexQueryKind.And ? Occur.MUST : Occur.SHOULD);
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
                            return bq;
                        }
                    case CodexQueryKind.MatchPhrase:
                        //var mq = (MatchPhraseCodexQuery<T>)query;
                        //var result = new MultiPhraseQuery();
                        //return result;
                    default:
                        throw new NotImplementedException();
                }
            }
        }
    }
}
