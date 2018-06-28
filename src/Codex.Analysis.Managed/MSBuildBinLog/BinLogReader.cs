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
                var taskIdToInvocationMap = new Dictionary<(int, int), CompilerInvocation>();

                void TryGetInvocationFromEvent(object sender, BuildEventArgs args)
                {
                    var invocation = TryGetInvocationFromRecord(args, taskIdToInvocationMap);
                    if (invocation != null)
                    {
                        invocations.Add(invocation);
                    }
                }

                reader.TargetStarted += TryGetInvocationFromEvent;
                reader.MessageRaised += TryGetInvocationFromEvent;

                reader.Replay(binLogFilePath);

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

        private static CompilerInvocation TryGetInvocationFromRecord(BuildEventArgs args, Dictionary<(int, int), CompilerInvocation> taskIdToInvocationMap)
        {
            int targetId = args.BuildEventContext?.TargetId ?? -1;
            int projectId = args.BuildEventContext?.ProjectInstanceId ?? -1;
            if (targetId < 0)
            {
                return null;
            }

            var targetStarted = args as TargetStartedEventArgs;
            if (targetStarted != null && targetStarted.TargetName == "CoreCompile")
            {
                var invocation = new CompilerInvocation();
                taskIdToInvocationMap[(targetId, projectId)] = invocation;
                invocation.ProjectFile = targetStarted.ProjectFile;
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
            if (taskIdToInvocationMap.TryGetValue((targetId, projectId), out compilerInvocation))
            {
                compilerInvocation.Language = language;
                compilerInvocation.CommandLine = commandLine;
                taskIdToInvocationMap.Remove((targetId, projectId));
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
            if (name != "Csc" && name != "Vbc" || ((task.Parent as Target)?.Name != "CoreCompile"))
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
