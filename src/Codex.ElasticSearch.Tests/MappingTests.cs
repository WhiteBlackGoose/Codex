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
    public class MappingTests
    {
        /// <summary>
        /// This test doesn't actually verify anything. It just provides an easy way of viewing mappings
        /// </summary>
        [Test]
        public void TestMappings()
        {
            var mapping = new TypeMappingDescriptor<IDefinitionSearchModel>().AutoMapEx();
            Assert.Pass(mapping.ElasticSerialize());
        }

        [Test]
        public void TestStoredFilterMappings()
        {
            var mapping = new TypeMappingDescriptor<IStoredFilter>().AutoMapEx();
            Assert.Pass(mapping.ElasticSerialize());
        }
    }
}
