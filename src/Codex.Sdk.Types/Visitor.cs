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
    [GeneratorExclude]
    public interface IVisitor : IVisitorBase,
        IValueVisitor<string>,
        IValueVisitor<bool>,
        IValueVisitor<long>,
        //IValueVisitor<byte[]>,
        IValueVisitor<SymbolId>,
        IValueVisitor<DateTime>,
        IValueVisitor<int>
    {
    }

    public interface IValueVisitor<TValue> : IVisitorBase
    { 
        void Visit(MappingBase mapping, TValue value);
    }

    public interface IVisitorBase
    {
        bool ShouldVisit(MappingInfo mapping);
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

        public static void VisitEx<T>(this IMapping<T> mapping, IVisitor visitor, T value)
        {
            if (visitor.ShouldVisit(mapping.MappingInfo))
            {
                mapping.Visit(visitor, value);
            }
        }

        public static void VisitEx<T>(this IMapping<T> mapping, IVisitor visitor, IReadOnlyList<T> value)
            where T : class
        {
            if (visitor.ShouldVisit(mapping.MappingInfo))
            {
                mapping.Visit(visitor, value);
            }
        }

        public static void VisitEx<T>(this IValueMapping<T> mapping, IValueVisitor<T> visitor, T value)
        {
            if (visitor.ShouldVisit(mapping.MappingInfo))
            {
                mapping.Visit(visitor, value);
            }
        }

        public static void VisitEx<T>(this IValueMapping<T> mapping, IValueVisitor<T> visitor, IReadOnlyList<T> value)
        {
            if (visitor.ShouldVisit(mapping.MappingInfo))
            {
                mapping.Visit(visitor, value);
            }
        }
    }
}