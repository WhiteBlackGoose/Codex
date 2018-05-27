using Codex.Framework.Types;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Storage.DataModel;
using Codex.Utilities;
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
            var defaultResult = symbol.DefaultSerialize();

            Assert.AreNotEqual(entityResult, defaultResult, "Members not on serialization interface should be excluded");
            Assert.False(entityResult.ToLowerInvariant().Contains("value"), "SymbolId should be serialized as string rather than object");
            Assert.Pass(entityResult);
        }

        [Test]
        public void TestMembersSerializedByPropertyType()
        {
            var boundInfo = new BoundSourceInfo()
            {
                ProjectId = "testProjectId",
                References = new[]
                    {
                        new ReferenceSpan()
                        {
                            Reference = new DefinitionSymbol()
                            {
                                ContainerQualifiedName = "cqn",
                                ReferenceKind = "rkind"
                            }
                        }
                    }
            };

            var entity = new BoundSourceSearchModel()
            {
                BindingInfo = boundInfo,
                CompressedReferences = new ReferenceListModel(boundInfo.References)
            };

            var entityResult = entity.ElasticSerialize();
            Assert.True(entityResult.ToLowerInvariant().Contains("rkind"), "Property of ReferenceSymbol should be serialized when the property type is ReferenceSymbol");
            Assert.False(entityResult.ToLowerInvariant().Contains("cqn"), "Property of DefinitionSymbol should not be serialized when the property type is ReferenceSymbol");
            Assert.Pass(entityResult);
        }

        [Test]
        public void TestDefaultValueSerialization()
        {
            var entity = new BoundSourceInfo()
            {
                ProjectId = "testProjectId"
            };

            var entityResult = entity.SerializeEntity();
            var elasticResult = entity.ElasticSerialize();

            Assert.Pass(elasticResult);
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
    }

    internal static class TestSerializerExtensions
    {
        public static string ElasticSerialize(this object data)
        {
            using (var stream = new MemoryStream())
            {
                OverrideConnectionSettings ocs = new OverrideConnectionSettings(new Uri("http://localhost:0"));
                ocs.GetSerializer().Serialize(data, stream);

                stream.Position = 0;
                using (var sr = new StreamReader(stream))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        public static string DefaultSerialize(this EntityBase obj)
        {
            var serializer = JsonSerializer.CreateDefault();

            return serializer.Serialize(obj);
        }
    }
}
