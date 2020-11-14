using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Text;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Codex.Uno.Shared
{
    public static class ViewBuilder
    {
        public static TPanel WithChildren<TPanel>(this TPanel panel, params PanelChild<TPanel>[] children)
            where TPanel : Panel
        {
            foreach (var child in children)
            {
                child.AddToPanel(panel);
            }

            return panel;
        }

        public static ImageSource RelativeImageSource(string path)
        {
            throw new NotImplementedException();
        }

        public static Border WithChild(this Border border, FrameworkElement element)
        {
            border.Child = element;
            return border;
        }

        public static PanelChild<DockPanel> Top(FrameworkElement element)
        {
            DockPanel.SetDock(element, Dock.Top);
            return element;
        }

        public static Color Color(int value)
        {
            throw new NotImplementedException();
        }

        public static Brush B(Color color)
        {
            return new SolidColorBrush(color);
        }
    }

    public class PanelChild<TPanel>
        where TPanel : Panel
    {
        public FrameworkElement ChildElement { get; }

        public PanelChild(FrameworkElement childElement)
        {
            ChildElement = childElement;
        }

        public virtual void AddToPanel(TPanel panel)
        {
            panel.Children.Add(panel);
        }

        public static implicit operator PanelChild<TPanel>(FrameworkElement childElement)
        {
            return new PanelChild<TPanel>(childElement);
        }
    }
}
