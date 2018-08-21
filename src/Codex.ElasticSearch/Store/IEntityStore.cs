using Codex.Framework.Types;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using System;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Storage.DataModel;
using static Codex.Utilities.SerializationUtilities;
using Codex.Utilities;
using Codex.Analysis;
using System.Collections.Concurrent;
using Nest;
using System.Collections.Generic;

namespace Codex.ElasticSearch
{
    public interface IEntityStore<T>
        where T : ISearchEntity
    {
        Task StoreAsync(IReadOnlyList<T> entities);

        Task<IReadOnlyList<T>> GetAsync(IReadOnlyList<string> uids);
    }
}
