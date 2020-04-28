using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Codex.ObjectModel;
using Codex.Sdk.Search;

namespace Codex.Schema
{
    //public abstract partial class Mappings<TData>
    //{
    //    //private readonly Lazy<ReferenceSearchModelMapping> _lazyReferences = new Lazy<ReferenceSearchModelMapping>(() => null);
    //    //public ReferenceSearchModelMapping References => _lazyReferences.Value;

    //    //public class SearchEntityMapping
    //    //{
    //    //    [DataMember(Name = nameof(SearchEntity.EntityContentId))]
    //    //    public Mapping<string> EntityContentId { get; }

    //    //    public SearchEntityMapping()
    //    //    {
    //    //    }
    //    //}

    //    //public class ReferenceSearchModelMapping : SearchEntityMapping
    //    //{
    //    //    public ReferenceSymbolMapping Reference { get; }

    //    //    public ReferenceSearchModelMapping(IMappingContext context)
    //    //    {
    //    //        Reference = new ReferenceSymbolMapping(context.CreateSubContext(nameof(Reference)));
    //    //    }
    //    //}

    //    //public class ReferenceSymbolMapping : MappingBase<TData>
    //    //{
    //    //    public ReferenceSymbolMapping(IMappingContext context)
    //    //    {
    //    //    }

    //    //    public Mapping<string> ReferenceKind { get; }
    //    //}
    //}

    public interface IMappingContext
    {
        IMappingContext CreateSubContext(string name);
    }

    public class NullMappingContext : IMappingContext
    {
        public static readonly NullMappingContext Instance = new NullMappingContext();

        public IMappingContext CreateSubContext(string name)
        {
            return Instance;
        }
    }

    public class MappingBase<TData>
    {
        public string Name { get; }
    }
}

namespace Codex.ObjectModel
{
    public class Mapping<TRoot, TValue> : Mapping<TRoot>
    {
        public Mapping(MappingInfo info) : base(info)
        {
        }

        public void Visit(IValueVisitor<TValue> visitor, TValue value)
        {
            visitor.Visit(this, value);
        }

        public void Visit(IValueVisitor<TValue> visitor, IReadOnlyList<TValue> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                Visit(visitor, list[i]);
            }
        }
    }

    public class Mapping<TRoot> : MappingBase
    {
        public Mapping(MappingInfo info) : base(info)
        {
        }
    }
}