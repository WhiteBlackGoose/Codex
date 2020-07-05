using Codex.ObjectModel;
using Codex.Utilities;
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
            (SearchBehavior == null && PropertyTypeDefinition == null) ||
            PropertyTypeDefinition?.IsSearchRelevant == false;

        #region Builder

        public CodeMemberField BuilderField;
        public CodeMemberProperty BuilderProperty;
        public CodeMemberProperty BuilderImplementationProperty;

        public CodeStatementCollection BuilderApplyMethodStatements { get; } = new CodeStatementCollection();

        #endregion

        #region Mapping

        public CodeTypeMemberCollection MappingClassMembers { get; } = new CodeTypeMemberCollection();

        public List<CodeStatement> MappingVisitMethodStatements { get; } = new List<CodeStatement>();

        #endregion

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

        public void GenerateBuilder()
        {
            BuilderField = new CodeMemberField()
            {
                Name = BackingFieldName,
                Attributes = MemberAttributes.Private,
                Type = CoercedSourceType?.AsReference() ?? MutablePropertyType,
            };

            if (InitPropertyType != null)
            {
                if (IsReadOnlyList)
                {
                    BuilderField.InitExpression = new CodePropertyReferenceExpression(
                        new CodeTypeReferenceExpression(typeof(CollectionUtilities.Empty<>).MakeGenericTypeReference(MutablePropertyType.TypeArguments[0])),
                        nameof(CollectionUtilities.Empty<int>.Array));
                }
                else
                {
                    BuilderField.InitExpression = new CodeObjectCreateExpression(InitPropertyType);
                }
            }

            if (PropertyTypeDefinition != null || IsList)
            {
                BuilderImplementationProperty = new CodeMemberProperty()
                {
                    Type = ImmutablePropertyType,
                    Name = Name,
                    HasGet = true,
                    PrivateImplementationType = PropertyInfo.DeclaringType.AsReference()
                }
                .AddComments(Comments)
                .Apply(p => p.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), Name))));
            }

            string coercePropertyMethodName = $"Coerce{Name}";

            BuilderProperty = new CodeMemberProperty()
            {
                Type = MutablePropertyType,
                Name = Name,
                Attributes = MemberAttributes.Public,
                HasGet = true,
            }
            .AddComments(Comments)
            .Apply(p => p.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), BuilderField.Name)
                .ApplyIf<CodeExpression>(CoerceGet, exp => new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), coercePropertyMethodName, exp)))))
            .Apply(p =>
            {
                p.HasSet = true;
                p.SetStatements.Add(new CodeAssignStatement(left: new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), BuilderField.Name), right: new CodePropertySetValueReferenceExpression()));
            });

            BuilderApplyMethodStatements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), BackingFieldName),
                GetPropertyApplyExpression()));
        }

        private CodeExpression GetPropertyApplyExpression()
        {
            //var valuePropertyReferenceExpression = new CodeFieldReferenceExpression(new CodeCastExpression(PropertyInfo.DeclaringType.AsReference(), new CodeVariableReferenceExpression("value")), Name);
            var valuePropertyReferenceExpression = new CodeFieldReferenceExpression(new CodeVariableReferenceExpression("value"), Name);
            if (!IsList)
            {
                if (PropertyTypeDefinition == null)
                {
                    // value.Property;
                    return valuePropertyReferenceExpression;
                }
                else
                {
                    // new PropertyType().CopyFrom(value.Property);
                    return new CodeSnippetExpression($"EntityUtilities.NullOrCopy(value.{valuePropertyReferenceExpression.FieldName}, v => new {PropertyTypeDefinition.ClassName}().Apply(v));");
                }
            }
            else
            {

                if (PropertyTypeDefinition == null)
                {
                    // new List<PropertyType>(value.Property);
                    return new CodeObjectCreateExpression(InitPropertyType,
                        valuePropertyReferenceExpression);
                }
                else
                {
                    // new List<PropertyType>(value.Select(v => new PropertyType().CopyFrom(v)));
                    return new CodeObjectCreateExpression(InitPropertyType,
                        new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(Enumerable)), nameof(Enumerable.Select)),
                        valuePropertyReferenceExpression,
                            new CodeSnippetExpression($"v => EntityUtilities.NullOrCopy(v, _v => new {PropertyTypeDefinition.ClassName}().Apply(_v))")));
                }

            }
        }
    }
}
