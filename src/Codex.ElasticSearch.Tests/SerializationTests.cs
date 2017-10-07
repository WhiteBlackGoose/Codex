using Codex.Framework.Types;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Utilities;
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

            var entityResult = symbol.SerializeEntity();
            var defaultResult = DefaultSerialize(symbol);

            Assert.AreNotEqual(entityResult, defaultResult, "Members not on serialization interface should be excluded");
            Assert.False(entityResult.ToLowerInvariant().Contains("value"), "SymbolId should be serialized as string rather than object");
            Assert.Pass(entityResult);
        }

        [Test]
        public void TestHashing()
        {
            var content = string.Join("||", Enumerable.Range(0, 10000));
            EncoderContext context = new EncoderContext();
            context.StringBuilder.Append(content);
            var contentHash = context.ToBase64HashString();
            var sameContentHash = context.ToBase64HashString();
            Assert.AreEqual(contentHash, sameContentHash, "Hashes of the same content should be the same");

            for (int i = 0; i < 20; i++)
            {
                context.StringBuilder.Append("0");
                var differentContentHash = context.ToBase64HashString();
                Assert.AreNotEqual(contentHash, differentContentHash, "Hashes of the different content should NOT be the same");
            }
        }

        [Test]
        public void TestOnlyAllowedStagesSerialized()
        {
            var definition1 = new DefinitionSearchModel()
            {
                Definition = new DefinitionSymbol()
                {
                    DocumentationInfo = new DocumentationInfo()
                    {
                        Comment = "One"
                    }
                }
            };

            var definition2 = new DefinitionSearchModel()
            {
                Definition = new DefinitionSymbol()
                {
                    DocumentationInfo = new DocumentationInfo()
                    {
                        Comment = "Two"
                    }
                }
            };

            definition1.PopulateContentIdAndSize();
            definition2.PopulateContentIdAndSize();

            // Verify that the entity content id does not change due to changes in members excluded from serialization
            Assert.AreEqual(definition1.Uid, definition2.Uid);
            Assert.AreEqual(definition1.EntityContentId, definition2.EntityContentId);
            Assert.AreEqual(definition1.SerializeEntity(ObjectStage.Index), definition2.SerializeEntity(ObjectStage.Index));

            // Analysis object stage contains DocumentationInfo so serialization should be different now
            Assert.AreNotEqual(definition1.SerializeEntity(ObjectStage.Analysis), definition2.SerializeEntity(ObjectStage.Analysis));
            Assert.AreNotEqual(definition1.SerializeEntity(ObjectStage.All), definition2.SerializeEntity(ObjectStage.All));

            // Verify the comment is MISSING for the serilialized string for Index stage
            Assert.False(definition1.SerializeEntity(ObjectStage.Index).Contains(definition1.Definition.DocumentationInfo.Comment));
            Assert.False(definition2.SerializeEntity(ObjectStage.Index).Contains(definition2.Definition.DocumentationInfo.Comment));

            // Verify the comment is PRESENT for the serilialized string for Analysis or All stage
            Assert.True(definition2.SerializeEntity(ObjectStage.All).Contains(definition2.Definition.DocumentationInfo.Comment));
            Assert.True(definition2.SerializeEntity(ObjectStage.Analysis | ObjectStage.Index).Contains(definition2.Definition.DocumentationInfo.Comment));
            Assert.True(definition2.SerializeEntity(ObjectStage.Analysis).Contains(definition2.Definition.DocumentationInfo.Comment));

            Assert.Pass(definition1.SerializeEntity(ObjectStage.Index));
        }

        private string DefaultSerialize(object obj)
        {
            var serializer = JsonSerializer.CreateDefault();

            return serializer.Serialize(obj);
        }
    }

}
