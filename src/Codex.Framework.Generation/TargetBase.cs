using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Generation
{
    class TargetBase
    {
        protected static string GetFilePath(string fileName, [CallerFilePath] string filePath = null)
        {
            return Path.Combine(Path.GetDirectoryName(filePath), fileName);
        }
    }
}
