using Codex.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public class DirectoryFileSystem : SystemFileSystem
    {
        public readonly string RootDirectory;
        private readonly string SearchPattern;

        public DirectoryFileSystem(string rootDirectory, string searchPattern = "*.*")
        {
            RootDirectory = rootDirectory;
            SearchPattern = searchPattern;
        }

        public override IEnumerable<string> GetFiles()
        {
            return Directory.GetFiles(RootDirectory, SearchPattern, SearchOption.AllDirectories);
        }

        public override IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            return Directory.GetFiles(Path.Combine(RootDirectory, relativeDirectoryPath), SearchPattern, SearchOption.AllDirectories);
        }
    }

    public class ZipFileSystem : FileSystem
    {
        public readonly string ArchivePath;
        private ZipArchive zipArchive;

        public ZipFileSystem(string archivePath)
        {
            ArchivePath = archivePath;
            zipArchive = new ZipArchive(File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read, leaveOpen: false);
        }

        public override Stream OpenFile(string filePath)
        {
            lock (zipArchive)
            {
                MemoryStream memoryStream = new MemoryStream();

                using (var entryStream = zipArchive.GetEntry(filePath).Open())
                {
                    entryStream.CopyTo(memoryStream);
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
        }

        public override IEnumerable<string> GetFiles()
        {
            return zipArchive.Entries.Where(e => e.Length != 0).Select(e => e.FullName).ToArray();
        }

        public override IEnumerable<string> GetFiles(string relativeDirectoryPath)
        {
            relativeDirectoryPath = PathUtilities.EnsureTrailingSlash(relativeDirectoryPath, '\\');
            return GetFiles().Where(n => n.Replace('/', '\\').StartsWith(relativeDirectoryPath));
        }

        public override void Dispose()
        {
            zipArchive.Dispose();
            zipArchive = null;
        }
    }
}