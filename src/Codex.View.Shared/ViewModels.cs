using Codex.Sdk.Search;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace Codex.View
{
    public class TextSpanSearchResultViewModel : FileItemResultViewModel
    {
        public ITextLineSpanResult SearchResult { get; }

        public int LineNumber { get; }
        public string PrefixText { get; }
        public string ContentText { get; }
        public string SuffixText { get; }

        public TextSpanSearchResultViewModel(ITextLineSpanResult result)
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

    public class SymbolResultViewModel : ProjectItemResultViewModel
    {
        public string ShortName { get; set; }
        public string DisplayName { get; set; }
        public string SymbolKind { get; set; }
        public string ProjectId { get; set; }

        public string ImageMoniker { get; set; }
        public int SortOrder { get; set; }
        public int IdentifierLength => ShortName.Length;

        public SymbolResultViewModel(IDefinitionSymbol entry)
        {
            ShortName = entry.ShortName;
            DisplayName = entry.DisplayName;
            ProjectId = entry.ProjectId;
            SymbolKind = entry.Kind?.ToLowerInvariant();
        }
    }

    public class ProjectGroupResultsViewModel
    {
        public string ProjectName { get; set; }
        public IReadOnlyList<ProjectItemResultViewModel> Items { get; set; }
    }

    public class ProjectResultsViewModel : LeftPaneContent
    {
        public List<ProjectGroupResultsViewModel> ProjectGroups { get; set; }

        public ProjectResultsViewModel()
        {
        }

        public ProjectResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            ProjectGroups = response.Result.Hits.Select(sr => sr.Definition).GroupBy(sr => sr.ProjectId).Select(projectGroup => new ProjectGroupResultsViewModel()
            {
                ProjectName = projectGroup.Key,
                Items = projectGroup.Select(symbol => new SymbolResultViewModel(symbol)).ToList()
            }).ToList();
        }
    }

    public class CategoryGroupSearchResultsViewModel
    {
        public Visibility HeaderVisibility => string.IsNullOrEmpty(Header) ? Visibility.Collapsed : Visibility.Visible;

        public string Header { get; }

        public ProjectResultsViewModel ProjectResults { get; set; } = new ProjectResultsViewModel();

        public CategoryGroupSearchResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            var result = response.Result;

            ProjectResults.ProjectGroups = result.Hits.Select(sr => sr.TextLine).GroupBy(sr => sr.ProjectId).Select(projectGroup => new ProjectGroupResultsViewModel()
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

    public class CategorizedSearchResultsViewModel : LeftPaneContent
    {
        public List<CategoryGroupSearchResultsViewModel> Categories { get; }

        public CategorizedSearchResultsViewModel(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            Categories = new List<CategoryGroupSearchResultsViewModel>()
            {
                new CategoryGroupSearchResultsViewModel(searchString, response)
            };
        }
    }

    public interface LeftPaneContent
    {
    }

    public class LeftPaneViewModel : NotifyPropertyChangedBase
    {
        public Visibility SearchInfoVisibility => !string.IsNullOrEmpty(SearchInfo) ? Visibility.Visible : Visibility.Collapsed;

        public string SearchInfo { get; set; } = null;

        public Visibility ContentVisibility => Content != null ? Visibility.Visible : Visibility.Collapsed;

        public LeftPaneContent Content { get; set; }

        public static readonly LeftPaneViewModel Initial = new LeftPaneViewModel()
        {
            SearchInfo = "Enter a search string. Start with ` for full text search results only."
        };

        public static LeftPaneViewModel FromSearchResponse(string searchString, IndexQueryHitsResponse<ISearchResult> response)
        {
            if (response.Error != null)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = response.Error
                };
            }
            else if (response.Result?.Hits == null || response.Result.Hits.Count == 0)
            {
                return new LeftPaneViewModel()
                {
                    SearchInfo = $"No results found."
                };
            }

            var result = response.Result;
            bool isDefinitionsResult = result.Hits[0].Definition != null;
            return new LeftPaneViewModel()
            {
                Content = isDefinitionsResult ?
                    (LeftPaneContent)new ProjectResultsViewModel(searchString, response) :
                    new CategorizedSearchResultsViewModel(searchString, response),
                SearchInfo = isDefinitionsResult ?
                    (result.Hits.Count < result.Total ?
                        $"Displaying top {result.Hits.Count} results out of {result.Total}:" :
                        $"{result.Hits.Count} results found:")
                    : string.Empty
            };
        }
    }

    public class BindableValue<T> : NotifyPropertyChangedBase
    {
        private T value;

        public T Value
        {
            get
            {
                return value;
            }

            set
            {
                this.value = value;
                OnPropertyChanged();
            }
        }
    }


    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        protected void OnPropertyChanged([CallerMemberName] string memberName = null)
        {
            propertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
        }

        private event PropertyChangedEventHandler propertyChanged;
        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                propertyChanged += value;
            }

            remove
            {
                propertyChanged -= value;
            }
        }
    }

    public class ViewModelDataContext : NotifyPropertyChangedBase
    {
        private LeftPaneViewModel leftPane;

        public LeftPaneViewModel LeftPane
        {
            get
            {
                return leftPane;
            }

            set
            {
                // HACK to force full refresh of left pane
                //if (value != null)
                //{
                //    leftPane = null;
                //    OnPropertyChanged();
                //}

                leftPane = value;
                OnPropertyChanged();
            }
        }

        public void Initialize()
        {
            LeftPane = LeftPaneViewModel.Initial;
        }
    }
}
