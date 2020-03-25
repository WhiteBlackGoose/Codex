using System;
using System.Windows;
using System.Windows.Controls;
using Bridge.Html5;
using System.Windows.Media;
using Granular.Host.Render;
using Codex.View.Web;

namespace Codex.View
{
    public partial class HtmlContentView : FrameworkElement, IHtmlRenderElementHost
    {
        private HTMLElement m_htmlElement;
        private HTMLDivElement m_childrenHost;

        public IHtmlContent Content
        {
            get { return (IHtmlContent)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty = ViewUtilities.RegisterDependencyProperty<HtmlContentView, IHtmlContent>(
            "ContentProperty",
            (view, content) => view.OnContentChanged(content));

        public HtmlContentView()
        {
            VerticalAlignment = VerticalAlignment.Stretch;
            this.VisualIsHitTestVisible = true;
            VisualBackground = Brushes.Transparent;
        }

        public void SetRenderElement(HTMLElement htmlElement)
        {
            m_htmlElement = htmlElement;
            htmlElement.Style.OverflowX = Overflow.Auto;
            htmlElement.Style.OverflowY = Overflow.Auto;

            if (m_childrenHost != null)
            {
                m_htmlElement.AppendChild(m_childrenHost);
            }
        }

        private void OnContentChanged(IHtmlContent content)
        {
            ViewUtilities.RenderQueue.InvokeAsync(() =>
            {
                var oldChildrenHost = m_childrenHost;
                var newChildrenHost = new HTMLDivElement();
                m_childrenHost = newChildrenHost;
                content?.Render(newChildrenHost, new RenderContext(this));

                if (oldChildrenHost != null)
                {
                    m_htmlElement?.ReplaceChild(newChildrenHost, oldChildrenHost);
                }
                else
                {
                    m_htmlElement?.AppendChild(newChildrenHost);
                }
            });
        }
    }
}
