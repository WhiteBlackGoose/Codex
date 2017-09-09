using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;

namespace Codex.ObjectModel
{
    /// <summary>
    /// Represents a reference to a project
    /// </summary>
    partial class ReferencedProject
    {
        public override string ToString()
        {
            return DisplayName ?? ProjectId;
        }
    }
}