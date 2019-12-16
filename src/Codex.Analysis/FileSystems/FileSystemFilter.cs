using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.FileSystems
{
    public class FileSystemFilter
    {
        public virtual bool IncludeDirectory(FileSystem fileSystem, string directoryPath) => true;

        public virtual bool IncludeFile(FileSystem fileSystem, string filePath) => true;
    }

    public static class FileSystemFilterExtensions
    {
        public static FileSystemFilter Combine(this FileSystemFilter f1, FileSystemFilter f2)
        {
            if (f1 == null) return f2;
            else if (f2 == null) return f1;
            else
            {
                return new MultiFileSystemFilter(f1, f2);
            }
        }
    }
}
