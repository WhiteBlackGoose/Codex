using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;

namespace Codex.Utilities
{
    public static partial class CollectionUtilities
    {
        public static IReadOnlyList<TResult> SelectList<T, TResult>(this IReadOnlyCollection<T> items, Func<T, TResult> selector)
        {
            TResult[] results = new TResult[items.Count];
            int i = 0;
            foreach (var item in items)
            {
                results[i] = selector(item);
                i++;
            }

            return results;
        }

        public static T SingleOrDefaultNoThrow<T>(this IEnumerable<T> items)
        {
            int count = 0;
            T result = default(T);
            foreach (var item in items)
            {
                if (count == 0)
                {
                    result = item;
                }
                else
                {
                    return default(T);
                }

                count++;
            }

            return result;
        }

        public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> items)
        {
            if (items is IReadOnlyList<T>)
            {
                return (IReadOnlyList<T>)items;
            }
            else
            {
                return items.ToList();
            }
        }

        public static IEnumerable<T> Interleave<T>(IEnumerable<T> spans1, IEnumerable<T> spans2)
            where T : Span
        {
            bool end1 = false;
            bool end2 = false;

            var enumerator1 = spans1.GetEnumerator();
            var enumerator2 = spans2.GetEnumerator();

            T current1 = default(T);
            T current2 = default(T);

            end1 = MoveNext(enumerator1, ref current1);
            end2 = MoveNext(enumerator2, ref current2);

            while (!end1 || !end2)
            {
                while (!end1)
                {
                    if (end2 || current1.Start <= current2.Start)
                    {
                        yield return current1;
                    }
                    else
                    {
                        break;
                    }

                    end1 = MoveNext(enumerator1, ref current1);
                }

                while (!end2)
                {
                    if (end1 || current2.Start <= current1.Start)
                    {
                        yield return current2;
                    }
                    else
                    {
                        break;
                    }

                    end2 = MoveNext(enumerator2, ref current2);
                }
            }
        }

        public static IEnumerable<T> SortedUnique<T>(this IEnumerable<T> items, IComparer<T> comparer)
        {
            T lastItem = default(T);
            bool hasLastItem = false;
            foreach (var item in items)
            {
                if (!hasLastItem || comparer.Compare(lastItem, item) != 0)
                {
                    hasLastItem = true;
                    lastItem = item;
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> ExclusiveInterleave<T>(this IEnumerable<T> items1, IEnumerable<T> items2, IComparer<T> comparer)
        {
            bool end1 = false;
            bool end2 = false;

            var enumerator1 = items1.GetEnumerator();
            var enumerator2 = items2.GetEnumerator();

            T current1 = default(T);
            T current2 = default(T);

            end1 = MoveNext(enumerator1, ref current1);
            end2 = MoveNext(enumerator2, ref current2);

            while (!end1 || !end2)
            {
                while (!end1)
                {
                    if (end2 || comparer.Compare(current1, current2) <= 0)
                    {
                        yield return current1;

                        // Skip over matching spans from second list
                        while (!end2 && comparer.Compare(current1, current2) == 0)
                        {
                            end2 = MoveNext(enumerator2, ref current2);
                        }
                    }
                    else
                    {
                        break;
                    }

                    end1 = MoveNext(enumerator1, ref current1);
                }

                while (!end2)
                {
                    if (end1 || comparer.Compare(current1, current2) > 0)
                    {
                        yield return current2;
                    }
                    else
                    {
                        break;
                    }

                    end2 = MoveNext(enumerator2, ref current2);
                }
            }
        }

        private static bool MoveNext<T>(IEnumerator<T> enumerator1, ref T current)
        {
            if (enumerator1.MoveNext())
            {
                current = enumerator1.Current;
                return true;
            }
            else
            {
                return false;
            }
        }

        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                value = defaultValue;
            }

            return value;
        }

        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default(TValue))
        {
            TValue value;
            if (!dictionary.TryGetValue(key, out value))
            {
                dictionary[key] = defaultValue;
                value = defaultValue;
            }

            return value;
        }
    }
}
