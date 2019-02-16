using Codex.ObjectModel;
using Codex.Sdk.Utilities;
using Codex.Serialization;
using Codex.Storage.Utilities;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.Utilities
{
    public static class TextIndexingUtilities
    {
        public static ICollection<T> ToCollection<T>(this T value) where T : class
        {
            Contract.Ensures(Contract.Result<ICollection<T>>() != null);

            if (value == default(T))
            {
                // Use singleton array after switching to .NET 4.6
                return new T[] { };
            }

            return new T[] { value };
        }

        public static SourceFile FromChunks(IChunkedSourceFile chunkedFile, IEnumerable<ITextChunkSearchModel> chunks)
        {
            var sourceFile = new SourceFile(chunkedFile);
            var chunkMap = chunks.ToDictionarySafe(c => c.Uid, c => c.Chunk);
            using (var sbLease = Pools.StringBuilderPool.Acquire())
            {
                var sb = sbLease.Instance;
                foreach (var chunkRef in chunkedFile.Chunks)
                {
                    foreach (var line in chunkMap[chunkRef.Id].ContentLines)
                    {
                        // TODO: This should probably be handled by custom serializer
                        FullTextUtilities.DecodeFullText(line, sb);
                    }
                }

                sourceFile.Content = sb.ToString();
            }

            return sourceFile;
        }

        public static void ToChunks(this ISourceFile sourceFile, bool excludeFromSearch, out ChunkedSourceFile chunkFile, out IReadOnlyList<TextChunkSearchModel> chunks, bool encodeFullText = true)
        {
            var lines = sourceFile.Content.GetLines(includeLineBreak: true);
            var lineChunks = IndexingUtilities.GetTextIndexingChunks(lines);

            chunkFile = new ChunkedSourceFile(sourceFile);
            var chunkList = new List<TextChunkSearchModel>();
            chunks = chunkList;

            int startLineNumber = 0;
            foreach (var lineChunk in lineChunks)
            {
                var chunk = new SourceFileContentChunk();
                chunk.ContentLines.AddRange(lineChunk);
                if (encodeFullText)
                {
                    // TODO: This should probably be handled by custom serializer
                    //FullTextUtilities.EncodeFullText(chunk.ContentLines);
                }

                var chunkSearchModel = new TextChunkSearchModel();
                if (excludeFromSearch)
                {
                    chunkSearchModel.RawChunk = chunk;
                }
                else
                {
                    chunkSearchModel.Chunk = chunk;
                }

                chunkSearchModel.PopulateContentIdAndSize();
                chunkList.Add(chunkSearchModel);
                chunkFile.Chunks.Add(new ChunkReference()
                {
                    Id = chunkSearchModel.Uid,
                    StartLineNumber = startLineNumber
                });

                startLineNumber += lineChunk.Count;
            }
        }
    }
}