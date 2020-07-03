using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Codex.Utilities
{
    public class LazySearchTypesMap<T>
        where T : class
    {
        private T[] _valuesById = new T[SearchTypes.RegisteredSearchTypes.Count];
        private Func<SearchType, T> _valueFactory;

        public T this[SearchType searchType]
        {
            get
            {
                var result = _valuesById[searchType.Id];
                if (result == null)
                {
                    result = _valueFactory(searchType);
                    _valuesById[searchType.Id] = result;
                }

                return result;
            }
        }

        public LazySearchTypesMap(Func<SearchType, T> valueFactory, bool initializeAll = false)
        {
            _valueFactory = valueFactory;
            if (initializeAll)
            {
                ForEach(_ => { });
            }
        }

        public void ForEach(Action<T> action)
        {
            foreach (var searchType in SearchTypes.RegisteredSearchTypes)
            {
                action(this[searchType]);
            }
        }
    }
}