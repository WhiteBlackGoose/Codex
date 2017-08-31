using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class InlineAttribute : Attribute
    {
        public readonly bool Inline;

        public InlineAttribute(bool inline = false)
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

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class QueryAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class RestrictedAttribute : Attribute
    {
        public readonly ObjectStage AllowedStages;

        public RestrictedAttribute(ObjectStage stages)
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
        Analysis = 1,
        Index = Analysis << 1 | Analysis,
        Upload = Index << 1 | Index,
        Search = Upload << 1 | Upload,
        All = Search | Upload | Index | Analysis
    }

    public enum SearchBehavior
    {
        None,
        Term,
        NormalizedKeyword,
        Sortword,
        HierarchicalPath,
        FullText,
        Prefix,
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
