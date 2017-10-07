using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Utilities
{
    internal class ElasticConstants
    {
        /// <summary>
        /// Max size of batches in bytes
        /// </summary>
        public const int BatchSizeBytes = (10 << 20);
    }
}
