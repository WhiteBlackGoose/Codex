using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Codex.Utilities
{
    /// <summary>
    /// Utility methods for paths and Uris (including relativizing and derelativizing)
    /// </summary>
    public static partial class PathUtilities
    {
        private static readonly char[] PathSeparatorChars = new char[] { '\\', '/' };

        public static string GetFileName(string path)
        {
            return path.Substring(path.LastIndexOfAny(PathSeparatorChars) + 1);
        }
    }
}