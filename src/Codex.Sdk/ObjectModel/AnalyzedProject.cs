using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace Codex.ObjectModel
{
    public partial class AnalyzedProject
    {
        public AnalyzedProject(string repositoryName, string projectId)
        {
            Contract.Requires(!string.IsNullOrEmpty(repositoryName));
            Contract.Requires(!string.IsNullOrEmpty(projectId));

            RepositoryName = repositoryName;
            ProjectId = projectId;
        }

        /// <summary>
        /// Additional source files to add to the repository
        /// </summary>
        public List<BoundSourceFile> AdditionalSourceFiles { get; set; } = new List<BoundSourceFile>();

        /// <summary>
        /// The definitions of reference symbols
        /// </summary>
        public ConcurrentDictionary<Symbol, DefinitionSymbol> ReferenceDefinitionMap = new ConcurrentDictionary<Symbol, DefinitionSymbol>();
    }
}