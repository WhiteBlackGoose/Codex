using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.Utilities;
using Codex.ObjectModel;

namespace Codex
{
    partial interface ISearchEntity
    {
        /// <summary>
        /// Returns the underlying search entity
        /// </summary>
        SearchEntity UnderlyingSearchEntity { get; }
    }

    namespace ObjectModel
    {
        partial class SearchEntity
        {
            public SearchEntity UnderlyingSearchEntity => this;
        }
    }
}