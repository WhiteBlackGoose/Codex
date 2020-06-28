using Codex.Sdk.Search;

namespace Codex.ObjectModel
{
    public partial interface IClient { }

    public abstract partial class ClientBase : IClient
    {
        public abstract IIndex<T> CreateIndex<T>(SearchType<T> searchType)
            where T : class, ISearchEntity;
    }
}