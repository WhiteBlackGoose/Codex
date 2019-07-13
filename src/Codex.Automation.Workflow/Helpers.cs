using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Automation.Workflow
{
    public static class Helpers
    {
        public static T With<T>(this T item, Action<T> modification)
        {
            modification(item);
            return item;
        }

        public static void Log(string message, [CallerMemberName]string method = null)
        {
            Console.WriteLine($"{method}: {message}");
        }

        public static bool Invoke(string processExe, params string[] arguments)
        {
            var processArgs = string.Join(" ", arguments.Select(QuoteIfNecessary));
            Log($"Running: {processExe} {processArgs}");

            try
            {
                var process = Process.Start(new ProcessStartInfo(processExe, processArgs)
                {
                    UseShellExecute = false
                });

                process.WaitForExit();
                Log($"Run completed with exit code '{process.ExitTime}': {processExe} {processArgs}");

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Log(ex.ToString());
                return false;
            }
        }

        public static string QuoteIfNecessary(string arg)
        {
            return arg.Contains(" ") ? $"\"{arg}\"" : arg;
        }
    }
}
