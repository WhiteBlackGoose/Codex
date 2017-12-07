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
            // TODO: This should be configurable through build properties somehow
            CodexProvider.Instance = new WebApiCodex("http://localhost:9491/api/codex/");
        }
    }
}
