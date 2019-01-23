using System.IO;
using System.Text;

namespace Codex.Sdk.Utilities
{
    public class EncoderContext
    {
        public readonly StringBuilder StringBuilder = new StringBuilder();
        public readonly byte[] ByteBuffer;
        public readonly char[] CharBuffer;
        public readonly StringWriter Writer;

        public EncoderContext(int charBufferSize = 1024)
        {
            Writer = new StringWriter(StringBuilder);
            CharBuffer = new char[charBufferSize];
            ByteBuffer = new byte[Encoding.UTF8.GetMaxByteCount(charBufferSize)];
        }
    }
}
