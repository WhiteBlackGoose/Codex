using System.Windows;
using Bridge;
using Bridge.Html5;
using Granular.Presentation.Web;

namespace Codex.View
{
    public partial class EditorHostControl : FrameworkElement, IHtmlElementHost
    {
        private HTMLElement m_htmlElement;
        //private List<HTMLElement> m_arrangedElements = new List<HTMLElement>();

        public void SetRenderElement(HTMLElement htmlElement)
        {
            m_htmlElement = htmlElement;
            m_htmlElement.TextContent = "Hello World";
        }
    }
}
