using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.ObjectModel
{
    public partial class Repository
    {
        public Repository(string name)
        {
            Contract.Requires(name != null);
            Name = name;
        }
    }
}
