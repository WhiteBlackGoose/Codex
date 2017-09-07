using System;
using System.Collections.Generic;
using System.Text;

namespace Codex
{
    public interface IPropertySearchModel : ISearchEntity
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
