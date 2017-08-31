using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types
{
    /// <summary>
    /// High level storage operations
    /// </summary>
    public interface IStore<T> : IStore
    {
        // TODO: Generate preprocess
        Task StoreAsync(T value);
    }

    public interface IStore
    {
        Task InitializeAsync();

        Task FinalizeAsync();
    }
}
