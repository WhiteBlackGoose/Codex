using System.Threading.Tasks;
using Codex.Sdk.Utilities;

namespace Codex.ElasticSearch
{
    public interface IBatcher<TStoredFilterBuilder>
    {
        TStoredFilterBuilder[] DeclaredDefinitionStoredFilter { get; }

        void Add<T>(SearchType<T> searchType, T entity, params TStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity;

        ValueTask<None> AddAsync<T>(SearchType<T> searchType, T entity, params TStoredFilterBuilder[] additionalStoredFilters)
            where T : class, ISearchEntity;

        Task FinalizeAsync(string repositoryName);
    }
}