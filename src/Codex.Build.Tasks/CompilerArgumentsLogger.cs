using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Codex.Build.Tasks.CompilerArgumentsUtilities;

namespace Codex.Build.Tasks
{
    public class CompilerArgumentsLogger : IForwardingLogger
    {
        public LoggerVerbosity Verbosity { get; set; }
        public string Parameters { get; set; }
        public IEventRedirector BuildEventRedirector { get; set; }
        public int NodeId { get; set; }

        public string LogDirectory { get; set; }
        private int _id;

        public void Initialize(IEventSource eventSource)
        {
            ParseParameters();
            Console.WriteLine(Parameters);
            Console.WriteLine(LogDirectory);
            Directory.CreateDirectory(LogDirectory);
            eventSource.MessageRaised += OnMessageRaised;
        }

        public void Initialize(IEventSource eventSource, int nodeCount)
        {
            Initialize(eventSource);
        }

        private void ParseParameters()
        {
            var args = Parameters.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim());
            foreach (string arg in args)
            {
                string argValue;
                if (MatchArg(arg, "LogDirectory", out argValue))
                {
                    LogDirectory = argValue;
                }
                else
                {
                    throw new ArgumentException("Invalid Argument: " + arg);
                }
            }
        }

        private static bool MatchArg(string arg, string argName, out string argValue)
        {
            if (arg.StartsWith($"{argName}=", StringComparison.OrdinalIgnoreCase))
            {
                argValue = arg.Substring(argName.Length + 1);
                return true;
            }
            argValue = null;
            return false;
        }

        private void OnMessageRaised(object sender, BuildMessageEventArgs e)
        {
            if (e is TaskCommandLineEventArgs taskArgs)
            {
                string commandLine = GetCommandLineFromEventArgs(e, out var kind);
                if (commandLine != null)
                {
                    var id = Interlocked.Increment(ref _id);
                    var compiler = kind == CompilerKind.CSharp ? "csc" : "vbc";

                    var argPath = Path.Combine(LogDirectory, $"{NodeId}_{id}_{Path.GetFileNameWithoutExtension(e.ProjectFile)}.{compiler}.args.txt");
                    File.WriteAllLines(argPath,
                        new[]
                        {
                            $"{ProjectFilePrefix}{e.ProjectFile}",
                            commandLine
                        });
                }
            }
        }

        public void Shutdown()
        {
        }
    }
}
