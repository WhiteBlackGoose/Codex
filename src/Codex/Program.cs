using System;

namespace Codex.Application
{
    public class Program
    {
        public static void Main(params string[] args)
        {
            if (Environment.GetEnvironmentVariable("CodexDebugOnStart") == "1")
            {
                System.Diagnostics.Debugger.Launch();
            }

            new CodexApplication().Run(args);
        }
    }
}
