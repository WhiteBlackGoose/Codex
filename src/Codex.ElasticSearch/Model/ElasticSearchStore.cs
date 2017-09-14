using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Nest;

namespace Codex.ElasticSearch
{
    public partial class ElasticSearchStore
    {
        internal readonly ElasticSearchService Service;
        internal readonly ElasticSearchStoreConfiguration Configuration;

        /// <summary>
        /// Creates an elasticsearch store with the given prefix for indices
        /// </summary>
        public ElasticSearchStore(ElasticSearchStoreConfiguration configuration, ElasticSearchService service)
        {
            Configuration = configuration;
            Service = service;
        }

        //public override Task FinalizeAsync()
        //{
        //    // TODO: Delta commits.
        //    // TODO: Finalize commits. Should there be a notion of sessions for commits
        //    // rather than having the entire store be commit specific
        //    return base.FinalizeAsync();
        //}

        //public override Task InitializeAsync()
        //{
        //    return base.InitializeAsync();
        //}

        public async Task<ElasticSearchEntityStore<TSearchType>> CreateStoreAsync<TSearchType>(SearchType searchType)
            where TSearchType : class
        {
            var store = new ElasticSearchEntityStore<TSearchType>(this, searchType);
            await store.InitializeAsync();
            return store;
        }

        public async Task<Guid?> TryGetSourceHashTreeId(SourceSearchModel sourceModel)
        {
            throw new NotImplementedException();
        }

        public async Task AddBoundSourceFileAsync(string repoName, BoundSourceFile boundSourceFile)
        {
            var sourceModel = CreateSourceModel(repoName, boundSourceFile);
            await Service.UseClient(async context =>
            {
                var existingSourceTreeId = await TryGetSourceHashTreeId(sourceModel);
                if (existingSourceTreeId != null)
                {
                    // TODO: Add documents to stored filter based on hash tree
                    // which should have child nodes references the filters to add
                    return false;
                }

                Guid sourceTreeId = Guid.NewGuid();


                return true;
            });


            var batch = new ElasticSearchBatch();

            batch.Add(SourceStore, sourceModel);

            foreach (var property in sourceModel.File.SourceFile.Info.Properties)
            {
                batch.Add(PropertyStore, new PropertySearchModel()
                {
                    Uid = Placeholder.Value<string>("Populate fields"),
                    Key = property.Key,
                    Value = property.Value,
                    OwnerId = sourceModel.Uid,
                });
            }

            foreach (var definitionSpan in sourceModel.File.Definitions.Where(ds => !ds.Definition.ExcludeFromSearch))
            {
                batch.Add(DefinitionStore, new DefinitionSearchModel()
                {
                    Uid = Placeholder.Value<string>("Populate fields"),
                    Definition = definitionSpan.Definition,
                });
            }

            foreach (var referenceSpan in sourceModel.File.References.Where(rs => !rs.Reference.ExcludeFromSearch))
            {
                Placeholder.NotImplemented($"Group by symbol as in {nameof(Storage.DataModel.SourceFileModel.GetSearchReferences)}");
                batch.Add(ReferenceStore, new ReferenceSearchModel()
                {
                    Uid = Placeholder.Value<string>("Populate fields"),
                    Reference = referenceSpan.Reference
                });
            }
        }

        private SourceSearchModel CreateSourceModel(string repoName, BoundSourceFile boundSourceFile)
        {
            throw new NotImplementedException();
        }
    }

    public class ElasticSearchBatch
    {
        public BulkDescriptor BulkDescriptor = new BulkDescriptor();

        public void Add<T>(ElasticSearchEntityStore<T> store, T entity)
            where T : class, ISearchEntity
        {
            PopulateContentIdAndSize(entity, store);
        }

        public void PopulateContentIdAndSize<T>(T entity, ElasticSearchEntityStore<T> store)
            where T : class, ISearchEntity
        {
            Placeholder.NotImplemented("Get content id, size, and store content id as Uid where appropriate");
        }

        public async Task<IBulkResponse> ExecuteAsync(ClientContext context)
        {
            var response = await context.Client.BulkAsync(BulkDescriptor);
            throw new NotImplementedException();
        }
    }

    public class ElasticSearchStoreConfiguration
    {
        /// <summary>
        /// Prefix for indices
        /// </summary>
        public string Prefix;

        /// <summary>
        /// Indicates where indices should be created when <see cref="ElasticSearchStore.InitializeAsync"/> is called.
        /// </summary>
        public bool CreateIndices = true;

        /// <summary>
        /// The number of shards for created indices
        /// </summary>
        public int? ShardCount;
    }
}
