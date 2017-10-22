using Codex.ElasticSearch.Utilities;
using Codex.Framework.Types;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Storage.ElasticProviders;
using Codex.Utilities;
using Nest;
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
    public class MappingTets
    {
        [Test]
        public void TestMappings()
        {
            var mapping = new TypeMappingDescriptor<IBoundSourceSearchModel>().AutoMap(MappingPropertyVisitor.Instance);
            Assert.Pass(mapping.ElasticSerialize());
        }
    }
}
