using Codex.ElasticSearch.Utilities;
using Codex.Framework.Types;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Storage.ElasticProviders;
using Codex.Utilities;
using Nest;
using static Nest.Infer;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ElasticSearch.Store;
using System.Runtime.CompilerServices;

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class DirectoryCodexStoreTests
    {
        /// <summary>
        /// This test doesn't actually verify anything. It just provides an easy way of viewing mappings
        /// </summary>
        [Test]
        public async Task TestRoundtrip()
        {
            var inputStore = CreateInputStore();

            var outputStoreDirectory = Directory.CreateDirectory(Path.Combine(TestContext.CurrentContext.TestDirectory, "outstore"));
            var outputStore = new DirectoryCodexStore(outputStoreDirectory.FullName);

            await inputStore.ReadAsync(outputStore);
            Assert.Pass(outputStoreDirectory.FullName);
        }

        private DirectoryCodexStore CreateInputStore([CallerFilePath] string filePath = null)
        {
            return new DirectoryCodexStore(TestInputsDirectoryHelper.FullPath) { DisableOptimization = true };
        }
    }
}
