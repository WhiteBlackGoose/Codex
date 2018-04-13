using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codex.Logging
{
    public class TextLogger : Logger
    {
        private readonly TextWriter writer;
        private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private int reservation;
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();

        public TextLogger(TextWriter writer)
        {
            this.writer = writer;
        }

        public override void LogError(string error)
        {
            WriteLineCore($"ERROR: {error}");
        }

        public override void LogWarning(string warning)
        {
            WriteLineCore($"WARNING: {warning}");
        }

        public override void LogMessage(string message, MessageKind kind)
        {
            WriteLineCore(message);
        }

        protected virtual void WriteLineCore(string text)
        {
            text = $"[{stopwatch.Elapsed.ToString(@"hh\:mm\:ss")}]: {text}";
            messages.Enqueue(text);

            FlushMessages();
        }

        private void FlushMessages()
        {
            if (Interlocked.CompareExchange(ref reservation, 1, 0) == 0)
            {
                int count = 0;
                while (count < 10 && messages.TryDequeue(out var m))
                {
                    writer.WriteLine(m);
                    count++;
                }

                Volatile.Write(ref reservation, 0);
            }
        }

        public override void Dispose()
        {
            FlushMessages();
        }
    }
}
