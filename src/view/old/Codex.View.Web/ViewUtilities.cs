using Bridge.Html5;
using Codex.ObjectModel;
using Granular.Host;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace Codex.View
{
    public static partial class ViewUtilities
    {
        internal static RenderQueue RenderQueue = new RenderQueue();

        public static T WithOnClick<T>(this T element, Action action)
            where T : HTMLElement
        {
            element.OnClick = e =>
            {
                action();

                e.PreventDefault();
                e.StopPropagation();
            };


            return element;
        }

        public static T WithChild<T>(this T element, Node child)
            where T : HTMLElement
        {
            element.AppendChild(child);
            return element;
        }

        public static T WithText<T>(this T element, string text)
            where T : HTMLElement
        {
            element.TextContent = text;
            return element;
        }

        public static void ToggleExpandCollapse(MouseEvent<HTMLDivElement> arg)
        {
            var headerElement = arg.CurrentTarget;
            var collapsible = headerElement.NextElementSibling;
            if ((Display)collapsible.Style.Display == Display.None)
            {
                collapsible.Style.Display = Display.Block;
                headerElement.SetBackgroundIcon("minus");
            }
            else
            {
                collapsible.Style.Display = Display.None;
                headerElement.SetBackgroundIcon("plus");
            }

            arg.PreventDefault();
            arg.StopPropagation();
        }

        public static HTMLElement SetBackgroundIcon(this HTMLElement element, string imageName)
        {
            element.Style.BackgroundImage = GetIconPath(imageName);
            return element;
        }

        public static string GetIconPath(string imageName)
        {
            return $"content/icons/{imageName}.png";
        }

        public static HTMLDivElement RenderHeaderedContent(HTMLElement parentElement, string headerClass, string headerText, string contentClass)
        {
            // Header
            parentElement.AppendChild(new HTMLDivElement()
            {
                ClassName = headerClass,
                OnClick = ViewUtilities.ToggleExpandCollapse,
                TextContent = headerText
            });

            // Content
            var contentElement = new HTMLDivElement() { ClassName = contentClass };
            parentElement.AppendChild(contentElement);
            return contentElement;
        }

        public static bool EndsWith(this string s, string value, StringComparison comparison)
        {
            var suffixStartIndex = s.Length - value.Length;
            return s.IndexOf(value, suffixStartIndex, 1, comparison) == suffixStartIndex;
        }

        public static string GetFileNameGlyph(string fileName)
        {
            if (fileName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return "csharp";
            }
            else if (fileName.EndsWith(".vb", StringComparison.OrdinalIgnoreCase))
            {
                return "vb";
            }
            else if (fileName.EndsWith(".ts", StringComparison.OrdinalIgnoreCase))
            {
                return "TypeScript";
            }
            else if (fileName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                return "xaml";
            }

            return "212";
        }

        public static string GetGlyph(this IDefinitionSymbol s, string filePath = null)
        {
            var glyph = s.Glyph;
            if (glyph != Glyph.Unknown)
            {
                return glyph.GetGlyphNumber().ToString();
            }

            if (!string.IsNullOrEmpty(filePath))
            {
                if (string.Equals(s.Kind, nameof(SymbolKinds.File), StringComparison.OrdinalIgnoreCase))
                {
                    return GetFileNameGlyph(filePath);
                }
            }

            return "0";
        }

    }
}
