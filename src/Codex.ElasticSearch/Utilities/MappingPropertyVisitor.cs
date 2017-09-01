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
        private static readonly IObjectProperty DisabledProperty = new ObjectProperty() { Enabled = false };

        private const DataInclusionOptions AlwaysInclude = DataInclusionOptions.None;

        public override IProperty Visit(PropertyInfo propertyInfo, ElasticsearchPropertyAttributeBase attribute)
        {
            var searchBehavior = propertyInfo.GetSearchBehavior();
            var dataInclusion = propertyInfo.GetDataInclusion() ?? AlwaysInclude;
            if (searchBehavior != null)
            {
                // TODO: Add properties for all search behaviors
                switch (searchBehavior.Value)
                {
                    case SearchBehavior.None:
                        break;
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
