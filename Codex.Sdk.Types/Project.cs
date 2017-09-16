using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IProject : IProjectScopeEntity
    {
        /// <summary>
        /// References to files in the project
        /// </summary>
        IReadOnlyList<IProjectFileLink> Files { get; }

        IReadOnlyList<IReferencedProject> ProjectReferences { get; }
    }

    public interface IReferencedProject
    {
        /// <summary>
        /// The identifier of the referenced project
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string ProjectId { get; }

        /// <summary>
        /// Used definitions for the project. Sorted.
        /// </summary>
        IReadOnlyList<IDefinitionSymbol> Definitions { get; }

        /// <summary>
        /// The display name of the project
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The properties of the project. Such as Version, PublicKey, etc.
        /// TODO: Implement maps for generated types
        /// </summary>
        IReadOnlyDictionary<string, string> Properties { get; }
    }

    /// <summary>
    /// NOTE: Do not set <see cref="IRepoScopeEntity.RepositoryName"/>
    /// </summary>
    public interface IProjectFileLink : IProjectFileScopeEntity
    {
        /// <summary>
        /// Unique identifier for file
        /// TODO: What is this?
        /// </summary>
        string FileId { get; }
    }
}
