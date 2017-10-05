using Codex.ObjectModel;
using Codex.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ElasticSearch.Tests
{
    [TestFixture]
    public class SerializationTests
    {
        [Test]
        public void TestOnlyInterfaceMembersSerialized()
        {
            var symbol = new Symbol()
            {
                Id = SymbolId.UnsafeCreateWithValue("sid"),
                ProjectId = "pid",
                Kind = nameof(SymbolKinds.File),
                ExtData = new ExtensionData()
            };

            var entityResult = Serialize(symbol);
            var defaultResult = Serialize(symbol, asEntity: false);

            Assert.AreNotEqual(entityResult, defaultResult, "Members not on serialization interface should be excluded");
        }

        [Test]
        public void TestOnlyAllowedStagesSerialized()
        {
            Placeholder.NotImplemented("Also ensure only allowed stages are serialized");

            // TODO: Add your test code here
            Assert.Pass("Your first passing test");
        }

        private string Serialize(object obj, bool asEntity = true)
        {
            var serializer = asEntity ?
                JsonSerializer.Create(new JsonSerializerSettings()
                {
                    ContractResolver = new EntityContractResolver()
                }) :
                JsonSerializer.CreateDefault();

            return serializer.Serialize(obj);
        }
    }

}
