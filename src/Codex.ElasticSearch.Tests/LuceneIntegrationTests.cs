using Codex.ElasticSearch.Formats;
using Codex.ElasticSearch.Search;
using Codex.ElasticSearch.Store;
using Codex.Lucene.Search;
using Codex.ObjectModel;
using Codex.Sdk.Search;
using Codex.Serialization;
using Codex.Utilities;
using CodexTestCSharpLibrary;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class LuceneIntegrationTests
    {
        [Test]
        public async Task TestReferences()
        {
            (var store, var codex) = await InitializeAsync("estest.", populateCount: 1);


        }

        private async Task<(ICodexStore store, ICodex codex)> InitializeAsync(
            string prefix,
            int populateCount,
            bool clear = false,
            SearchType[] activeIndices = null,
            [CallerMemberName] string testName = null)
        {
            bool populate = populateCount > 0;

            string directory = Path.GetFullPath($@"tests\{testName}");
            if (Directory.Exists(directory))
            {
                Directory.Delete(testName, true);
            }

            var configuration = new LuceneConfiguration(directory);

            var store = new LuceneCodexStore(configuration);

            //await store.InitializeAsync();

            if (populate)
            {
                DirectoryCodexStore originalStore = DirectoryCodexStoreTests.CreateInputStore();
                //await store.FinalizeAsync();

                for (int i = 0; i < populateCount; i++)
                {
                    await originalStore.ReadAsync(store);
                }
            }

            var codex = new LuceneCodex(configuration);

            return (store, codex);
        }
    }
}
