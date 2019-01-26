using Codex.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Codex.Sdk.Utilities
{
    public class EncoderContext
    {
        public readonly StringBuilder StringBuilder = new StringBuilder();
        public readonly List<ulong> UIntList = new List<ulong>();
        public readonly byte[] ByteBuffer;
        public readonly char[] CharBuffer;
        public readonly StringWriter Writer;

        public EncoderContext(int charBufferSize = 1024)
        {
            Writer = new StringWriter(StringBuilder);
            CharBuffer = new char[charBufferSize];
            ByteBuffer = new byte[Encoding.UTF8.GetMaxByteCount(charBufferSize)];
        }

        public void Reset()
        {
            StringBuilder.Clear();
            UIntList.Clear();
        }

        public string ToBase64HashString(IEnumerable<string> content)
        {
            return ToHash(content).ToBase64String();
        }

        public MurmurHash ToHash(IEnumerable<string> contentStream)
        {
            return new Murmur3().ComputeHash(contentStream.SelectMany(content => GetByteStream(content)));
        }

        public string ToBase64HashString(string content)
        {
            return ToHash(content).ToBase64String();
        }

        public MurmurHash ToHash(string content)
        {
            return new Murmur3().ComputeHash(GetByteStream(content));
        }

        public string ToBase64HashString()
        {
            return new Murmur3().ComputeHash(GetByteStream()).ToBase64String();
        }

        public IEnumerable<ArraySegment<byte>> GetByteStream()
        {
            int offset = 0;
            int remainingChars = StringBuilder.Length;
            var builder = StringBuilder;
            var chars = CharBuffer;
            var bytes = ByteBuffer;
            while (remainingChars > 0)
            {
                var copiedChars = Math.Min(remainingChars, chars.Length);
                builder.CopyTo(offset, chars, 0, copiedChars);
                var byteLength = Encoding.UTF8.GetBytes(chars, 0, copiedChars, bytes, 0);
                yield return new ArraySegment<byte>(bytes, 0, byteLength);
                offset += copiedChars;
                remainingChars -= copiedChars;
            }
        }

        public IEnumerable<ArraySegment<byte>> GetByteStream(string content)
        {
            int offset = 0;
            int remainingChars = content.Length;
            var chars = CharBuffer;
            var bytes = ByteBuffer;
            while (remainingChars > 0)
            {
                var copiedChars = Math.Min(remainingChars, chars.Length);
                content.CopyTo(offset, chars, 0, copiedChars);
                var byteLength = Encoding.UTF8.GetBytes(chars, 0, copiedChars, bytes, 0);
                yield return new ArraySegment<byte>(bytes, 0, byteLength);
                offset += copiedChars;
                remainingChars -= copiedChars;
            }
        }
    }
}
