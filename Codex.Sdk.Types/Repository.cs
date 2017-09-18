using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface IRepository
    {
        /// <summary>
        /// The name fo the repository
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Describes the repository
        /// </summary>
        string Description { get; }

        [EntityId]
        string Id { get; }

        string WebAddress { get; }

        /// <summary>
        /// The web address for source control of the repository
        /// </summary>
        string SourceControlWebAddress { get; }

        /// <summary>
        /// The regex for extracting
        /// </summary>
        string FileWebAddressTransformInputRegex { get; }

        /// <summary>
        /// The name of the primary branch for the repository
        /// </summary>
        string PrimaryBranch { get; }

        IReadOnlyList<IRepositoryReference> RepositoryReferences { get; }
    }

    public interface IRepositoryReference
    {
        /// <summary>
        /// The name of the reference repository
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Optional. Id of repository
        /// </summary>
        string Id { get; }
    }
}
