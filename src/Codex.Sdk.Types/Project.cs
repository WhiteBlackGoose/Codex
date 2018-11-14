using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    [GeneratedClassName("AnalyzedProject")]
    public interface IProject : IProjectScopeEntity
    {
        /// <summary>
        /// The project kind (see <see cref="ObjectModel.ProjectKind"/>)
        /// </summary>
        string ProjectKind { get; }

        /// <summary>
        /// The primary file of the project (i.e. the .csproj file)
        /// </summary>
        IProjectFileLink PrimaryFile { get; }

        /// <summary>
        /// References to files in the project
        /// </summary>
        IReadOnlyList<IProjectFileLink> Files { get; }

        /// <summary>
        /// Descriptions of referenced projects and used definitions from the projects
        /// </summary>
        IReadOnlyList<IReferencedProject> ProjectReferences { get; }
    }

    namespace ObjectModel
    {
        /// <summary>
        /// Defines standard set of project kinds
        /// </summary>
        public enum ProjectKind
        {
            Source,

            MetadataAsSource,

            Decompilation
        }
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
        [Include(ObjectStage.Analysis)]
        IReadOnlyList<IDefinitionSymbol> Definitions { get; }

        /// <summary>
        /// The display name of the project
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The properties of the project. Such as Version, PublicKey, etc.
        /// </summary>
        IPropertyMap Properties { get; }
    }

    /// <summary>
    /// NOTE: Do not set <see cref="IRepoScopeEntity.RepositoryName"/>
    /// </summary>
    public interface IProjectFileLink : IProjectFileScopeEntity
    {
        /// <summary>
        /// Unique identifier for file
        /// TODO: Make this checksum and searchable and use for discovering commit from PDB.
        /// TODO: What is this?
        /// </summary>
        string FileId { get; }
    }
}