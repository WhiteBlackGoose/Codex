using System.Windows;
using Bridge;
using Bridge.Html5;
using Granular.Presentation.Web;
using Monaco;
using System.Windows.Media;
using static monaco.editor;
using System.Windows.Threading;
using Granular.Host;

namespace Codex.View
{
    public partial class EditorHostControl : FrameworkElement, IHtmlElementHost
    {
        private HTMLElement m_htmlElement;
        private IStandaloneCodeEditor m_editor;
        private RenderQueue renderQueue = new RenderQueue();

        public async void SetRenderElement(HTMLElement htmlElement)
        {
            m_htmlElement = htmlElement;
            m_editor = await Editor.Create(htmlElement, new EditorConstructionOptions()
            {
                value = "Hello World",
                language = "text",
                readOnly = true
            });

            this.VisualIsHitTestVisible = true;
            VisualBackground = Brushes.Transparent;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var currentSize = VisualBounds.Size;
            finalSize = base.ArrangeOverride(finalSize);

            if (!currentSize.IsClose(finalSize))
            {
                if (m_editor != null)
                {
                    renderQueue.InvokeAsync(() =>
                    {
                        m_editor.layout();
                    });
                }
            }

            return finalSize;
        }
    }
}
