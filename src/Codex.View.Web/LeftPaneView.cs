using Granular.Presentation.Web;
using System;
using System.Windows;
using System.Windows.Controls;
using Bridge.Html5;
using System.Windows.Media;

namespace Codex.View
{
    public partial class LeftPaneView : IHtmlElementHost
    {
        private HTMLElement m_htmlElement;
        
        public void SetRenderElement(HTMLElement htmlElement)
        {
            m_htmlElement = htmlElement;
            //htmlElement.Style.PointerEvents = PointerEvents.Auto;
            htmlElement.Style.OverflowX = Overflow.Auto;
            htmlElement.Style.OverflowY = Overflow.Auto;
        }
    }
}
