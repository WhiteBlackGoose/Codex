using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Sdk.Search;

namespace Codex.ObjectModel
{
    public interface IVisitor : 
        IValueVisitor<string>,
        IValueVisitor<bool>,
        IValueVisitor<long>,
        //IValueVisitor<byte[]>,
        IValueVisitor<SymbolId>,
        IValueVisitor<DateTime>,
        IValueVisitor<int>
    {
        bool ShouldVisit(MappingInfo mapping);
    }

    public interface IValueVisitor<TValue>
    { 
        void Visit(MappingBase mapping, TValue value);
    }

    public interface IQueryFactory<TQuery> : 
        IQueryFactory<TQuery, string>,
        IQueryFactory<TQuery, bool>,
        IQueryFactory<TQuery, long>,
        IQueryFactory<TQuery, SymbolId>,
        IQueryFactory<TQuery, DateTime>,
        IQueryFactory<TQuery, int>
    {
    }

    public interface IQueryFactory<TQuery, TValue>
    {
        TQuery TermQuery(MappingBase mapping, TValue term);
    }

    public static class Visitor
    {
        public static void Visit<T>(this IMapping<T> mapping, IVisitor visitor, IReadOnlyList<T> list)
            where T : class
        {
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item != null)
                {
                    mapping.Visit(visitor, item);
                }
            }
        }
    }
}