using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Generation
{
    class TypeDefinition
    {
        public ObjectStage AllowedStages;

        public Type Type;

        public List<PropertyDefinition> Properties;

        public string ClassName;
        public string BaseName;
        public string BuilderClassName;

        public string SearchDescriptorName;

        public TypeDefinition(Type type)
        {
            Type = type;

            // Remove leading I from interface name
            ClassName = type.Name.Substring(1);
            BaseName = ClassName.Replace("SearchModel", "");
            SearchDescriptorName = BaseName + "IndexDescriptor";
            BuilderClassName = ClassName + "Builder";
            AllowedStages = type.GetAllowedStages();

            Properties = type.GetProperties().Select(p => new PropertyDefinition(p)).ToList();
        }
    }

    class PropertyDefinition
    {
        public ObjectStage AllowedStages;

        public SearchBehavior? SearchBehavior;

        public string Name;

        public PropertyInfo PropertyInfo;

        public TypeDefinition PropertyTypeDefinition;

        public bool IsList;

        public PropertyDefinition(PropertyInfo propertyInfo)
        {
            Name = propertyInfo.Name;
            PropertyInfo = propertyInfo;
            AllowedStages = propertyInfo.GetAllowedStages();
            SearchBehavior = propertyInfo.GetSearchBehavior();
            IsList = propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);
        }

    }

    public static class Helpers
    {
        public static ObjectStage GetAllowedStages(this MemberInfo type)
        {
            var attribute = type.GetAttribute<RestrictedAttribute>();
            return attribute?.AllowedStages ?? ObjectStage.All;
        }

        public static SearchBehavior? GetSearchBehavior(this MemberInfo type)
        {
            var attribute = type.GetAttribute<SearchBehaviorAttribute>();
            return attribute?.Behavior;
        }

        public static T GetAttribute<T>(this MemberInfo type)
        {
            return type.GetCustomAttributes(typeof(T), inherit: false).OfType<T>().FirstOrDefault();
        }
    }
}
