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

namespace Codex.ElasticSearch
{
    public class LegacyElasticSearchStoreConfiguration
    {
        /// <summary>
        /// The ElasticSearch endpoint (i.e. http://localhost:9200)
        /// </summary>
        public string Endpoint { get; set; }
    }
}
