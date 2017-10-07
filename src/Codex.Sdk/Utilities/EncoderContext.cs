using System.IO;
using System.Text;

namespace Codex.Sdk.Utilities
{
    public class EncoderContext
    {
        public readonly StringBuilder StringBuilder = new StringBuilder();
        public readonly byte[] ByteBuffer = new byte[Encoding.UTF8.GetMaxByteCount(1024)];
        public readonly char[] CharBuffer = new char[1024];
        public readonly StringWriter Writer;

        public EncoderContext()
        {
            Writer = new StringWriter(StringBuilder);
        }
    }
}
