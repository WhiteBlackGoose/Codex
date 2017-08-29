using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Types
{
    public interface IRef<T>
    {
        string Id { get; }
    }

    public interface INested<TContainer>
    {

    }

    public interface IProperty
    {
        /// <summary>
        /// The key of the property
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string Key { get; }

        /// <summary>
        /// The value of the property
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string Value { get; }

        /// <summary>
        /// The identifier of the object owning this property
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string OwnerId { get; }
    }
}
