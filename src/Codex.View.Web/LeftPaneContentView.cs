using System;
using System.Windows;
using System.Windows.Controls;
using Bridge.Html5;
using System.Windows.Media;
using Granular.Host.Render;

namespace Codex.View
{
    public partial class LeftPaneContentView : FrameworkElement, IHtmlRenderElementHost
    {
        private HTMLElement m_htmlElement;
        private HTMLDivElement m_childrenHost;

        public LeftPaneContent Content
        {
            get { return (LeftPaneContent)GetValue(ContentProperty); }
            set { SetValue(ContentProperty, value); }
        }

        public static readonly DependencyProperty ContentProperty = ViewUtilities.RegisterDependencyProperty<LeftPaneContentView, LeftPaneContent>("ContentProperty");

        public LeftPaneContentView()
        {
            VerticalAlignment = VerticalAlignment.Stretch;
            this.VisualIsHitTestVisible = true;
            VisualBackground = Brushes.Transparent;
        }

        public void SetRenderElement(HTMLElement htmlElement)
        {
            m_htmlElement = htmlElement;
            if (m_childrenHost != null)
            {
                m_htmlElement.AppendChild(m_childrenHost);
            }
        }

        public void RenderContent(LeftPaneView view, LeftPaneContent viewModel)
        {
            ViewUtilities.RenderQueue.InvokeAsync(() =>
            {
                var oldChildrenHost = m_childrenHost;
                var newChildrenHost = new HTMLDivElement();
                m_childrenHost = newChildrenHost;
                viewModel?.Render(view, m_childrenHost);
                //m_htmlElement?.AppendChild(m_childrenHost);

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
