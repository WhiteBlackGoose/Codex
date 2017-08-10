using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;

namespace Codex.Analysis.Managed
{
    public class BinLogReader
    {
        public static IEnumerable<CompilerInvocation> ExtractInvocations(string binLogFilePath)
        {
            if (!File.Exists(binLogFilePath))
            {
                throw new FileNotFoundException(binLogFilePath);
            }

            if (binLogFilePath.EndsWith(".buildlog", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractInvocationsFromBuild(binLogFilePath);
            }

            var invocations = new List<CompilerInvocation>();
            var reader = new BinaryLogReplayEventSource();
            var records = reader.ReadRecords(binLogFilePath);
            foreach (var record in records)
            {
                var invocation = TryGetInvocationFromRecord(record);
                if (invocation != null)
                {
                    invocations.Add(invocation);
                }
            }

            return invocations;
        }

        private static IEnumerable<CompilerInvocation> ExtractInvocationsFromBuild(string logFilePath)
        {
            var build = Serialization.Read(logFilePath);
            var invocations = new List<CompilerInvocation>();
            build.VisitAllChildren<Task>(t =>
            {
                var invocation = TryGetInvocationFromTask(t);
                if (invocation != null)
                {
                    invocations.Add(invocation);
                }
            });

            return invocations;
        }

        private static CompilerInvocation TryGetInvocationFromRecord(Record record)
        {
            var task = record.Args as TaskCommandLineEventArgs;
            if (task == null)
            {
                return null;
            }

            var name = task.TaskName;
            if (name != "Csc" && name != "Vbc")
            {
                return null;
            }

            var language = name == "Csc" ? LanguageNames.CSharp : LanguageNames.VisualBasic;
            var commandLine = task.CommandLine;
            commandLine = TrimCompilerExeFromCommandLine(commandLine, language);
            return new CompilerInvocation
            {
                Language = language,
                CommandLine = commandLine
            };
        }

        private static string TrimCompilerExeFromCommandLine(string commandLine, string language)
        {
            int occurrence = -1;
            if (language == LanguageNames.CSharp)
            {
                occurrence = commandLine.IndexOf("csc.exe ", StringComparison.OrdinalIgnoreCase);
            }
            else if (language == LanguageNames.VisualBasic)
            {
                occurrence = commandLine.IndexOf("vbc.exe ", StringComparison.OrdinalIgnoreCase);
            }

            if (occurrence > -1)
            {
                commandLine = commandLine.Substring(occurrence + "csc.exe ".Length);
            }

            return commandLine;
        }

        private static CompilerInvocation TryGetInvocationFromTask(Microsoft.Build.Logging.StructuredLogger.Task task)
        {
            var name = task.Name;
            if (name != "Csc" && name != "Vbc")
            {
                return null;
            }

            var language = name == "Csc" ? LanguageNames.CSharp : LanguageNames.VisualBasic;
            var commandLine = task.CommandLineArguments;
            commandLine = TrimCompilerExeFromCommandLine(commandLine, language);
            return new CompilerInvocation
            {
                Language = language,
                CommandLine = commandLine
            };
        }
    }
}
