extern alias binlog;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Logging.StructuredLogger;
using Microsoft.CodeAnalysis;
using BinaryLogReplayEventSource = binlog::Microsoft.Build.Logging.BinaryLogReplayEventSource;

namespace Codex.Analysis.Managed
{

    public class BinLogReader
    {
        /// <summary>
        /// Binlog reader does not handle concurrent accesses appropriately so handle it here
        /// </summary>
        private static readonly ConcurrentDictionary<string, Lazy<List<CompilerInvocation>>> m_binlogInvocationMap
            = new ConcurrentDictionary<string, Lazy<List<CompilerInvocation>>>(StringComparer.OrdinalIgnoreCase);

        public static IEnumerable<CompilerInvocation> ExtractInvocations(string binLogFilePath)
        {
            // Normalize the path
            binLogFilePath = Path.GetFullPath(binLogFilePath);

            if (!File.Exists(binLogFilePath))
            {
                throw new FileNotFoundException(binLogFilePath);
            }

            var lazyResult = m_binlogInvocationMap.GetOrAdd(binLogFilePath, new Lazy<List<CompilerInvocation>>(() =>
            {
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
            }));

            var result = lazyResult.Value;

            // Remove the lazy now that the operation has completed
            m_binlogInvocationMap.TryRemove(binLogFilePath, out var ignored);

            return result;
        }

        private static List<CompilerInvocation> ExtractInvocationsFromBuild(string logFilePath)
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

            TaskStartedEventArgs taskStarted = args as TaskStartedEventArgs;
            if (taskStarted != null && (taskStarted.TaskName == "Csc" || taskStarted.TaskName == "Vbc"))
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

            CompilerInvocation compilerInvocation;
            if (taskIdToInvocationMap.TryGetValue(taskId, out compilerInvocation))
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
