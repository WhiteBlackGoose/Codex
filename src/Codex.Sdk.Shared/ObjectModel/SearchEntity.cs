using Codex.Utilities;
using System;
using System.Diagnostics;
using System.Linq;
using static Codex.Utilities.SerializationUtilities;

namespace Codex.ObjectModel
{
    partial class SearchEntity
    {
        public virtual SearchType GetSearchType()
        {
            throw new NotImplementedException();
        }

        public virtual string GetRoutingKey()
        {
            throw new NotImplementedException();
        }

        protected virtual string GetRoutingKey<TBase, T>(SearchType<TBase> searchType, T entity)
            where TBase : class, ISearchEntity
            where T : TBase
        {
            return searchType?.GetRoutingKey?.Invoke(entity);
        }
    }
}