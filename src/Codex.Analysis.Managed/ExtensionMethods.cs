using Codex.Utilities;
using Microsoft.CodeAnalysis;
using System;
using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Codex.Analysis
{
    public static class ExtensionMethods
    {
        public static string GetDisplayString(this ISymbol symbol)
        {
            var specialType = symbol.As<ITypeSymbol>()?.SpecialType;
            bool isSpecialType = specialType.GetValueOrDefault(SpecialType.None) != SpecialType.None;

            return isSpecialType ? symbol.ToDisplayString(DisplayFormats.QualifiedNameDisplayFormat) : symbol.ToDisplayString(DisplayFormats.GetDisplayFormat(symbol.Language));
        }

        public static bool IsMemberSymbol(this ISymbol symbol)
        {
            switch (symbol.Kind)
            {
                case SymbolKind.Method:
                case SymbolKind.Event:
                case SymbolKind.Field:
                case SymbolKind.Property:
                    return true;
            }

            return false;
        }

        public static bool IsEquivalentKind(this SyntaxToken node, CS.SyntaxKind kind)
        {
            int rawKind = (int)kind;
            if (node.Language == LanguageNames.VisualBasic)
            {
                rawKind = (int)GetVBSyntaxKind(kind);
            }

            return node.RawKind == rawKind;
        }

        public static bool IsEquivalentKind(this SyntaxNode node, CS.SyntaxKind kind)
        {
            if (node == null)
            {
                return false;
            }

            int rawKind = (int)kind;
            if (node.Language == LanguageNames.VisualBasic)
            {
                rawKind = (int)GetVBSyntaxKind(kind);
            }

            return node.RawKind == rawKind;
        }

        public static VB.SyntaxKind GetVBSyntaxKind(this CS.SyntaxKind kind)
        {
            switch (kind)
            {
                case CS.SyntaxKind.SimpleMemberAccessExpression:
                    return VB.SyntaxKind.SimpleMemberAccessExpression;
                case CS.SyntaxKind.OverrideKeyword:
                    return VB.SyntaxKind.OverridesKeyword;
                case CS.SyntaxKind.NewKeyword:
                    return VB.SyntaxKind.NewKeyword;
                default:
                    throw new ArgumentException($"Can't convert {kind} to VB Syntax Kind");
            }
        }


        public static int GetSymbolDepth(this ISymbol symbol)
        {
            ISymbol current = symbol.ContainingSymbol;
            int depth = 0;
            while (current != null)
            {
                var namespaceSymbol = current as INamespaceSymbol;
                if (namespaceSymbol != null)
                {
                    // if we've reached the global namespace, we're already at the top; bail
                    if (namespaceSymbol.IsGlobalNamespace)
                    {
                        break;
                    }
                }
                else
                {
                    // we don't want namespaces to add to our "depth" because they won't be displayed in the tree
                    depth++;
                }

                current = current.ContainingSymbol;
            }

            return depth;
        }
    }
}
