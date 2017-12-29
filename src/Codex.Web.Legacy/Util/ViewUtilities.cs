using Codex.ObjectModel;
using System;
using System.Collections.Generic;

namespace Codex.Web
{
    public static partial class ViewUtilities
    {
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
