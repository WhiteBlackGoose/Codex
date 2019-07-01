using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codex.Storage
{
    public class Program
    {
        public static void Main(params string[] args)
        {
            if (Environment.GetEnvironmentVariable("CodexDebugOnStart") == "1")
            {
                System.Diagnostics.Debugger.Launch();
            }

            new CodexStorageApplication().Run(args);
        }
    }
}
