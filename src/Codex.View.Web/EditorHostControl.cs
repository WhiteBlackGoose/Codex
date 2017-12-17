using System.Windows;
using Bridge;
using Bridge.Html5;
using Granular.Presentation.Web;
using Monaco;
using System.Windows.Media;
using static monaco.editor;

namespace Codex.View
{
    public partial class EditorHostControl : FrameworkElement, IHtmlElementHost
    {
        private HTMLElement m_htmlElement;
        private IStandaloneCodeEditor m_editor;
        //private List<HTMLElement> m_arrangedElements = new List<HTMLElement>();

        public async void SetRenderElement(HTMLElement htmlElement)
        {
            m_htmlElement = htmlElement;
            //m_htmlElement.TextContent = "Hello World";
            m_editor = await Editor.Create(htmlElement, "Hello World");

            this.VisualIsHitTestVisible = true;
            VisualBackground = Brushes.DeepPink;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var currentSize = VisualBounds.Size;
            finalSize = base.ArrangeOverride(finalSize);

            if (!currentSize.IsClose(finalSize))
            {
                m_editor?.layout();
            }

            return finalSize;
        }
    }
}
