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

                    return false;
                }

                Guid sourceTreeId = Guid.NewGuid();


                return true;
            });

            var bd = new BulkDescriptor();

            SourceStore.AddCreateOperation(bd, sourceModel);
        }

        private SourceSearchModel CreateSourceModel(string repoName, BoundSourceFile boundSourceFile)
        {
            throw new NotImplementedException();
        }
    }

    public class ElasticSearchBatch
    {
        public BulkDescriptor BulkDescriptor = new BulkDescriptor();

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
