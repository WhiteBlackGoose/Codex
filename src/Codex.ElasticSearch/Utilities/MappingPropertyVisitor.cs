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
using Codex.Serialization;
using System.Collections;

namespace Codex.ElasticSearch.Utilities
{
    public class MappingPropertyVisitor : NoopPropertyVisitor, IPropertyVisitor
    {
        public static readonly MappingPropertyVisitor Instance = new MappingPropertyVisitor();

        private static readonly IProperty IgnoredProperty = PropertyWalker.IgnoredPropertyInstance;
        private static readonly IProperty DisabledProperty = new ObjectProperty();

        public const string DisabledPropertyKey = "IsDisabled";

        private static IDictionary<string, object> DisabledLocalMetadata = new Dictionary<string, object>()
        {
            { DisabledPropertyKey, true }
        };

        private const DataInclusionOptions AlwaysInclude = DataInclusionOptions.None;

        public static IProperties GetProperties(Type type, int recursionDepth = 0)
        {
            var properties = new Properties();

            AddProperties(type, properties, recursionDepth);
            return properties;
        }

        public static T AddProperties<T>(Type type, T properties, int recursionDepth = 0)
            where T : IProperties
        {
            var allTypes = new[] { type }.Concat(type.GetInterfaces()).Distinct().ToList();
            AddProperties(allTypes, properties, recursionDepth);
            return properties;
        }

        private static void AddProperties(List<Type> allTypes, IProperties properties, int recursionDepth)
        {
            HashSet<string> names = new HashSet<string>();
            foreach (var type in allTypes)
            {
                foreach (var property in type.GetProperties().Where(p => names.Add(p.Name)))
                {
                    var elasticProperty = GetElasticProperty(property, recursionDepth + 1);
                    if (elasticProperty != null)
                    {
                        properties.Add(property, elasticProperty);
                    }
                }
            }
        }

        private static IProperty GetElasticProperty(PropertyInfo propertyInfo, int recursionDepth)
        {
            var allowedStages = propertyInfo.GetAllowedStages();

            if ((allowedStages & ObjectStage.Index) == 0)
            {
                return null;
            }

            var searchBehavior = propertyInfo.GetSearchBehavior();
            var dataInclusion = propertyInfo.GetDataInclusion() ?? AlwaysInclude;

            Placeholder.Todo("Verify mappings");
            Placeholder.Todo("Add properties for all search behaviors");
            if (searchBehavior.HasValue)
            {
                switch (searchBehavior.Value)
                {
                    case SearchBehavior.None:
                        return null;
                    case SearchBehavior.Term:
                        return PropertyWalker.InferProperty(propertyInfo.PropertyType);
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
                        return new PrefixFullTextTextAttribute();
                }
            }

            var underlyingType = GetUnderlyingType(propertyInfo.PropertyType);
            if (ElasticCodexTypeUtilities.Instance.IsEntityType(underlyingType))
            {
                ObjectProperty objectProperty = new ObjectProperty() { Properties = new Properties() };
                AddProperties(underlyingType, objectProperty.Properties, recursionDepth: recursionDepth);

                if (objectProperty.Properties.Count != 0)
                {
                    return objectProperty;
                }
            }

            return null;
        }

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
                        return null;
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
                        break;
                }
            }

            if (ElasticCodexTypeUtilities.Instance.IsEntityType(GetUnderlyingType(propertyInfo.PropertyType)))
            {
                // Infer's object
                return null;
            }

            return IgnoredProperty;
        }

        void IPropertyVisitor.Visit(IProperty type, PropertyInfo propertyInfo, ElasticsearchPropertyAttributeBase attribute)
        {
            var searchBehavior = propertyInfo.GetSearchBehavior();
            var dataInclusion = propertyInfo.GetDataInclusion() ?? AlwaysInclude;
            Placeholder.Todo("Add properties for all search behaviors");
            var allowedStages = propertyInfo.GetAllowedStages();

            if ((allowedStages & ObjectStage.Index) == 0)
            {
                DisableProperty(type);
            }

            if (searchBehavior.HasValue)
            {
                switch (searchBehavior.Value)
                {
                    case SearchBehavior.Term:
                    case SearchBehavior.NormalizedKeyword:
                    case SearchBehavior.Sortword:
                    case SearchBehavior.HierarchicalPath:
                    case SearchBehavior.FullText:
                    case SearchBehavior.Prefix:
                    case SearchBehavior.PrefixFullName:
                        break;
                    case SearchBehavior.None:
                        DisableProperty(type);
                        break;
                    default:
                        if (!(type is IObjectProperty))
                        {
                            DisableProperty(type);
                        }
                        break;
                }
            }
            else if (!(type is IObjectProperty))
            {
                DisableProperty(type);
            }
            else
            {
                Visit((IObjectProperty)type, propertyInfo, attribute);
            }
        }

        private static void DisableProperty(IProperty type)
        {
            type.LocalMetadata = DisabledLocalMetadata;
        }

        public override void Visit(IObjectProperty type, PropertyInfo propertyInfo, ElasticsearchPropertyAttributeBase attribute)
        {
            type.Properties.RemoveDisabledProperties();

            if (type.Properties.Count == 0)
            {
                DisableProperty(type);
            }
        }

        private static Type GetUnderlyingType(Type type)
        {
            if (type.IsArray)
                return type.GetElementType();

            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsGenericType && type.GetGenericArguments().Length == 1
                && (typeInfo.ImplementedInterfaces.Any(t => t == typeof(IEnumerable)) || Nullable.GetUnderlyingType(type) != null))
                return type.GetGenericArguments()[0];

            return type;
        }
    }
}
