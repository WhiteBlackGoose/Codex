using Bridge.Html5;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.View.Web
{
    public interface IHtmlContent
    {
        void Render(HTMLElement parentElement, RenderContext context);
    }
}
