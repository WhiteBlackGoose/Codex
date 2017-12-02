using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Tests
{
    /// <summary>
    /// A file inside the test directory to read for getting its path easily
    /// </summary>
    public class TestInputsDirectoryHelper
    {
        public static readonly string FullPath = GetPath();

        private static string GetPath([CallerFilePath] string filePath= null)
        {
            return Path.GetDirectoryName(filePath);
        }
    }
}
