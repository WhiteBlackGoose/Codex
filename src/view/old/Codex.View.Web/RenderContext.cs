using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Codex.View.Web
{
    public class RenderContext
    {
        public readonly UIElement Container;

        public RenderContext(UIElement container)
        {
            Container = container;
        }
    }
}
