using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    public class SearchType
    {
        public static SearchType<T> Create<T>([CallerMemberName]string name = null)
        {
            return new SearchType<T>();
        }
    }

    public class SearchType<TSearchType> : SearchType
    {
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

