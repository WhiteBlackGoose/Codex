using Codex.Sdk.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Utilities
{
    public static class IndexingUtilities
    {
        private const ulong HighBits = ulong.MaxValue - uint.MaxValue;
        private const ulong LowBits = uint.MaxValue;
        private const int UidLength = 12;

        private static readonly char[] s_toLowerInvariantCache = CreateToLowerInvariantCache();

        private static readonly char[] s_hexMap =
        {'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};

        private static char[] CreateToLowerInvariantCache()
        {
            var a = new char[char.MaxValue + 1];
            for (int c = char.MinValue; c <= char.MaxValue; c++)
            {
                a[c] = char.ToLowerInvariant((char)c);
            }

            return a;
        }

        /// <summary>
        /// <code>code.ToLowerInvariant</code> is surprisingly expensive; this is a cache
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ToLowerInvariantFast(this char character)
        {
            return s_toLowerInvariantCache[character];
        }

        /// <summary>
        /// Get the bytes as a hex string.
        /// </summary>
        public unsafe static string ToHex(this IReadOnlyList<byte> checksum)
        {
            char* charBuffer = stackalloc char[(2 * checksum.Count) + 1];
            var j = 0;

            for (var i = 0; i < checksum.Count; i++)
            {
                charBuffer[j++] = s_hexMap[(checksum[i] & 0xF0) >> 4];
                charBuffer[j++] = s_hexMap[checksum[i] & 0x0F];
            }

            charBuffer[j] = '\0';

            return new string(charBuffer);
        }

        public static string GetChecksumKey(ChecksumAlgorithm algorithm)
        {
            switch (algorithm)
            {
                case ChecksumAlgorithm.Sha1:
                    return "Checksum.Sha1";
                case ChecksumAlgorithm.Sha256:
                    return "Checksum.Sha256";
                case ChecksumAlgorithm.MD5:
                    return "Checksum.MD5";
                default:
                    return null;
            }
        }

        public static string ComputeSymbolUid(string symbolIdName)
        {
            //using (var leasedBuffer = Pools.ByteArrayPool.Acquire())
            //{
            //    var buffer = leasedBuffer.Instance;
            //    var max = Encoding.UTF8.GetMaxByteCount(symbolIdName.Length);

            //}

            Placeholder.Todo("Use more efficient method of computing symbol UID with fewer allocations");
            return ComputeSymbolUidOld(symbolIdName);
        }

        public static string ComputeSymbolUidOld(string symbolIdName)
        {
            string uidPadded = ComputeHashString(symbolIdName);

            char[] uidChars = uidPadded.ToCharArray(0, UidLength);
            for (int i = 0; i < uidChars.Length; i++)
            {
                char c = uidChars[i];
                if (i == 0 && char.IsNumber(c))
                {
                    c = (char)(c - '0' + 'a');
                }
                else if (c == '/')
                {
                    c = 'y';
                }
                else if (c == '+')
                {
                    c = 'z';
                }
                else
                {
                    c = c.ToLowerInvariantFast();
                }

                uidChars[i] = c;
            }

            return new string(uidChars);
        }

        public static string ComputeHashString(string symbolIdName)
        {
            var max = Math.Max(MurmurHash.BYTE_LENGTH, Encoding.UTF8.GetMaxByteCount(symbolIdName.Length));
            byte[] buffer = new byte[max];
            var hash = ComputeFullHash(symbolIdName, buffer);

            for (int i = 0; i < MurmurHash.BYTE_LENGTH; i++)
            {
                buffer[i] = hash.GetByte(i);
            }

            var uidPadded = Convert.ToBase64String(buffer, 0, MurmurHash.BYTE_LENGTH);
            return uidPadded;
        }

        public static MurmurHash ComputeFullHash(string value, byte[] buffer = null)
        {
            var max = Encoding.UTF8.GetMaxByteCount(value.Length);
            if (buffer == null || buffer.Length < max)
            {
                buffer = new byte[max];
            }

            int byteLength = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);
            if (buffer.Length < 1000)
            {
                // For backward compatibility with old code which would use the full buffer when calculating
                // the hash rather than just the portion which is occupied by the serialized bytes.
                // Use the more efficient mechanism for large strings
                byteLength = buffer.Length;
            }

            var hasher = new Murmur3();
            return hasher.ComputeHash(buffer, length: byteLength);
        }

        public static MurmurHash ComputePrefixHash(string value, byte[] buffer = null)
        {
            var max = Encoding.UTF8.GetMaxByteCount(value.Length);
            if (buffer == null || buffer.Length < max)
            {
                buffer = new byte[max];
            }

            int byteLength = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, 0);

            var hasher = new Murmur3();
            int remaining = byteLength;

            uint int0, int1, int2, int3;
            int start = 0;
            int length = Math.Min(4, remaining);

            var hash = hasher.ComputeHash(buffer, start, length);
            int0 = hash.GetInt(0);
            start += length;
            remaining -= length;
            length <<= 1;

            length = Math.Min(length, remaining);
            if (length > 0)
            {
                hash = hasher.ComputeHash(buffer, start, length);
            }

            int1 = hash.GetInt(1);
            remaining -= length;
            start += length;
            length <<= 1;

            length = Math.Min(length, remaining);
            if (length > 0)
            {
                hash = hasher.ComputeHash(buffer, start, length);
            }

            int2 = hash.GetInt(2);
            remaining -= length;
            start += length;

            if (remaining > 0)
            {
                hash = hasher.ComputeHash(buffer, start, remaining);
            }

            int3 = hash.GetInt(3);

            return new MurmurHash(int0, int1, int2, int3);
        }

        public static QualifiedName ParseQualifiedName(string fullyQualifiedTerm)
        {
            QualifiedName qn = new QualifiedName();
            if (fullyQualifiedTerm == null)
            {
                return qn;
            }

            int indexOfLastDot = fullyQualifiedTerm.LastIndexOf('.');
            if (indexOfLastDot >= 0)
            {
                qn.ContainerName = fullyQualifiedTerm.Substring(0, indexOfLastDot);
            }

            qn.Name = fullyQualifiedTerm.Substring(indexOfLastDot + 1);
            return qn;
        }

        public static List<ListSegment<string>> GetTextIndexingChunks(IReadOnlyList<string> lines)
        {
            return TextChunker.GetConsistentChunks(lines, chunkSizeHint: lines.Count / 100, minChunkSize: 10);
        }
    }

    public class QualifiedName
    {
        public string ContainerName;
        public string Name;
    }

    public enum ChecksumAlgorithm
    {
        MD5,
        Sha1,
        Sha256
    }
}
