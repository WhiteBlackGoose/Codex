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
using Codex.Analysis.External;

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class AnalysisTests
    {
        /// <summary>
        /// This test doesn't actually verify anything. It just provides an easy way of viewing mappings
        /// </summary>
        [Test]
        public void TestSemanticStoreLoad()
        {
            var semanticStore = new CodexSemanticStore(@"C:\temp\dsc\dsc");
            semanticStore.Load();
        }

        [Test]
        public void TestComputeWebAddress()
        {
            Assert.Pass(StoreUtilities.GetFileWebAddress("https://ref12.visualstudio.com/Ref12/_git/_full/Codex", "foo.cs"));
        }
    }
}
