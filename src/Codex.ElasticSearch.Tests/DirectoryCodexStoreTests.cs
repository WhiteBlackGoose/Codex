using Codex.ElasticSearch.Store;
using Codex.Utilities;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class DirectoryCodexStoreTests
    {
        /// <summary>
        /// Verifies storage of entities in DirectoryCodexStore with optimized storage.
        /// 1. Reads from a store in unoptimized form. 
        /// 2. Writes into an optimized form. 
        /// 3. Reads optimized form and writes to unoptimized form.
        /// 4. Verify original unoptimized form matches roundtripped unoptimized form written in step 3.
        /// </summary>
        [Test]
        public async Task TestRoundtrip()
        {
            var originalStore = CreateInputStore();

            var optimizedOutputStore = CreateOutputStore("opt");
            await originalStore.ReadAsync(optimizedOutputStore);

            // Now read in optimized output store and write to unoptimized output store
            var optimizedInputStore = new DirectoryCodexStore(optimizedOutputStore.DirectoryPath);
            var roundtrippedStore = CreateOutputStore("unopt", disableOptimization: true);
            await optimizedInputStore.ReadAsync(roundtrippedStore);

            var originalEntityFiles = GetEntityFileMap(originalStore.DirectoryPath);
            var roundtrippedEntityFiles = GetEntityFileMap(roundtrippedStore.DirectoryPath);

            var addedFiles = roundtrippedEntityFiles.Keys.Except(originalEntityFiles.Keys, StringComparer.OrdinalIgnoreCase).ToList();
            var removedFiles = originalEntityFiles.Keys.Except(roundtrippedEntityFiles.Keys, StringComparer.OrdinalIgnoreCase).ToList();

            Assert.AreEqual(0, addedFiles.Count);
            Assert.AreEqual(0, removedFiles.Count);
            Assert.AreNotEqual(0, originalEntityFiles.Keys.Count);

            foreach (var relativePath in originalEntityFiles.Keys)
            {
                var originalContents = File.ReadAllText(originalEntityFiles[relativePath]);
                var roundtrippedContents = File.ReadAllText(roundtrippedEntityFiles[relativePath]);
                Assert.AreEqual(originalContents, roundtrippedContents);
            }
        }

        private static Dictionary<string, string> GetEntityFileMap(string directoryPath)
        {
            directoryPath = PathUtilities.EnsureTrailingSlash(directoryPath);
            return DirectoryCodexStore.GetEntityFiles(directoryPath).ToDictionary(p => p.Substring(directoryPath.Length), StringComparer.OrdinalIgnoreCase);
        }

        private DirectoryCodexStore CreateOutputStore(string name, bool disableOptimization = false)
        {
            return new DirectoryCodexStore(Path.Combine(TestContext.CurrentContext.TestDirectory, name))
            {
                DisableOptimization = disableOptimization
            };
        }

        public static DirectoryCodexStore CreateInputStore()
        {
            return new DirectoryCodexStore(Path.Combine(TestInputsDirectoryHelper.FullPath, "test1"));
        }
    }
}
