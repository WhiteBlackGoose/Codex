using Codex.View;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.Toolkit.Uwp.UI.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
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

        public static PanelChild<Grid> Column(int column, PanelChild<Grid> element)
        {
            Grid.SetColumn(element.ChildElement, column);
            return element;
        }

        public static PanelChild<Grid> Row(int row, PanelChild<Grid> element)
        {
            Grid.SetRow(element.ChildElement, row);
            return element;
        }

        public static PanelChild<Grid> Column (GridLengthEx width, FrameworkElement element = null, GridExtent? extent = default)
        {
            return new GridChild(element)
            {
                ColumnDefinition = new ColumnDefinition() { Width = width }
            };
        }

        public static PanelChild<Grid> Row(GridLengthEx height, FrameworkElement element = null, GridExtent? extent = default)
        {
            return new GridChild(element)
            {
                RowDefinition = new RowDefinition() { Height = height }
            };
        }

        public static FrameworkElement Text(params object[] segments)
        {
            var tb = new TextBlock()
            {
                Foreground = B(Colors.Black)
            };

            var tip = new TextBlockToolTip(tb);
            bool hasTooltip = false;

            foreach (var segment in segments)
            {
                if (segment is string s)
                {
                    tb.Inlines.Add(new Run()
                    {
                        Text = s
                    });

                    tip.ToolTips.Add((default, s));
                }
                else if (segment is LinkEx l)
                {
                    Hyperlink item = new Hyperlink()
                    {
                        Inlines =
                        {
                            new Run()
                            {
                                Text = l.Text
                            }
                        },
                        NavigateUri = new Uri(l.Url)
                    };

                    if (l.Tooltip != null)
                    {
                        hasTooltip = true;
                    }

                    tb.Inlines.Add(item);
                    tip.ToolTips.Add((default, l.Text));
                }
            }

            if (hasTooltip)
            {
                //ToolTipService.SetToolTip(tb, tip);
            }

            return tb;
        }

        public class TextBlockToolTip : ToolTip
        {
            public List<(Rect bounds, string text)> ToolTips { get; } = new List<(Rect bounds, string text)>();
            private TextBlock Block { get; }

            public TextBlockToolTip(TextBlock block)
            {
                Opened += OnOpened;
                Block = block;
            }

            private void OnOpened(object sender, RoutedEventArgs e)
            {
                //var p = CoreWindow.GetForCurrentThread().PointerPosition;

                //foreach (var toolTip in ToolTips)
                //{
                //    start.GetCharacterRect
                //    if (toolTip.bounds.Contains(p))
                //    {
                //        Content = toolTip.text;
                //        break;
                //    }
                //}
            }
        }

        public static LinkEx Link(string text, string url, string tooltip = null)
        {
            //try
            //{
            //    var uri = new Uri(url);
            //    Console.WriteLine($"Success: Text={text}, Url={url}");
            //}
            //catch
            //{
            //    Console.WriteLine($"Fail: Text={text}, Url={url}");
            //}
            return new LinkEx(text, url, tooltip);
        }

        public static FrameworkElement Tip(FrameworkElement element, string tooltip)
        {
            ToolTipService.SetToolTip(element, tooltip);
            Placeholder.Todo("Create tooltip");
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

        public static GridLengthEx Star { get; } = new GridLengthEx() { Type = GridUnitType.Star, Value = 1 };
        public static GridLengthEx Auto { get; } = new GridLengthEx() { Type = GridUnitType.Auto, Value = 1 };
    }

    public struct GridExtent
    {
        public int? Column { get; set; }
        public int? ColumnSpan { get; set; }
        public int? Row { get; set; }
        public int? RowSpan { get; set; }

        public void Apply(FrameworkElement element)
        {
            if (Column != null) Grid.SetColumn(element, Column.Value);
            if (ColumnSpan != null) Grid.SetColumnSpan(element, ColumnSpan.Value);
            if (Row != null) Grid.SetRow(element, Row.Value);
            if (RowSpan != null) Grid.SetRowSpan(element, RowSpan.Value);
        }
    }

    public record LinkEx(string Text, string Url, string Tooltip)
    {
    }


    public struct GridLengthEx
    {
        public double Value { get; set; }
        public GridUnitType Type { get; set; }

        public static GridLengthEx operator *(GridLengthEx l, double factor)
        {
            l.Value *= factor;
            return l;
        }

        public static implicit operator GridLength(GridLengthEx l)
        {
            if (l.Type == GridUnitType.Auto)
            {
                return GridLength.Auto;
            }

            return new GridLength(l.Value, l.Type);
        }

        public static implicit operator GridLengthEx(double value)
        {
            return new GridLengthEx() { Type = GridUnitType.Pixel, Value = 1 };
        }
    }

    public class GridChild : PanelChild<Grid>
    {
        public RowDefinition RowDefinition { get; set; }
        public ColumnDefinition ColumnDefinition { get; set; }
        public GridExtent Extent { get; set; }

        public GridChild(FrameworkElement childElement)
            : base(childElement)
        {
        }

        public override void AddToPanel(Grid panel)
        {
            var extent = Extent;
            if (RowDefinition != null)
            {
                extent.Row = panel.RowDefinitions.Count;
                panel.RowDefinitions.Add(RowDefinition);
            }

            if (ColumnDefinition != null)
            {
                extent.Column = panel.ColumnDefinitions.Count;
                panel.ColumnDefinitions.Add(ColumnDefinition);
            }

            if (ChildElement != null)
            {
                extent.Apply(ChildElement);
            }

            base.AddToPanel(panel);
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
            if (ChildElement != null)
            {
                panel.Children.Add(ChildElement);
            }
        }

        public static implicit operator PanelChild<TPanel>(FrameworkElement childElement)
        {
            return new PanelChild<TPanel>(childElement);
        }
    }
}
