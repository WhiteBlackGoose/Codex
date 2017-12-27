using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.View.Web
{
    public class ViewsPath
    {
        public static readonly string ViewsFilePath = GetPath("Views.xml");
        public static readonly string ViewsFolderPath = GetPath();

        private static string GetPath(string fileName = null, [CallerFilePath] string filePath = null)
        {
            return Path.Combine(Path.GetDirectoryName(filePath), fileName);
        }
    }
}
