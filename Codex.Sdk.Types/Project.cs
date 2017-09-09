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
        IReadOnlyList<IFileLink> Files { get; }

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

    public interface IFileLink
    {
        /// <summary>
        /// The virtual path to the file inside the project
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Unique identifer for file
        /// </summary>
        string FileId { get; }

        /// <summary>
        /// Unique identifier of project
        /// </summary>
        string ProjectId { get; }
    }
}
