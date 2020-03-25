using Granular.Host.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

[assembly: System.Windows.ApplicationHost(typeof(Granular.Host.Wpf.WpfApplicationHost))]

namespace Codex.View.Wpf.Compatibility
{
    class Reference
    {
        // Reference host project in order to ensure it included
    }
}
