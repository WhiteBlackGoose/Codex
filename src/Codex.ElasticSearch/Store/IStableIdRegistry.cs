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
        Task SetStableIdsAsync(IReadOnlyList<IStableIdItem> items);
    }

    public interface IStableIdItem
    {
        /// <summary>
        /// The group to reserve the stable id in
        /// </summary>
        int StableIdGroup { get; }

        /// <summary>
        /// The unique identifier for item
        /// </summary>
        string Uid { get; }

        /// <summary>
        /// The index name in which the item will be stored
        /// </summary>
        string IndexName { get; }

        /// <summary>
        /// Sets the stable id for the item
        /// </summary>
        void SetStableId(int stableId);
    }
}
