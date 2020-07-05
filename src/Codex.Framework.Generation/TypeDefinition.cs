using Codex.ObjectModel;
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

        public bool IsSearchRelevant;

        public bool Migrated;

        public Type Type;

        public List<PropertyDefinition> Properties;
        public List<PropertyDefinition> GeneratedProperties;

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

        public TypeDefinition SearchRelevantBase
        {
            get
            {
                var type = BaseTypeDefinition;
                while (type != null)
                {
                    if (type.IsSearchRelevant) return type;
                    type = type.BaseTypeDefinition;
                }

                return null;
            }
        }

        public bool IsAdapter;
        public bool Exclude;

        public List<TypeDefinition> Interfaces = new List<TypeDefinition>();
        public List<Type> AllInterfaces = new List<Type>();
        public SearchType SearchType;

        public CodeTypeReference MappingTypeReference;

        public readonly CodeTypeDeclaration BuilderTypeDeclaration;

        public readonly CodeTypeDeclaration MappingTypeDeclaration;

        public CodeMemberProperty MappingIndexer = Generator.CreateMappingIndexer();
        public readonly CodeMemberMethod MappingVisitMethod;

        public TypeDefinition(Type type)
        {
            Type = type;

            // Remove leading I from interface name
            ExplicitClassName = type.GetAttribute<GeneratedClassNameAttribute>()?.Name;
            ClassName = ExplicitClassName ?? type.GetAttribute<GeneratedClassNameAttribute>()?.Name ?? type.Name.Substring(1);
            BaseName = ClassName.Replace("SearchModel", "");
            SearchDescriptorName = BaseName + "IndexDescriptor";
            BuilderClassName = ClassName + "Builder";
            AllowedStages = type.GetAllowedStages();
            IsAdapter = type.GetAttribute<AdapterTypeAttribute>() != null;
            Exclude = type.GetAttribute<GeneratorExcludeAttribute>() != null;

            MappingTypeDeclaration = new CodeTypeDeclaration(ClassName + "Mapping")
            {
                IsClass = true,
                IsPartial = true,
                TypeParameters =
                {
                    new CodeTypeParameter("TRoot")
                }
            };

            BuilderTypeDeclaration = new CodeTypeDeclaration(ClassName)
            {
                IsClass = true,
                IsPartial = true
            }.AddComments(Comments.ToArray());

            BuilderTypeDeclaration.CustomAttributes.Add(
                    new CodeAttributeDeclaration(
                        typeof(SerializationInterfaceAttribute).AsReference(),
                        new CodeAttributeArgument(new CodeTypeOfExpression(Type))));

            MappingTypeReference = new CodeTypeReference(ClassName + "Mapping", new CodeTypeReference("TRoot"));
            Properties = type.GetProperties().Select(p => new PropertyDefinition(p)).ToList();

            MappingVisitMethod = new CodeMemberMethod()
            {
                Name = nameof(IMapping<bool>.Visit),
                Attributes = MemberAttributes.Public,
                Parameters =
                    {
                        new CodeParameterDeclarationExpression(typeof(ObjectModel.IVisitor), "visitor"),
                        new CodeParameterDeclarationExpression(Type, "value")
                    }
            };

            MappingVisitMethod.Statements.Add(new CodeConditionStatement(
                new CodeBinaryOperatorExpression(
                    new CodeArgumentReferenceExpression("value"),
                    CodeBinaryOperatorType.IdentityEquality,
                    new CodePrimitiveExpression(null)), new CodeMethodReturnStatement()));
        }

        public void GenerateProperties()
        {
            foreach (var property in Properties)
            {
                property.GenerateBuilder();
            }
        }

        public void GenerateBuilder()
        {
            // Generate constructors

            if (BaseTypeDefinition != null)
            {
                BuilderTypeDeclaration.BaseTypes.Add(BaseTypeDefinition.ClassName);
            }
            else if (!IsAdapter)
            {
                BuilderTypeDeclaration.BaseTypes.Add(typeof(EntityBase));
            }

            BuilderTypeDeclaration.BaseTypes.Add(Type);

            // Add properties
            foreach (var property in GeneratedProperties)
            {
                BuilderTypeDeclaration.Members.Add(property.BuilderField);
                BuilderTypeDeclaration.Members.Add(property.BuilderProperty);
                property.BuilderImplementationProperty?.Apply(m => BuilderTypeDeclaration.Members.Add(m));
            }

            // Add empty constructor
            BuilderTypeDeclaration.Members.Add(new CodeConstructor()
            {
                Attributes = MemberAttributes.Public
            });

            // Add copy constructors
            foreach (var type in new[] { Type }.Concat(AllInterfaces).Except(new[] { typeof(ISearchEntityBase) }))
            {
                var copyConstructor = new CodeConstructor()
                {
                    Attributes = MemberAttributes.Public
                };

                copyConstructor.Parameters.Add(new CodeParameterDeclarationExpression(
                    type.AsReference(),
                    "value"));

                copyConstructor.Statements.Add(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                    new CodeThisReferenceExpression(), nameof(IEntityTarget<object>.CopyFrom)),
                    new CodeVariableReferenceExpression("value")));

                BuilderTypeDeclaration.Members.Add(copyConstructor);
            }

            // Add apply methods
            foreach (var type in new[] { this }.Concat(Interfaces))
            {
                BuilderTypeDeclaration.BaseTypes.Add(typeof(IEntityTarget<>).MakeGenericTypeReference(type.Type.AsReference()));

                var applyMethod = new CodeMemberMethod()
                {
                    Name = nameof(IEntityTarget<object>.CopyFrom),
                    Attributes = MemberAttributes.Public,
                };

                applyMethod.Parameters.Add(new CodeParameterDeclarationExpression(
                    type.Type.AsReference(),
                    "value"));


                BuilderTypeDeclaration.Members.Add(applyMethod);

                foreach (var property in type.GeneratedProperties)
                {
                    applyMethod.Statements.AddRange(property.BuilderApplyMethodStatements);
                }

                if (type.BaseTypeDefinition != null)
                {
                    applyMethod.Statements.Add(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(
                        type == this ? (CodeExpression)new CodeBaseReferenceExpression() : new CodeThisReferenceExpression(),
                        applyMethod.Name),
                        new CodeCastExpression(type.BaseTypeDefinition.Type, new CodeVariableReferenceExpression("value"))));
                }
            }
        }

        public void GenerateMapping()
        {
            // Add properties
            foreach (var property in GeneratedProperties)
            {
                if (property.ExcludeFromIndexing) continue;

                MappingTypeDeclaration.AddMappingProperty(
                        property.Name,
                        property.PropertyTypeDefinition?.MappingTypeReference ?? property.PropertyType.AsReference().AsMappingType(),
                        MappingIndexer,
                        property.SearchBehavior,
                        MappingVisitMethod,
                        property.AllowedStages);
            }

            // Generate visitor
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
