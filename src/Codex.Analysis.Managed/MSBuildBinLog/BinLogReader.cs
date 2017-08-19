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
            var taskIdToInvocationMap = new Dictionary<int, CompilerInvocation>();

            foreach (var record in records)
            {
                var invocation = TryGetInvocationFromRecord(record, taskIdToInvocationMap);
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

        private static CompilerInvocation TryGetInvocationFromRecord(Record record, Dictionary<int, CompilerInvocation> taskIdToInvocationMap)
        {
            var args = record.Args;
            if (args == null)
            {
                return null;
            }

            var taskId = args.BuildEventContext?.TaskId ?? 0;

            if (args is TaskStartedEventArgs taskStarted && (taskStarted.TaskName == "Csc" || taskStarted.TaskName == "Vbc"))
            {
                var invocation = new CompilerInvocation();
                taskIdToInvocationMap[taskId] = invocation;
                invocation.ProjectFile = taskStarted.ProjectFile;
                return null;
            }

            var task = args as TaskCommandLineEventArgs;
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

            if (taskIdToInvocationMap.TryGetValue(taskId, out var compilerInvocation))
            {
                compilerInvocation.Language = language;
                compilerInvocation.CommandLine = commandLine;
                taskIdToInvocationMap.Remove(taskId);
            }

            return compilerInvocation;
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

        private static CompilerInvocation TryGetInvocationFromTask(Task task)
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
                CommandLine = commandLine,
                ProjectFile = task.GetNearestParent<Microsoft.Build.Logging.StructuredLogger.Project>()?.ProjectFile
            };
        }
    }
}
