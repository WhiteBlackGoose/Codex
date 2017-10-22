using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Codex.Sdk.Utilities;
using Codex.Storage.ElasticProviders;
using Codex.Storage.DataModel;

namespace Codex.ElasticSearch.Utilities
{
    public class MappingPropertyVisitor : NoopPropertyVisitor
    {
        public static readonly MappingPropertyVisitor Instance = new MappingPropertyVisitor();

        private static readonly IProperty IgnoredProperty = PropertyWalker.IgnoredPropertyInstance;

        private const DataInclusionOptions AlwaysInclude = DataInclusionOptions.None;

        public override IProperty Visit(PropertyInfo propertyInfo, ElasticsearchPropertyAttributeBase attribute)
        {
            var searchBehavior = propertyInfo.GetSearchBehavior();
            var dataInclusion = propertyInfo.GetDataInclusion() ?? AlwaysInclude;

            Placeholder.Todo("Verify mappings");
            Placeholder.Todo("Add properties for all search behaviors");
            if (searchBehavior.HasValue)
            {
                switch (searchBehavior.Value)
                {
                    case SearchBehavior.None:
                        return IgnoredProperty;
                    case SearchBehavior.Term:
                        break;
                    case SearchBehavior.NormalizedKeyword:
                        return new NormalizedKeywordAttribute();
                    case SearchBehavior.Sortword:
                        return new SortwordAttribute();
                    case SearchBehavior.HierarchicalPath:
                        return new HierachicalPathAttribute();
                    case SearchBehavior.FullText:
                        return new FullTextAttribute(dataInclusion);
                    case SearchBehavior.Prefix:
                        return new PrefixTextAttribute();
                    case SearchBehavior.PrefixFullName:
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            var property = base.Visit(propertyInfo, attribute);
            if (property is IObjectProperty)
            {
                if (((IObjectProperty)property).Properties.Count == 0)
                {
                    return IgnoredProperty;
                }
            }

            if (!(property is IObjectProperty) && !searchBehavior.HasValue)
            {
                return IgnoredProperty;
            }

            return property;
        }

        public override void Visit(IObjectProperty type, PropertyInfo propertyInfo, ElasticsearchPropertyAttributeBase attribute)
        {
            type.Properties.RemoveDisabledProperties();

            if (type.Properties.Count == 0)
            {
                type.Enabled = false;
            }
        }
    }
}
