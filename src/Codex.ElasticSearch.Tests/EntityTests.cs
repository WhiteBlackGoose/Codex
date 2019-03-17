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

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class EntityTests
    {
        /// <summary>
        /// This test doesn't actually verify anything. It just provides an easy way of viewing mappings
        /// </summary>
        [Test]
        public void TestDefinition()
        {
            verifyShortNameAbbreviation("ElasticSearchBatch", "esb");
            verifyShortNameAbbreviation("IDefinitionSymbol", "ids");
            verifyShortNameAbbreviation("IDEHelperClass", "ihc");
            verifyShortNameAbbreviation("ESIntegrationTests", "eit");
            verifyShortNameAbbreviation("IDS", null);
            verifyShortNameAbbreviation("IDefinition", null);
            verifyShortNameAbbreviation("PrefixFilterIdentifier25NGramAnalyzer", "pfinga");

            void verifyShortNameAbbreviation(string shortName, string abbreviation)
            {
                var definition = new DefinitionSymbol() { ShortName = shortName };
                Assert.AreEqual(abbreviation, definition.AbbreviatedName?.ToLowerInvariant());
            }
        }
    }
}
