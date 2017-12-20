using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public interface ILanguageInfo
    {
        /// <summary>
        /// The name of the language
        /// </summary>
        [SearchBehavior(SearchBehavior.NormalizedKeyword)]
        string Name { get; }

        /// <summary>
        /// Describes the style for classified spans. See <see cref="IBoundSourceInfo.Classifications"/>.
        /// </summary>
        IReadOnlyList<IClassificationStyle> Classifications { get; }
    }

    /// <summary>
    /// Describes styling for a given classification
    /// </summary>
    public interface IClassificationStyle
    {
        /// <summary>
        /// The default classification color for the classification. This is used for
        /// contexts where a mapping from classification id to color is not
        /// available.
        /// </summary>
        int Color { get; }

        /// <summary>
        /// Indicates whether the spans classified with this classification should have italic font by default
        /// </summary>
        bool Italic { get; }

        /// <summary>
        /// The name of the classification
        /// </summary>
        string Name { get; }
    }
}
