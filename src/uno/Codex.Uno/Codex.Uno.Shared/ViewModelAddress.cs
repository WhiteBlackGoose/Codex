using Codex.ObjectModel;
using Codex.Sdk.Search;
using Nest;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;

namespace Codex.View
{
    public record ViewModelAddress
    {
        public string rightProjectId;
        public string leftProjectId;
        public string filePath;
        public string leftSymbolId;
        public string projectScope;
        public string rightSymbolId;
        public int? lineNumber;
        public string searchText;
        public string WindowTitle;
        public RightPaneMode rightPaneMode;
        public LeftPaneMode leftPaneMode;


        public void Navigate(MainController app)
        {
            switch (leftPaneMode)
            {
                case LeftPaneMode.unspecified:
                    break;
                case LeftPaneMode.search:
                    app.SearchTextChanged(searchText ?? "");
                    break;
                case LeftPaneMode.outline:
                    break;
                case LeftPaneMode.project:
                    // TODO:
                    break;
                case LeftPaneMode.references:
                    app.FindAllReferences(new FindAllReferencesArguments()
                    {
                        SymbolId = leftSymbolId,
                        ProjectId = leftProjectId,
                        ProjectScopeId = projectScope
                    });
                    break;
                case LeftPaneMode.namespaces:
                    break;
                default:
                    break;
            }

            switch (rightPaneMode)
            {
                case RightPaneMode.overview:
                    break;
                case RightPaneMode.file:
                    break;
                case RightPaneMode.line:
                    break;
                case RightPaneMode.symbol:
                    break;
                case RightPaneMode.about:
                    break;
                default:
                    break;
            }
        }

        private bool ParseValue<TValue>(Dictionary<string, string> queryParams, string paramName, ref TValue paramValue, string alternateParamName = null)
        {
            throw new NotImplementedException();
        }

        public void Parse(Dictionary<string, string> queryParams)
        {
            if (ParseValue(queryParams, "left", ref leftPaneMode))
            {
                switch (leftPaneMode)
                {
                    case LeftPaneMode.outline:
                        ParseValue(queryParams, "leftProject", ref leftProjectId, "rightProject");
                        ParseValue(queryParams, "leftSymbol", ref leftSymbolId, "file");
                        break;
                    case LeftPaneMode.search:
                        ParseValue(queryParams, "query", ref searchText);
                        break;
                    case LeftPaneMode.project:
                        ParseValue(queryParams, "leftProject", ref leftProjectId, "rightProject");
                        break;
                    case LeftPaneMode.references:
                        ParseValue(queryParams, "leftProject", ref leftProjectId, "rightProject");
                        ParseValue(queryParams, "leftSymbol", ref leftSymbolId, "rightSymbol");
                        break;
                    case LeftPaneMode.namespaces:
                        ParseValue(queryParams, "leftProject", ref leftProjectId, "rightProject");
                        break;
                    default:
                        break;
                }
            }
            else
            {
                if (ParseValue(queryParams, "query", ref searchText))
                {
                    leftPaneMode = LeftPaneMode.search;
                }
                else if (ParseValue(queryParams, "leftProject", ref leftProjectId))
                {
                    if (ParseValue(queryParams, "leftSymbol", ref leftSymbolId))
                    {
                        leftPaneMode = LeftPaneMode.references;
                    }
                    else
                    {
                        leftPaneMode = LeftPaneMode.project;
                    }
                }
            }

            if (ParseValue(queryParams, "right", ref rightPaneMode))
            {
                ParseValue(queryParams, "rightProject", ref rightProjectId, "leftProject");
                ParseValue(queryParams, "file", ref filePath);
                ParseValue(queryParams, "line", ref lineNumber);
                ParseValue(queryParams, "rightSymbol", ref rightSymbolId);
            }
            else
            {
                if (ParseValue(queryParams, "rightProject", ref rightProjectId, "leftProject"))
                {
                    if (ParseValue(queryParams, "file", ref filePath))
                    {
                        if (ParseValue(queryParams, "line", ref lineNumber))
                        {
                            rightPaneMode = RightPaneMode.line;
                        }
                        else if (ParseValue(queryParams, "rightSymbol", ref rightSymbolId))
                        {
                            rightPaneMode = RightPaneMode.symbol;
                        }
                        else
                        {
                            rightPaneMode = RightPaneMode.file;
                        }
                    }
                }
            }
        }

