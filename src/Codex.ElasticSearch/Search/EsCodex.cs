//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Codex.ObjectModel;
//using Codex.Sdk.Search;
//using Codex.Search;
//using Nest;

//namespace Codex.ElasticSearch.Search
//{
//    public class EsCodex : CodexBase<EsIndexClient, ElasticSearchStoreConfiguration>
//    {
//        protected override Task<StoredFilterSearchContext<EsIndexClient>> GetStoredFilterContextAsync(ContextCodexArgumentsBase arguments)
//        {
//            throw new NotImplementedException();
//        }

//        protected override Task<IndexQueryHitsResponse<T>> UseClient<T>(ContextCodexArgumentsBase arguments, Func<StoredFilterSearchContext<EsIndexClient>, Task<IndexQueryHits<T>>> useClient)
//        {
//            throw new NotImplementedException();
//        }

//        protected override Task<IndexQueryResponse<T>> UseClientSingle<T>(ContextCodexArgumentsBase arguments, Func<StoredFilterSearchContext<EsIndexClient>, Task<T>> useClient)
//        {
//            throw new NotImplementedException();
//        }
//    }

//    public class EsIndexClient : ClientBase
//    {
//        private ElasticClient client;

//        public override IIndex<T> CreateIndex<T>(SearchType<T> searchType)
//        {
//            throw new NotImplementedException();
//        }

//        private class Index<T> : IIndex<T>
//            where T : class, ISearchEntity
//        {
//            private ElasticClient client;
//            private string indexName;

//            public  async Task<IReadOnlyList<T>> GetAsync(IStoredFilterInfo storedFilterInfo, params string[] ids)
//            {
//                var result = await client.MultiGetAsync(mg => mg.Index(indexName).GetMany<T>(ids));

//                return result.Hits.Select(h => (T)h.Source).ToList();
//            }

//            public Task<IIndexSearchResponse<TResult>> QueryAsync<TResult>(IStoredFilterInfo storedFilterInfo, Func<ICodexQueryBuilder<T>, CodexQuery<T>> filter, OneOrMany<Mapping<T>> sort = null, int? take = null, Func<ICodexQueryBuilder<T>, CodexQuery<T>> boost = null) where TResult : T
//            {
//                //client.SearchAsync<T>(sd =>
//                //{
//                //    sd.Query(qcd => qcd.store)
//                //})

//                throw new NotImplementedException();
//            }
//        }
//    }
//}
