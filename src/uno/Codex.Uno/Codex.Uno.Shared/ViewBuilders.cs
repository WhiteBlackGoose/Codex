using Codex.View;
using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

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
            return new BitmapImage(new Uri(path, UriKind.Relative));
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

        public static PanelChild<Grid> Column(int column, FrameworkElement element)
        {
            Grid.SetColumn(element, column);
            return element;
        }

        public static PanelChild<Grid> Row(int row, FrameworkElement element)
        {
            Grid.SetRow(element, row);
            return element;
        }

        public static Color C(int value)
        {
            unchecked
            {
                return Color.FromArgb(
                    byte.MaxValue,
                    (byte)(value >> 16),
                    (byte)(value >> 8),
                    (byte)value);
            }
        }

        public static Brush B(Color color)
        {
            return new SolidColorBrush(color);
        }

        public static Brush B(int color)
        {
            return B(C(color));
        }

        public static FontFamily F(string name)
        {
            return new FontFamily(name);
        }

        public static Border WithMargin(this UIElement element, Thickness margin)
        {
            return new Border()
            {
                Margin = margin,
                Child = element
            };
        }

        public static FontFamily Consolas = F("Consolas");

        public static Button OnExecute(this Button b, Action action)
        {
            b.Click += (s, e) => action();
            return b;
        }

        public static FrameworkElement BindContent<T>(Bound<T> bound, Func<T, UIElement> onUpdate)
        {
            Console.WriteLine($"TESTING: {bound.Value}");
            return new ContentPresenter()
                .Bind(bound, (control, value) => control.Content = value == null ? null : onUpdate(value));
        }

        public static ItemsControl Add(this ItemsControl control, IEnumerable<UIElement> items)
        {
            foreach (var item in items)
            {
                control.Items.Add(item);
            }

            return control;
        }

        public static TElement HideIfNull<TElement, TValue>(this TElement element, Bound<TValue> bound)
            where TElement : UIElement
        {
            element.Bind(bound, (e, value) => e.Visibility = value != null ? Visibility.Visible : Visibility.Collapsed);
            return element;
        }

        public static T Bind<T, TValue>(this T element, Bound<TValue> bound, Action<T, TValue> onUpdate)
        {
            bound.OnUpdate(value => onUpdate(element, value));
            onUpdate(element, bound.Value);
            return element;
        }

        public static T Bind<T>(this T element, Func<T, ValueBinding> binding)
        {
            return element;
        }

        public static TextBox OnTextChanged(this TextBox textBox, Action<string> textChanged)
        {
            textBox.TextChanged += (_, e) =>
            {
                textChanged(textBox.Text);
            };

            return textBox;
        }
    }

    public class GridExtent
    {
        public int Column { get; set; }
        public int ColumnSpan { get; set; }
        public int Row { get; set; }
        public int RowSpan { get; set; }
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
            panel.Children.Add(ChildElement);
        }

        public static implicit operator PanelChild<TPanel>(FrameworkElement childElement)
        {
            return new PanelChild<TPanel>(childElement);
        }
    }
}
