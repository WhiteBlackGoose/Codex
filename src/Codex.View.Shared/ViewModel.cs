using Codex.Sdk.Search;
using System;
using System.Collections.Generic;
using System.Text;

namespace Codex.View.Shared
{
    public class TextResultViewModel : FileItemResultViewModel
    {
        public ISearchResult SearchResult { get; }

        public TextResultViewModel(ISearchResult result)
        {
            SearchResult = result;
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
        public List<FileItemResultViewModel> Items { get; set; }
    }

    public class ProjectResultsViewModel
    {
        public string ProjectName { get; set; }
        public List<ProjectItemResultViewModel> Items { get; set; }
    }
}
