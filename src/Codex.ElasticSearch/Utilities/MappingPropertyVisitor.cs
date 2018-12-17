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
    public static class MappingPropertyVisitor
    {
        private const DataInclusionOptions AlwaysInclude = DataInclusionOptions.None;

        private static Func<PropertyInfo, IProperty> PropertyWalkerInferProperty = EntityReflectionHelpers.
            CreateStaticMethodCall<PropertyInfo, IProperty>(typeof(PropertyWalker), "InferProperty");

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
                        if (propertyInfo.PropertyType == typeof(string))
                        {
                            return new CodexKeywordAttribute();
                        }
                        return PropertyWalkerInferProperty(propertyInfo);
                    case SearchBehavior.NormalizedKeyword:
                        return new NormalizedKeywordAttribute();
                    case SearchBehavior.Sortword:
                        return new SortwordAttribute();
                    case SearchBehavior.HierarchicalPath:
                        return new HierarchicalPathAttribute();
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
