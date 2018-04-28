using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public class FileSystem : IDisposable
    {
        public FileSystem()
        {
        }

        public virtual IEnumerable<string> GetFiles()
        {
            return new string[0];
        }

        public virtual IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            return new string[0];
        }

        public virtual Stream OpenFile(string filePath)
        {
            return Stream.Null;
        }

        public virtual void Dispose()
        {
        }
    }

    public class SystemFileSystem : FileSystem
    {
        public override Stream OpenFile(string filePath)
        {
            return File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
    }
}