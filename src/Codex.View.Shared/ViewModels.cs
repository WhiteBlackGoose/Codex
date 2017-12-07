using Codex.Sdk.Search;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Codex.View
{
    public class TextSpanSearchResultViewModel : FileItemResultViewModel
    {
        public ISearchResult SearchResult { get; }

        public int LineNumber { get; }
        public string PrefixText { get; }
        public string ContentText { get; }
        public string SuffixText { get; }

        public TextSpanSearchResultViewModel(ISearchResult result)
        {
            SearchResult = result;
            var referringSpan = result.TextSpan;
            LineNumber = referringSpan.LineNumber;
            string lineSpanText = referringSpan.LineSpanText;
            if (lineSpanText != null)
            {
                PrefixText = lineSpanText.Substring(0, referringSpan.LineSpanStart);
                ContentText = lineSpanText.Substring(referringSpan.LineSpanStart, referringSpan.Length);
                SuffixText = lineSpanText.Substring(referringSpan.LineSpanStart + referringSpan.Length);
            }
        }
    }

    public class ProjectItemResultViewModel
    {

    }

    public class FileItemResultViewModel
    {

    }

    public class FileResultsViewModel : ProjectItemResultViewModel
    {
        public string Path { get; set; }
        public IReadOnlyList<FileItemResultViewModel> Items { get; set; }
    }

    public class ProjectResultsViewModel
    {
        public string ProjectName { get; set; }
        public IReadOnlyList<ProjectItemResultViewModel> Items { get; set; }
    }

    public class TextSearchResultsViewModel
    {
        public string Header { get; }

        public List<ProjectResultsViewModel> ProjectResults { get; }

        public TextSearchResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            var result = response.Result;

            ProjectResults = result.Hits.GroupBy(sr => sr.ProjectId).Select(projectGroup => new ProjectResultsViewModel()
            {
                ProjectName = projectGroup.Key,
                Items = projectGroup.GroupBy(sr => sr.ProjectRelativePath).Select(fileGroup => new FileResultsViewModel()
                {
                    Path = fileGroup.Key,
                    Items = fileGroup.Select(sr => new TextSpanSearchResultViewModel(sr)).ToList()
                }).ToList()
            }).ToList();

            Header = $"{result.Hits.Count} text search hits for '{searchString}'";
        }
    }
}
