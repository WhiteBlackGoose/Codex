using System.Windows;
using Bridge;
using Bridge.Html5;
using Granular.Presentation.Web;
using Monaco;
using System.Windows.Media;
using static monaco.editor;
using System.Windows.Threading;
using Granular.Host;
using static Codex.View.ViewUtilities;
using System;
using System.Windows.Input;

namespace Codex.View
{
    public partial class LeftPaneViewModel
    {
        public void Render(LeftPaneView view, HTMLElement parentElement)
        {
            Content?.Render(view, parentElement);
        }
    }

    public abstract partial class LeftPaneChild
    {
        public abstract void Render(LeftPaneView view, HTMLElement parentElement);
    }

    public partial class LeftPaneContent : LeftPaneChild
    {
    }

    public abstract partial class ProjectItemResultViewModel : LeftPaneChild
    {
    }

    public abstract partial class FileItemResultViewModel : LeftPaneChild
    {
    }

    public partial class FileResultsViewModel
    {
        public override void Render(LeftPaneView view, HTMLElement parentElement)
        {
            var fileGroupElement = new HTMLDivElement() { ClassName = "rF" };
            parentElement.AppendChild(fileGroupElement);

            fileGroupElement.AppendChild(new HTMLDivElement()
            {
                ClassName = "rN",
                TextContent = $"{Path} ({Counter.Count})"
            }
            .SetBackgroundIcon(GetFileNameGlyph(Path)));

            foreach (var item in Items)
            {
                item.Render(view, fileGroupElement);
            }
        }
    }

    public partial class SymbolResultViewModel
    {
        public override void Render(LeftPaneView view, HTMLElement parentElement)
        {
            parentElement.AppendChild(
                new HTMLAnchorElement()
                .WithOnClick(() => { Commands.GoToDefinition.RaiseExecuted(view, this.Symbol); })
                .WithChild(
                    new HTMLDivElement()
                    {
                        ClassName = "resultItem"
                    }
                    .WithChild(
                        new HTMLImageElement()
                        {
                            Height = 16,
                            Width = 16,
                            Src = GetIconPath(Symbol.GetGlyph())
                        })
                    .WithChild(
                        new HTMLDivElement()
                        {
                            ClassName = "resultKind",
                            TextContent = Symbol.Kind.ToLowerCase()
                        })
                    .WithChild(
                        new HTMLDivElement()
                        {
                            ClassName = "resultName",
                            TextContent = Symbol.ShortName
                        })
                    .WithChild(
                        new HTMLDivElement()
                        {
                            ClassName = "resultDescription",
                            TextContent = Symbol.DisplayName
                        })
                ));
        }
    }

    public partial class TextSpanSearchResultViewModel
    {
        public override void Render(LeftPaneView view, HTMLElement parentElement)
        {
            parentElement
                .WithChild(
                    new HTMLAnchorElement()
                    {
                        ClassName = "rL"
                    }
                    .WithOnClick(() =>
                    {
                        if (TextResult != null)
                        {
                            Commands.GoToSpan.RaiseExecuted(view, TextResult);
                        }
                        else
                        {
                            Commands.GoToReference.RaiseExecuted(view, ReferenceResult);
                        }
                    })
                    .WithChild(Document.CreateElement("b").WithText(LineNumber.ToString()))
                    .WithChild(new Text(PrefixText))
                    .WithChild(Document.CreateElement("i").WithText(ContentText))
                    .WithChild(new Text(SuffixText)));
        }
    }

    public partial class CategorizedSearchResultsViewModel
    {
        public override void Render(LeftPaneView view, HTMLElement parentElement)
        {
            foreach (var category in Categories)
            {
                category.Render(view, parentElement);
            }
        }
    }

    public partial class CategoryGroupSearchResultsViewModel
    {
        public void Render(LeftPaneView view, HTMLElement parentElement)
        {
            var contentElement = RenderHeaderedContent(parentElement, "rH", Header, "rK");
            ProjectResults.Render(view, contentElement);
        }
    }

    public partial class ProjectResultsViewModel
    {
        public override void Render(LeftPaneView view, HTMLElement parentElement)
        {
            foreach (var projectGroup in ProjectGroups)
            {
                projectGroup.Render(view, parentElement);
            }
        }
    }

    public partial class ProjectGroupResultsViewModel
    {
        public void Render(LeftPaneView view, HTMLElement parentElement)
        {
            var contentElement = RenderHeaderedContent(parentElement, "rA", $"{ProjectName} ({Counter.Count})", "rG");
            foreach (var item in Items)
            {
                item.Render(view, contentElement);
            }
        }
    }
}
