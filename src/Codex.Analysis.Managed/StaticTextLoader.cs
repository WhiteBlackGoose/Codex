using System;
using System.Threading;
using System.Threading.Tasks;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Codex.Analysis
{
    /// <summary>
    /// Reference types from CS.Workspaces and VB.Workspaces to make RAR copy the assemblies
    /// </summary>
    public class StaticTextLoader : TextLoader
    {
        public string Content { get; }

        private SourceText sourceText;

        public StaticTextLoader(string content)
        {
            Content = content;
        }

        public override Task<TextAndVersion> LoadTextAndVersionAsync(Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
        {
            sourceText = sourceText ?? SourceText.From(Content);

            return Task.FromResult(TextAndVersion.Create(sourceText, VersionStamp.Default));
        }
    }
}