        public void ToUrl()
        {
            var queryParams = new Dictionary<string, string>(); ;

            switch (leftPaneMode)
            {
                case LeftPaneMode.outline:
                    if (leftProjectId != rightProjectId || leftSymbolId != filePath)
                    {
                        AppendParam(queryParams, "left", "outline");
                        AppendParam(queryParams, "leftProject", leftProjectId);
                        AppendParam(queryParams, "leftSymbol", leftSymbolId);
                    }
                    else if (!string.IsNullOrEmpty(rightProjectId) && !string.IsNullOrEmpty(filePath))
                    {
                        AppendParam(queryParams, "left", "outline");
                    }
                    break;
                case LeftPaneMode.search:
                    AppendParam(queryParams, "query", searchText);
                    break;
                case LeftPaneMode.project:
                    AppendParam(queryParams, "leftProject", leftProjectId);
                    break;
                case LeftPaneMode.references:
                    if (!string.IsNullOrEmpty(leftProjectId) && !string.IsNullOrEmpty(leftSymbolId))
                    {
                        AppendParam(queryParams, "leftProject", leftProjectId);
                        AppendParam(queryParams, "leftSymbol", leftSymbolId, "rightSymbol");
                        AppendParam(queryParams, "projectScope", projectScope);
                    }
                    break;
                case LeftPaneMode.namespaces:
                    AppendParam(queryParams, "left", "namespaces");
                    AppendParam(queryParams, "leftProject", leftProjectId, "rightProject");
                    break;
                default:
                    break;
            }

            AppendParam(queryParams, "right", rightPaneMode);

            switch (rightPaneMode)
            {
                case RightPaneMode.file:
                    if (!string.IsNullOrEmpty(rightProjectId) && !string.IsNullOrEmpty(rightSymbolId))
                    {
                        AppendParam(queryParams, "rightProject", rightProjectId, "leftProject");
                        AppendParam(queryParams, "file", filePath);
                    }
                    break;
                case RightPaneMode.line:
                    if (!string.IsNullOrEmpty(rightProjectId) && !string.IsNullOrEmpty(rightSymbolId) && lineNumber != null)
                    {
                        AppendParam(queryParams, "rightProject", rightProjectId, "leftProject");
                        AppendParam(queryParams, "file", filePath);
                        AppendParam(queryParams, "line", lineNumber);
                    }
                    break;
                case RightPaneMode.symbol:
                    if (!string.IsNullOrEmpty(rightProjectId) && !string.IsNullOrEmpty(rightSymbolId))
                    {
                        AppendParam(queryParams, "rightProject", rightProjectId, "leftProject");
                        AppendParam(queryParams, "file", filePath);
                        AppendParam(queryParams, "rightSymbol", rightSymbolId);
                    }
                    break;
                case RightPaneMode.overview:
                case RightPaneMode.about:
                default:
                    break;
            }
        }

        private void AppendParam(Dictionary<string, string> queryParams, string paramName, object paramValue, string alternateParamName = null)
        {
            var value = paramValue?.ToString();
            if (!string.IsNullOrEmpty(value))
            {
                if (alternateParamName == null 
                    || !queryParams.TryGetValue(alternateParamName, out var alternateValue) 
                    || alternateValue != value)
                {
                    queryParams[paramName] = value;
                }
            }
        }
    }

    public enum RightPaneMode
    {
        overview,
        file,
        line,
        symbol,
        about
    }

    public enum LeftPaneMode
    {
        unspecified,
        search,
        outline,
        project,
        references,
        namespaces
    }
}
