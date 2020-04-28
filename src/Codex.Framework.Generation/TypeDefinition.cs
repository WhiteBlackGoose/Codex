using Microsoft.CodeAnalysis;
using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Framework.Generation
{
    class TypeDefinition
    {
        public ObjectStage AllowedStages;

        public bool Migrated;

        public Type Type;

        public List<PropertyDefinition> Properties;

        public string ExplicitClassName;
        public string ClassName;
        public string BaseName;
        public string BuilderClassName;
        public string ApplyMethodName => $"Apply{ClassName}";

        public string SearchDescriptorName;

        public List<CodeCommentStatement> Comments = new List<CodeCommentStatement>();

        public List<CodeTypeReference> TypeParameters = new List<CodeTypeReference>();

        public INamedTypeSymbol TypeSymbol;
        public Type BaseType;
        public TypeDefinition BaseTypeDefinition;
        public bool IsAdapter;

        public List<TypeDefinition> Interfaces = new List<TypeDefinition>();
        public SearchType SearchType;

        public CodeTypeReference MappingTypeReference;

        public readonly CodeTypeDeclaration MappingTypeDeclaration;

        public TypeDefinition(Type type)
        {
            Type = type;
            if (type == typeof(ICodeSymbol))
            {

            }

            // Remove leading I from interface name
            ExplicitClassName = type.GetAttribute<GeneratedClassNameAttribute>()?.Name;
            ClassName = ExplicitClassName ?? type.GetAttribute<GeneratedClassNameAttribute>()?.Name ?? type.Name.Substring(1);
            BaseName = ClassName.Replace("SearchModel", "");
            SearchDescriptorName = BaseName + "IndexDescriptor";
            BuilderClassName = ClassName + "Builder";
            AllowedStages = type.GetAllowedStages();
            IsAdapter = type.GetAttribute<AdapterTypeAttribute>() != null;

            MappingTypeDeclaration = new CodeTypeDeclaration(ClassName + "Mapping")
            {
                IsClass = true,
                IsPartial = true,
                TypeParameters =
                {
                    new CodeTypeParameter("TRoot")
                }
            };

            MappingTypeReference = new CodeTypeReference(ClassName + "Mapping", new CodeTypeReference("TRoot"));
            Properties = type.GetProperties().Select(p => new PropertyDefinition(p)).ToList();
        }
    }

    class PropertyDefinition
    {
        public ObjectStage AllowedStages;

        public SearchBehavior? SearchBehavior;

        public string Name;
        public string BackingFieldName;

        public PropertyInfo PropertyInfo;

        public Type PropertyType;

        public Type CoercedSourceType;

        public TypeDefinition PropertyTypeDefinition;

        public CodeTypeReference ImmutablePropertyType;
        public CodeTypeReference MutablePropertyType;
        public CodeTypeReference InitPropertyType;

        public List<CodeCommentStatement> Comments = new List<CodeCommentStatement>();

        public bool Inline;

        public bool CoerceGet;

        public bool IsList;
        public bool IsReadOnlyList;

        public bool ExcludeFromIndexing => SearchBehavior == Codex.SearchBehavior.None ||
            (SearchBehavior == null && PropertyTypeDefinition == null);

        public PropertyDefinition(PropertyInfo propertyInfo)
        {
            Name = propertyInfo.Name;
            BackingFieldName = $"m_{Name}";
            PropertyInfo = propertyInfo;
            AllowedStages = propertyInfo.GetAllowedStages();
            SearchBehavior = propertyInfo.GetSearchBehavior();
            IsList = propertyInfo.PropertyType.IsGenericType && propertyInfo.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>);
            IsReadOnlyList = IsList && propertyInfo.GetAttribute<ReadOnlyListAttribute>() != null;
            Inline = propertyInfo.GetInline();
            CoerceGet = propertyInfo.GetAttribute<CoerceGetAttribute>() != null;
            CoercedSourceType = propertyInfo.GetAttribute<CoerceGetAttribute>()?.CoercedSourceType;
            PropertyType = IsList ?
                PropertyInfo.PropertyType.GenericTypeArguments[0] :
                PropertyInfo.PropertyType;
        }
    }

    public static class Helpers
    {
        public static string GetMetadataName(this Type type)
        {
            if (type.IsGenericType)
            {
                return type.GetGenericTypeDefinition().FullName;
            }

            return type.FullName;
        }

        public static void AddComments(this List<CodeCommentStatement> comments, string commentsText)
        {
            if (string.IsNullOrEmpty(commentsText))
            {
                return;
            }

            var reader = new StringReader(commentsText);
            string line = null;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                comments.Add(new CodeCommentStatement(line.Trim(), true));
            }
        }

        public static bool GetInline(this MemberInfo type)
        {
            var attribute = type.GetAttribute<SearchDescriptorInlineAttribute>();
            return attribute?.Inline ?? true;
        }

        public static ObjectStage GetAllowedStages(this MemberInfo type)
        {
            var attribute = type.GetAttribute<IncludeAttribute>();
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
