using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Utilities;
using System;
using System.Diagnostics;

namespace Codex.Build.Tasks
{
    /// <summary>
    /// The purpose of this task is to extract information necessary to get project imports (may just need to get out global properties and potentially env vars)
    /// </summary>
    public class GetImportsTask : Task
    {
        public override bool Execute()
        {
            Debugger.Launch();
            return true;
        }
    }
}
