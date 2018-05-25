using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IFileStatistics
    {
        /// <summary>
        /// The numbef of files
        /// </summary>
        long FileCount { get; }

        /// <summary>
        /// The number of classifications
        /// </summary>
        long Classifications { get; }

        /// <summary>
        /// The number of definitions
        /// </summary>
        long Definitions { get; }

        /// <summary>
        /// The number of references
        /// </summary>
        long References { get; }

        /// <summary>
        /// The number of lines of code in files
        /// </summary>
        long Lines { get; }

        /// <summary>
        /// The total size of files
        /// </summary>
        long Size { get; }

        /// <summary>
        /// The total size of analysis files
        /// </summary>
        long AnalyzedSize { get; }
    }
}
