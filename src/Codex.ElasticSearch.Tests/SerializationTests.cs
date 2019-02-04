using Codex.ElasticSearch.Tests.Properties;
using Codex.Framework.Types;
using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Storage.DataModel;
using Codex.Utilities;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
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
        public void TestStoredFilterSerialization()
        {
            var filter = new StoredFilter()
            {
                StableIds = new byte[] { 0, 2, 43 },
            };

            var filterResult = filter.SerializeEntity();
            Assert.Pass(filterResult);
        }

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
                Language = "testLanguage",
                References = new[]
                    {
                        new ReferenceSpan()
                        {
                            Reference = new DefinitionSymbol()
                            {
                                ContainerQualifiedName = "excluded.cqn",
                                ReferenceKind = "rkind"
                            }
                        }
                    }
            };

            var referenceSpanList = new ReferenceSearchModel()
            {
                Spans = new[]
                {
                    new ReferenceSpan()
                    {
                        Reference = new ReferenceSymbol()
                        {
                            Id = SymbolId.UnsafeCreateWithValue("excluded.refid")
                        },
                        Start = 323
                    }
                }
            };

            var entity = new BoundSourceSearchModel()
            {
                BindingInfo = boundInfo,
                CompressedReferences = new ReferenceListModel(boundInfo.References)
            };

            var entityResult = (refs: referenceSpanList, bound: entity).ElasticSerialize();
            Assert.True(entityResult.ToLowerInvariant().Contains("rkind"), "Property of ReferenceSymbol should be serialized when the property type is ReferenceSymbol");
            Assert.False(entityResult.ToLowerInvariant().Contains("cqn"), "Property of DefinitionSymbol should not be serialized when the property type is ReferenceSymbol");
            Assert.False(entityResult.ToLowerInvariant().Contains("refid"), "Property of ReferenceSpan should not be serialized when the property type is SymbolSpan");
            Assert.Pass(entityResult);
        }

        [Test]
        public void TestDefaultValueSerialization()
        {
            var entity = new BoundSourceInfo()
            { 
                Language = "testLanguage"
            };

            var entityResult = entity.SerializeEntity();
            var elasticResult = entity.ElasticSerialize();

            Assert.Pass(elasticResult);
        }

        private IEnumerable<ArraySegment<byte>> GetByteSegments(byte[] bytes, int segmentLength)
        {
            for (int i = 0; i < bytes.Length; i += segmentLength)
            {
                yield return new ArraySegment<byte>(bytes, i, Math.Min(segmentLength, bytes.Length - i));
            }
        }

        [Test]
        public void TestChunking()
        {
            var chunkMap = new ConcurrentDictionary<string, (int count, IReadOnlyList<string> chunk)>();
            var lineMap = new ConcurrentDictionary<string, int>();
            var fileSlices = Resources.ChunkedFileHistory.Split(new[] { "###" }, StringSplitOptions.RemoveEmptyEntries)
                .Where(f => f.Length > 2000) // Remove header
                .Select(f => f.Trim())
                .ToArray();
            var chunkInfo = new List<(int chunkIndex, int chunkCount, int startLine, int endLine)>();

            int deduplicatedChunks = 0;
            int deduplicatedLines = 0;
            int totalLines = 0;
            int totalDuplicateLines = 0;
            int totalChunks = 0;
            int totalChunkLines = 0;
            var encoderContext = new EncoderContext();
            foreach (var fileSlice in fileSlices)
            {
                var lines = fileSlice.GetLines(includeLineBreak: true);
                foreach (var line in lines)
                {
                    var count = lineMap.AddOrUpdate(line, 1, (k, v) => v + 1);
                    if (count > 1)
                    {
                        totalDuplicateLines++;
                    }
                }

                totalLines += lines.Count;
                var chunks = IndexingUtilities.GetTextIndexingChunks(lines);
                totalChunks += chunks.Count;
                int chunkIndex = 0;
                foreach (var chunk in chunks)
                {
                    chunkIndex++;
                    totalChunkLines += chunk.Count;

                    var hash = encoderContext.ToBase64HashString(chunk);
                    var value = chunkMap.AddOrUpdate(hash, (1, chunk), (k, v) => (v.count + 1, v.chunk));
                    if (value.count > 1)
                    {
                        chunkInfo.Add((chunkIndex, chunks.Count, chunk.Start, chunk.End));
                        deduplicatedChunks++;
                        deduplicatedLines += chunk.Count;
                    }
                }
            }

            Assert.AreEqual(totalLines, totalChunkLines);
            Assert.LessOrEqual(fileSlices.Length, deduplicatedChunks);
            Assert.Pass($"Total Lines: {totalLines}, Total Dupe Lines: {totalDuplicateLines}, Deduped Lines: {deduplicatedLines}, Deduplicated Chunks: {deduplicatedChunks}, Total Chunks: {totalChunks}\n{string.Join(Environment.NewLine, chunkInfo)}");
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

            var bytes = Encoding.UTF8.GetBytes(content);
            var fullHash = new Murmur3().ComputeHash(bytes).ToBase64String();
            for (int i = 3; i < 7; i++)
            {
                var segmentLength = (int)Math.Pow(10, i);
                var segmentsHash = new Murmur3().ComputeHash(GetByteSegments(bytes, segmentLength)).ToBase64String();
                Assert.AreEqual(fullHash, segmentsHash, "Hash based on segments and full content should be the same");
            }

            for (int charBufferSize = 1024; charBufferSize < 1030; charBufferSize++)
            {
                EncoderContext context2 = new EncoderContext(charBufferSize);
                context2.StringBuilder.Append(content);
                var contentHash2 = context2.ToBase64HashString();
                Assert.AreEqual(contentHash, contentHash2, "Hashes of the same content should be the same");
            }

            for (int charBufferSize = 10000; charBufferSize < 10005; charBufferSize++)
            {
                EncoderContext context2 = new EncoderContext(charBufferSize);
                context2.StringBuilder.Append(content);
                var contentHash2 = context2.ToBase64HashString();
                Assert.AreEqual(contentHash, contentHash2, "Hashes of the same content should be the same");
            }

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
