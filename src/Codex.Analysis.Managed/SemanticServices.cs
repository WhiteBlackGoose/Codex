using System;
using System.Threading;
using Codex.Utilities;
using Microsoft.CodeAnalysis;

using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;

namespace Codex.Analysis
{
    public class SemanticServices
    {
        private readonly Func<SyntaxToken, SyntaxNode> getBindableParentDelegate;
        private readonly Func<SemanticModel, SyntaxNode, CancellationToken, bool> isWrittenToDelegate;
        private readonly string language;

        public SemanticServices(Workspace workspace, string language)
        {
            this.language = language;

            var syntaxFactsService = WorkspaceHacks.GetSyntaxFactsService(workspace, language);
            var semanticFactsService = WorkspaceHacks.GetSemanticFactsService(workspace, language);

            var semanticFactsServiceType = semanticFactsService.GetType();
            var isWrittenTo = semanticFactsServiceType.GetMethod("IsWrittenTo");
            isWrittenToDelegate = (Func<SemanticModel, SyntaxNode, CancellationToken, bool>)
                Delegate.CreateDelegate(typeof(Func<SemanticModel, SyntaxNode, CancellationToken, bool>), semanticFactsService, isWrittenTo);

            var syntaxFactsServiceType = syntaxFactsService.GetType();
            var getBindableParent = syntaxFactsServiceType.GetMethod("TryGetBindableParent");
            getBindableParentDelegate = (Func<SyntaxToken, SyntaxNode>)
                Delegate.CreateDelegate(typeof(Func<SyntaxToken, SyntaxNode>), syntaxFactsService, getBindableParent);
        }

        public SyntaxNode GetBindableParent(SyntaxToken syntaxToken)
        {
            return getBindableParentDelegate(syntaxToken);
        }

        public bool IsWrittenTo(SemanticModel semanticModel, SyntaxNode syntaxNode, CancellationToken cancellationToken)
        {
            return isWrittenToDelegate(semanticModel, syntaxNode, cancellationToken);
        }

        /// <summary>
        /// Determines which tokens can possibly be semantic. Currently, implemented by exclusion,
        /// (i.e. return true for all token kinds except those known not to be semantic)
        /// </summary>
        public bool IsPossibleSemanticToken(SyntaxToken token)
        {
            if (language == LanguageNames.CSharp)
            {
                switch ((CS.SyntaxKind)token.RawKind)
                {
                    case CS.SyntaxKind.NamespaceKeyword:

                    // Visibility
                    case CS.SyntaxKind.PublicKeyword:
                    case CS.SyntaxKind.ProtectedKeyword:
                    case CS.SyntaxKind.PrivateKeyword:
                    case CS.SyntaxKind.InternalKeyword:

                    // Type declaration
                    case CS.SyntaxKind.ClassKeyword:
                    case CS.SyntaxKind.EnumKeyword:
                    case CS.SyntaxKind.StructKeyword:
                    case CS.SyntaxKind.InterfaceKeyword:

                    case CS.SyntaxKind.NewKeyword:
                    case CS.SyntaxKind.ReadOnlyKeyword:
                    case CS.SyntaxKind.StaticKeyword:
                    case CS.SyntaxKind.EqualsToken:
                    case CS.SyntaxKind.ReturnKeyword:
                    case CS.SyntaxKind.DotToken:
                        return false;
                }
            }
            else if(language == LanguageNames.VisualBasic)
            {
                switch ((VB.SyntaxKind)token.RawKind)
                {
                    case VB.SyntaxKind.NamespaceKeyword:
                    
                    // Visibility
                    case VB.SyntaxKind.PublicKeyword:
                    case VB.SyntaxKind.ProtectedKeyword:
                    case VB.SyntaxKind.PrivateKeyword:

                    // Type declaration
                    case VB.SyntaxKind.ClassKeyword:
                    case VB.SyntaxKind.EnumKeyword:
                    case VB.SyntaxKind.StructureKeyword:
                    case VB.SyntaxKind.InterfaceKeyword:

                    case VB.SyntaxKind.NewKeyword:
                    case VB.SyntaxKind.ReadOnlyKeyword:
                    case VB.SyntaxKind.StaticKeyword:
                    case VB.SyntaxKind.EqualsToken:
                    case VB.SyntaxKind.ReturnKeyword:
                    case VB.SyntaxKind.DotToken:
                        return false;
                }
            }

            return true;
        }

        public bool IsOverrideKeyword(SyntaxToken token)
        {
            return token.IsEquivalentKind(CS.SyntaxKind.OverrideKeyword);
        }

        public SyntaxNode TryGetUsingExpressionFromToken(SyntaxToken token)
        {
            if (language == LanguageNames.CSharp)
            {
                if (token.IsKind(CS.SyntaxKind.UsingKeyword))
                {
                    var node = token.Parent;
                    if (node.IsKind(CS.SyntaxKind.UsingStatement))
                    {
                        var usingStatement = (CS.Syntax.UsingStatementSyntax)node;

                        return (SyntaxNode)usingStatement.Expression ?? usingStatement.Declaration?.Type;
                    }
                    else if (node.IsKind(CS.SyntaxKind.LocalDeclarationStatement))
                    {
                        var usingStatement = (CS.Syntax.LocalDeclarationStatementSyntax)node;

                        return usingStatement.Declaration?.Type;
                    }
                }
            }
            else if(language == LanguageNames.VisualBasic)
            {
                if (token.IsKind(VB.SyntaxKind.UsingKeyword))
                {
                    var node = token.Parent;
                    if (node.IsKind(VB.SyntaxKind.UsingStatement))
                    {
                        var usingStatement = (VB.Syntax.UsingStatementSyntax)node;
                        return (SyntaxNode)usingStatement.Expression ?? usingStatement.Variables.FirstOrDefault();
                    }
                }
            }

            return null;
        }

        public SyntaxNode GetEventField(SyntaxNode bindableNode)
        {
            if (language == LanguageNames.CSharp)
            {
                if (bindableNode.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.EventFieldDeclaration))
                {
                    var eventFieldSyntax = bindableNode as Microsoft.CodeAnalysis.CSharp.Syntax.EventFieldDeclarationSyntax;
                    if (eventFieldSyntax != null)
                    {
                        bindableNode = eventFieldSyntax.Declaration.Variables[0];
                    }
                }
            }

            return bindableNode;
        }
    }
}
