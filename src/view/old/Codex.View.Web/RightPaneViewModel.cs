using Bridge.Html5;
using Codex.View.Web;
using Granular.Host;
using System;
using System.Windows;
using static Codex.View.ViewUtilities;
using static Codex.View.Web.HtmlBuilder;
using Visibility = System.Windows.Visibility;

namespace Codex.View
{
    public partial class RightPaneViewModel
    {
        public Lazy<SourceFileDetailsViewModel> SourceFileDetails { get; private set; }

        public Lazy<OverviewViewModel> Overview { get; private set; }

        public Visibility OverviewVisibility => BoolVisibility(Overview.Value != null);

        protected override void Initialize()
        {
            SourceFileDetails = new Lazy<SourceFileDetailsViewModel>(() =>
            {
                if (SourceFile == null)
                {
                    return null;
                }

                return new SourceFileDetailsViewModel(SourceFile);
            });

            Overview = new Lazy<OverviewViewModel>(() =>
            {
                if (SourceFile == null && Error == null)
                {
                    return new OverviewViewModel();
                }

                return null;
            });
        }
    }

    public class OverviewViewModel : IHtmlContent
    {
        public async void Render(HTMLElement parentElement, RenderContext context)
        {
            var overviewHtml = await Request.GetTextAsync("overview.html");
            parentElement.InnerHTML = overviewHtml;
        }
    }

    public class SourceFileDetailsViewModel : IHtmlContent
    {
        private IBoundSourceFile sourceFile;

        public SourceFileDetailsViewModel(IBoundSourceFile sourceFile)
        {
            this.sourceFile = sourceFile;
        }

        public void Render(HTMLElement parentElement, RenderContext context)
        {
            var table = new HTMLTableElement() { Id = "bottomPane", ClassName = "dH" };
            table.Style.Width = "100%";
            var row0 = table.InsertRow();
            var cell00 = row0.InsertCell();
            cell00.AppendChild(new Text("File:" + NBSP));
            cell00.AppendChild(new HTMLAnchorElement()
            {
                Id = "filePathLink",
                ClassName = "blueLink",
                Href = $"/?leftProject={sourceFile.ProjectId}&file={sourceFile.ProjectRelativePath}",
                Target = "_blank",
                Title = "Click to open file in a new tab",
                TextContent = sourceFile.ProjectRelativePath
            });

            var cell01 = row0.InsertCell();
            cell01.SetHtmlTextAlignment(TextAlignment.Right, Converter);
            cell01.AppendChild(Text(sourceFile.RepoRelativePath));

            var row1 = table.InsertRow();
            var cell10 = row1.InsertCell();
            cell10.AppendChild(Text("Project:" + NBSP));
            cell10.AppendChild(new HTMLAnchorElement()
            {
                Id = "projectExplorerLink",
                ClassName = "blueLink",
                Href = $"/?leftProject={sourceFile.ProjectId}",
                Title = "Click to open in project explorer",
                TextContent = sourceFile.ProjectId
            });
            parentElement.AppendChild(table);

            //cell00.AppendChild(Text(NBSP + "("));
            //cell00.AppendChild(new HTMLAnchorElement()
            //{
            //    Id = "filePathLink",
            //    ClassName = "blueLink",
            //    Href = $"/download/",
            //    Target = "_blank",
            //    Title = "Click to open file in a new tab",
            //    TextContent = sourceFile.ProjectRelativePath
            //});
        }
    }

    public class Lazy<T>
    {
        private Func<T> m_valueFactory;
        private T value;
        public T Value
        {
            get
            {
                if (m_valueFactory != null)
                {
                    value = m_valueFactory();
                    m_valueFactory = null;
                }

                return value;
            }
        }

        public Lazy(Func<T> valueFactory)
        {
            m_valueFactory = valueFactory;
        }
    }
}
