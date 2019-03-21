using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Build.Tasks
{
    public class CompilerArgumentsUtilities
    {
        public const string ProjectFilePrefix = "Project=";

        public enum CompilerKind
        {
            CSharp,
            VisualBasic
        }

        public static string GetCommandLineFromEventArgs(BuildEventArgs args, out CompilerKind language)
        {
            var task = args as TaskCommandLineEventArgs;
            language = default;
            if (task == null)
            {
                return null;
            }

            var name = task.TaskName;
            if (name != "Csc" && name != "Vbc")
            {
                return null;
            }

            language = name == "Csc" ? CompilerKind.CSharp : CompilerKind.VisualBasic;
            var commandLine = task.CommandLine;
            commandLine = TrimCompilerExeFromCommandLine(commandLine, language);
            return commandLine;
        }

        public static string TrimCompilerExeFromCommandLine(string commandLine, CompilerKind language)
        {
            int occurrence = -1;
            if (language == CompilerKind.CSharp)
            {
                occurrence = commandLine.IndexOf("csc.exe ", StringComparison.OrdinalIgnoreCase);
            }
            else if (language == CompilerKind.VisualBasic)
            {
                occurrence = commandLine.IndexOf("vbc.exe ", StringComparison.OrdinalIgnoreCase);
            }

            if (occurrence > -1)
            {
                commandLine = commandLine.Substring(occurrence + "csc.exe ".Length);
            }

            return commandLine;
        }
    }
}
