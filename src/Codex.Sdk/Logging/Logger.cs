using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Logging
{
    public abstract class Logger : IDisposable
    {
        public static readonly Logger Null = new NullLogger();

        public virtual void LogError(string error)
        {
            LogMessage(error);
        }

        public virtual void LogExceptionError(string operation, Exception ex)
        {
            LogError($"Operation: {operation}{Environment.NewLine}{ex.ToString()}");
        }

        public virtual void LogWarning(string warning)
        {
            LogMessage(warning);
        }

        public abstract void LogMessage(string message, MessageKind kind = MessageKind.Informational);

        public void WriteLine(string message)
        {
            LogMessage(message);
        }

        public virtual void Dispose()
        {
        }

        private class NullLogger : Logger
        {
            public override void LogMessage(string message, MessageKind kind = MessageKind.Informational)
            {
            }
        }
    }

    public enum MessageKind
    {
        Informational,
        Diagnostic,
    }
}
