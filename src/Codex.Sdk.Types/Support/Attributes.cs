using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class SearchDescriptorInlineAttribute : Attribute
    {
        public readonly bool Inline;

        public SearchDescriptorInlineAttribute(bool inline = false)
        {
            Inline = inline;
        }
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class EntityIdAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PlaceholderAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class SerializationInterfaceAttribute : Attribute
    {
        public readonly Type Type;

        public SerializationInterfaceAttribute(Type type)
        {
            Type = type;
        }
    }

    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class AdapterTypeAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    public sealed class GeneratedClassNameAttribute : Attribute
    {
        public readonly string Name;

        public GeneratedClassNameAttribute(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Excludes a property from serialization. Mainly used for excluding properties from serialization
    /// which have an inferred value such as (ReferenceKind on DefinitionSymbol is inferred as Definition)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class ExcludedSerializationPropertyAttribute : Attribute
    {
        public readonly string PropertyName;

        public ExcludedSerializationPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }

    /// <summary>
    /// Indicates an attached property which is not intrinsic to the parent object and should be
    /// excluded when computing the <see cref="ISearchEntity.EntityContentId"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class AttachedAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class QueryAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class ReadOnlyListAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class CoerceGetAttribute : Attribute
    {
        public readonly Type CoercedSourceType;

        public CoerceGetAttribute(Type coercedSourceType = null)
        {
            CoercedSourceType = coercedSourceType;
        }
    }

    /// <summary>
    /// Indicates stages for which the given property should be included
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class IncludeAttribute : Attribute
    {
        public readonly ObjectStage AllowedStages;

        public IncludeAttribute(ObjectStage stages)
        {
            AllowedStages = stages;
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Interface | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class RequiredForAttribute : Attribute
    {
        public readonly ObjectStage Stages;

        public RequiredForAttribute(ObjectStage stages)
        {
            Stages = stages;
        }
    }

    public enum ObjectStage
    {
        None = 0,
        Analysis = 1,
        Index = 1 << 1,
        Search = 1 << 2 | Index,
        All = Search | Index | Analysis,
        StoreRaw = 1 << 3 | All
    }

    public enum SearchBehavior
    {
        None,
        Term,
        NormalizedKeyword,
        Sortword,
        HierarchicalPath,
        FullText,
        PrefixTerm,
        PrefixShortName,
        PrefixFullName
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class SearchBehaviorAttribute : Attribute
    {
        public readonly SearchBehavior Behavior;

        public SearchBehaviorAttribute(SearchBehavior behavior)
        {
            Behavior = behavior;
        }
    }
}
