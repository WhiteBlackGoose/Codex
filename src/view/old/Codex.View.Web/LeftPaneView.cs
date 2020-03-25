using System;
using System.Windows;
using System.Windows.Controls;
using Bridge.Html5;
using System.Windows.Media;
using Granular.Host.Render;
using Granular.Extensions;

namespace Codex.View
{
    public partial class LeftPaneView : IHtmlRenderElementHost
    {
        private HTMLElement m_htmlElement;
        
        public void SetRenderElement(HTMLElement htmlElement)
        {
            m_htmlElement = htmlElement;
            VisualBackground = Brushes.Transparent;
            VisualIsHitTestVisible = true;
            htmlElement.Style.OverflowX = Overflow.Auto;
            htmlElement.Style.OverflowY = Overflow.Auto;
        }

        private static Overflow GetOverflow(double available, int actual)
        {
            if (available.IsClose(actual))
            {
                return Overflow.Hidden;
            }
            else
            {
                return Overflow.Auto;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var htmlElement = m_htmlElement;
            if (htmlElement != null && htmlElement.ClientWidth != 0)
            {
                ViewUtilities.RenderQueue.InvokeAsync(() =>
                {
                    htmlElement.Style.OverflowX = GetOverflow(finalSize.Width, htmlElement.ScrollWidth);
                    htmlElement.Style.OverflowY = GetOverflow(finalSize.Height, htmlElement.ScrollHeight);
                });
            }

            return base.ArrangeOverride(finalSize);
        }
    }
}
