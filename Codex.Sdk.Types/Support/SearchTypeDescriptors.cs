using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public abstract class SearchType
    {
        public string Name { get; protected set; }

        public static SearchType<T> Create<T>(List<SearchType> registeredSearchTypes, [CallerMemberName]string name = null)
            where T : class, ISearchEntity
        {
            var searchType = new SearchType<T>(name);
            registeredSearchTypes.Add(searchType);
            return searchType;
        }

        public abstract Type Type { get; }
    }

    public class SearchType<TSearchType> : SearchType
        where TSearchType : class, ISearchEntity
    {
        public override Type Type => typeof(TSearchType);

        public SearchType(string name)
        {
            Name = name;
        }

        public SearchType<TSearchType> Inherit<TPRovider, T>(
            Expression<Func<TPRovider, T>> providerField,
            Expression<Func<TSearchType, T>> searchField)
        {
            return this;
        }

        public SearchType<TSearchType> CopyTo(
            Expression<Func<TSearchType, object>> sourceField,
            Expression<Func<TSearchType, object>> targetField)
        {
            return this;
        }

        public SearchType<TSearchType> SearchAs<T>(
            Expression<Func<TSearchType, T>> searchField,
            SearchBehavior behavior)
        {
            return this;
        }
    }
}

