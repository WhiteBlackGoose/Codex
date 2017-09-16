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
using Codex.Storage.DataModel;
using static Codex.Utilities.SerializationUtilities;
using Codex.Utilities;

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

        public async Task<Guid?> TryGetSourceHashTreeId(BoundSourceSearchModel sourceModel)
        {
            throw new NotImplementedException();
        }

        public async Task AddBoundSourceFileAsync(string repoName, BoundSourceFile boundSourceFile)
        {
            var sourceFileInfo = boundSourceFile.SourceFile.Info;

            boundSourceFile.RepositoryName = boundSourceFile.RepositoryName ?? sourceFileInfo.RepositoryName;
            boundSourceFile.RepoRelativePath = boundSourceFile.RepoRelativePath ?? sourceFileInfo.RepoRelativePath;

            // TODO: These properties should not be defined on ISourceFileInfo as they require binding information
            boundSourceFile.Language = boundSourceFile.Language ?? sourceFileInfo.Language;
            boundSourceFile.ProjectRelativePath = boundSourceFile.ProjectRelativePath ?? sourceFileInfo.ProjectRelativePath;

            var textModel = new TextSourceSearchModel()
            {
                File = boundSourceFile.SourceFile,
            };

            ComputeContentId(textModel, isContentAddressed: true);

            var boundSourceModel = new BoundSourceSearchModel()
            {
                BindingInfo = boundSourceFile,
                TextUid = textModel.Uid,
                CompressedClassifications = new ClassificationListModel(boundSourceFile.Classifications)
            };

            await Service.UseClient(async context =>
            {
                var existingSourceTreeId = await TryGetSourceHashTreeId(boundSourceModel);
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

            batch.Add(TextSourceStore, textModel);
            batch.Add(BoundSourceStore, boundSourceModel);

            foreach (var property in boundSourceFile.SourceFile.Info.Properties)
            {
                batch.Add(PropertyStore, new PropertySearchModel()
                {
                    Uid = Placeholder.Value<string>("Populate fields"),
                    Key = property.Key,
                    Value = property.Value,
                    OwnerId = boundSourceModel.Uid,
                });
            }

            foreach (var definitionSpan in boundSourceFile.Definitions.Where(ds => !ds.Definition.ExcludeFromSearch))
            {
                batch.Add(DefinitionStore, new DefinitionSearchModel()
                {
                    Uid = Placeholder.Value<string>("Populate fields"),
                    Definition = definitionSpan.Definition,
                });
            }

            var referenceLookup = boundSourceFile.References
                .Where(r => !(r.Reference.ExcludeFromSearch))
                .ToLookup(r => r.Reference, ReferenceListModel.ReferenceSymbolEqualityComparer);

            foreach (var referenceGroup in referenceLookup)
            {
                var referenceModel = new ReferenceSearchModel((IProjectFileScopeEntity)textModel)
                {
                    Uid = Placeholder.Value<string>("Populate fields"),
                    Reference = new Symbol(referenceGroup.Key)
                };

                var spanList = referenceGroup.AsReadOnlyList();

                Placeholder.NotImplemented($"Group by symbol as in {nameof(Storage.DataModel.SourceFileModel.GetSearchReferences)}");
                if (referenceGroup.Count() < 10)
                {
                    // Small number of references, just store simple list
                    Placeholder.Todo("Verify that this does not store the extra fields on IReferenceSpan and just the Symbol span fields");
                    referenceModel.Spans = spanList;
                }
                else
                {
                    referenceModel.CompressedSpans = new SymbolLineSpanListModel(spanList);
                }

                batch.Add(ReferenceStore, referenceModel);
            }
        }

        private void ComputeContentId(ISearchEntity searchEntity, bool isContentAddressed)
        {
            Placeholder.NotImplemented("Serialize and hash entity");
        }

        private BoundSourceSearchModel CreateSourceModel(string repoName, BoundSourceFile boundSourceFile)
        {
            return new BoundSourceSearchModel()
            {
                BindingInfo = boundSourceFile
            };
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
