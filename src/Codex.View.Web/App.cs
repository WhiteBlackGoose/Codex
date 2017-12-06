using Codex.View.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.View
{
    partial class App
    {
        public App()
        {
            CodexProvider.Instance = new WebApiCodex("http://localhost:9491/api/codex/");
        }
    }
}
