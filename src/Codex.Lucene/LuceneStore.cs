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
using Lucene.Net.Documents;
using Codex.ElasticSearch;
using Codex.Logging;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using static Lucene.Net.Documents.Field;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;

namespace Codex.Lucene.Search
{
    public class LuceneCodexStore : ICodexStore
    {
        public LuceneCodexStore(LuceneConfiguration configuration)
        {
            Configuration = configuration;
        }

        public LuceneConfiguration Configuration { get; }

        public Task<ICodexRepositoryStore> CreateRepositoryStore(Repository repository, Commit commit, Branch branch)
        {
            return Task.FromResult<ICodexRepositoryStore>(new RepositoryStore(new Batcher(this), repository, commit, branch));
        }

        private class RepositoryStore : IndexingCodexRepositoryStoreBase<LuceneStoreFilterBuilder>
        {
            public RepositoryStore(Batcher batcher, Repository repository, Commit commit, Branch branch) 
                : base(batcher, batcher.Store.Configuration.Logger, repository, commit, branch)
            {
            }

            protected override void PopulateTextChunk(TextChunkSearchModel chunk)
            {
                chunk.PopulateContentIdAndSize();
            }
        }

        private interface ILuceneWriter<T>
            where T : class, ISearchEntity
        {
            void Write(SearchType<T> searchType, T value);
        }

        private class Batcher : IBatcher<LuceneStoreFilterBuilder>
        {
            private LazySearchTypesMap<IndexWriter> Writers { get; }
            public LuceneCodexStore Store { get; }

            public Mappings mappings = new Mappings();

            public LuceneStoreFilterBuilder[] DeclaredDefinitionStoredFilter { get; } = new[] { new LuceneStoreFilterBuilder() };

            public Batcher(LuceneCodexStore store)
            {
                Store = store;

                Writers = new LazySearchTypesMap<IndexWriter>(
                    s =>
                    new IndexWriter(
                        Store.Configuration.OpenIndexDirectory(s),
                        new IndexWriterConfig(LuceneVersion.LUCENE_48, new StandardAnalyzer(LuceneVersion.LUCENE_48))),
                    initializeAll: true);
            }

            public Task FinalizeAsync(string repositoryName)
            {
                return Task.Run(() => Writers.ForEach(w => w.Dispose()));
            }

            public void Add<T>(SearchType<T> searchType, T entity, params LuceneStoreFilterBuilder[] additionalStoredFilters)
                where T : class, ISearchEntity
            {
                entity.PopulateContentIdAndSize();

                Document doc = new Document()
                {
                    new StoredField(LuceneConstants.SourceFieldName, entity.SerializeEntity(ObjectStage.Index)),
                };

                var visitor = new DocumentVisitor(doc);

                var mapping = (IMapping<T>)mappings[searchType];

                mapping.Visit(visitor, entity);

                Writers[searchType].AddDocument(doc);
            }

            public ValueTask<None> AddAsync<T>(SearchType<T> searchType, T entity, params LuceneStoreFilterBuilder[] additionalStoredFilters)
                where T : class, ISearchEntity
            {
                Add(searchType, entity, additionalStoredFilters);
                return new ValueTask<None>(None.Value);
            }
        }

        private class LuceneStoreFilterBuilder
        {
        }
    }
}
