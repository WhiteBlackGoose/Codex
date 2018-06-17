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
    public interface IStableIdRegistry
    {
        /// <summary>
        /// Reserves and sets the stable ids on the items
        /// </summary>
        Task<IStableIdRegistration> SetStableIdsAsync(IReadOnlyList<IStableIdItem> items);

        Task FinalizeAsync();
    }

    public interface IStableIdRegistration : IDisposable
    {
        void Report(IStableIdItem item, bool used);
    }

    public interface IStableIdItem
    {
        /// <summary>
        /// The group to reserve the stable id in
        /// </summary>
        int StableIdGroup { get; }

        int StableId { get; set; }

        SearchType SearchType { get; }
    }
}
