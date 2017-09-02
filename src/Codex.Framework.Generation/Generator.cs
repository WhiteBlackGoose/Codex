using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System;
using System.Collections.Generic;
using System.CodeDom;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Codex.Framework.Types;

namespace Codex.Framework.Generation
{
    class Generator
    {
        public CSharpCodeProvider CodeProvider;

        public List<TypeDefinition> Types = new List<TypeDefinition>();
        public Dictionary<Type, TypeDefinition> DefinitionsByType = new Dictionary<Type, TypeDefinition>();

        public Generator()
        {
            CodeProvider = new CSharpCodeProvider();
        }

        public void Generate(string path)
        {
            LoadTypeInformation();
            GenerateBuilders();
            GenerateSearchDescriptors();
        }

        public void LoadTypeInformation()
        {
            Types = typeof(ObjectStage)
                .Assembly
                .GetTypes()
                .Where(t => t.IsInterface)
                //.Where(t => t.IsAssignableFrom(typeof(ISearchEntity)))
                .Select(ToTypeDefinition)
                .ToList();

            foreach (var typeDefinition in Types)
            {
                DefinitionsByType[typeDefinition.Type] = typeDefinition;
            }

            foreach (var typeDefinition in Types)
            {
                foreach (var property in typeDefinition.Properties)
                {
                    DefinitionsByType.TryGetValue(property.PropertyInfo.PropertyType, out property.PropertyTypeDefinition);
                }
            }
        }

        private TypeDefinition ToTypeDefinition(Type type)
        {
            return new TypeDefinition(type);
        }

        private void GenerateSearchDescriptors()
        {
            HashSet<TypeDefinition> visitedSearchTypeDefinitions = new HashSet<TypeDefinition>();
            HashSet<TypeDefinition> visitedTypeDefinitions = new HashSet<TypeDefinition>();
            HashSet<string> usedMemberNames = new HashSet<string>();

            CodeCompileUnit searchDescriptors = new CodeCompileUnit();
            CodeNamespace searchDescriptorsNamespace = new CodeNamespace("Codex.Framework.Types");
            searchDescriptors.Namespaces.Add(searchDescriptorsNamespace);
            searchDescriptorsNamespace.Imports.Add(new CodeNamespaceImport(typeof(Task<>).Namespace));

            CodeTypeDeclaration indexTypeDeclaration = new CodeTypeDeclaration(nameof(IIndex))
            {
                IsPartial = true,
                IsInterface = true
            };

            CodeTypeDeclaration storeTypeDeclaration = new CodeTypeDeclaration(nameof(IStore))
            {
                IsPartial = true,
                IsInterface = true
            };

            CodeTypeDeclaration storeBaseTypeDeclaration = new CodeTypeDeclaration("StoreBase")
            {
                IsPartial = true,
                IsClass = true,
                TypeAttributes = System.Reflection.TypeAttributes.Abstract | System.Reflection.TypeAttributes.Public,
            };

            var storeBaseInitialize = new CodeMemberMethod()
            {
                Name = "InitializeAsync",
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(Task))
            }.AsyncMethod();


            var storeBaseFinalize = new CodeMemberMethod()
            {
                Name = "FinalizeAsync",
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference(typeof(Task))
            }.AsyncMethod();

            var storeBaseCreateStore = new CodeMemberMethod()
            {
                Name = "CreateStoreAsync",
                Attributes = MemberAttributes.Public | MemberAttributes.Abstract,
                ReturnType = new CodeTypeReference("Task", new CodeTypeReference(nameof(IStore<ISearchEntity>), new CodeTypeReference(new CodeTypeParameter("TSearchType")))),
                
            }
            .Apply(m => m.TypeParameters.Add(new CodeTypeParameter("TSearchType").WithClassConstraint()))
            .Apply(m => m.Parameters.Add(new CodeParameterDeclarationExpression(typeof(SearchType), "searchType")));

            storeBaseTypeDeclaration.Members.Add(storeBaseInitialize);
            storeBaseTypeDeclaration.Members.Add(storeBaseFinalize);
            storeBaseTypeDeclaration.Members.Add(storeBaseCreateStore);

            searchDescriptorsNamespace.Types.Add(storeTypeDeclaration);
            searchDescriptorsNamespace.Types.Add(storeBaseTypeDeclaration);

            foreach (var searchType in SearchTypes.RegisteredSearchTypes)
            {
                var typeDefinition = DefinitionsByType[searchType.Type];
                indexTypeDeclaration.Members.Add(new CodeMemberField(typeDefinition.SearchDescriptorName, typeDefinition.SearchDescriptorName));

                var typedStoreProperty = new CodeMemberProperty()
                {
                    Type = new CodeTypeReference(typeof(IStore<>).MakeGenericType(searchType.Type)),
                    Name = searchType.Name + "Store",
                    HasGet = true
                };

                storeTypeDeclaration.Members.Add(typedStoreProperty);

                var typedStoreField = new CodeMemberField(typedStoreProperty.Type, $"m_{typedStoreProperty.Name}")
                {
                    Attributes = MemberAttributes.Private
                };

                storeBaseTypeDeclaration.Members.Add(typedStoreField);

                storeBaseTypeDeclaration.Members.Add(new CodeMemberProperty()
                {
                    Type = typedStoreProperty.Type,
                    Name = typedStoreProperty.Name,
                    HasGet = true,
                }.Apply(p => p.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), typedStoreField.Name)))));

                storeBaseInitialize.Statements.Add(new CodeAssignStatement(
                    new CodeVariableReferenceExpression(typedStoreField.Name),
                    new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), storeBaseCreateStore.Name, new CodeTypeReference(searchType.Type)),
                        new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(typeof(SearchTypes)), searchType.Name)).AwaitExpression()));

                storeBaseFinalize.Statements.Add(
                    new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeVariableReferenceExpression(typedStoreField.Name), "FinalizeAsync")).AwaitExpression());

                if (visitedSearchTypeDefinitions.Add(typeDefinition))
                {
                    CodeTypeDeclaration typeDeclaration = new CodeTypeDeclaration(typeDefinition.SearchDescriptorName);
                    //searchDescriptorsNamespace.Types.Add(typeDeclaration);

                    var constructor = new CodeConstructor();
                    constructor.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Index<bool>), "index"));
                    typeDeclaration.Members.Add(constructor);

                    visitedTypeDefinitions.Clear();
                    usedMemberNames.Clear();

                    PopulateProperties(visitedTypeDefinitions, usedMemberNames, new CodeTypeReference(typeDeclaration.Name), typeDefinition, typeDeclaration);
                }
            }

            using (var writer = new StreamWriter("SearchDescriptors.g.cs"))
            {
                CodeProvider.GenerateCodeFromCompileUnit(searchDescriptors, writer, new System.CodeDom.Compiler.CodeGeneratorOptions()
                {
                    BlankLinesBetweenMembers = true,
                    IndentString = "    "
                });
            }
        }

        private void PopulateProperties(
            HashSet<TypeDefinition> visitedTypeDefinitions,
            HashSet<string> usedMemberNames,
            CodeTypeReference searchType,
            TypeDefinition typeDefinition,
            CodeTypeDeclaration typeDeclaration)
        {
            if (visitedTypeDefinitions.Add(typeDefinition))
            {
                foreach (var type in typeDefinition.Type.GetInterfaces())
                {
                    TypeDefinition baseDefinition;
                    if (DefinitionsByType.TryGetValue(type, out baseDefinition))
                    {
                        PopulateProperties(visitedTypeDefinitions, usedMemberNames, searchType, baseDefinition, typeDeclaration);
                    }
                }

                foreach (var property in typeDefinition.Properties)
                {
                    if (property.SearchBehavior != null)
                    {
                        if (property.SearchBehavior != SearchBehavior.None)
                        {
                            CodeMemberField codeProperty = new CodeMemberField()
                            {
                                Name = property.Name,
                                Attributes = MemberAttributes.Public,
                                Type = new CodeTypeReference(property.SearchBehavior + "IndexProperty", searchType),
                            };

                            typeDeclaration.Members.Add(codeProperty);
                        }
                    }
                    else if (property.PropertyTypeDefinition != null)
                    {
                        if (!property.Inline)
                        {
                            var nestedTypeDeclaration = new CodeTypeDeclaration(property.PropertyTypeDefinition.SearchDescriptorName);
                            typeDeclaration.Members.Add(nestedTypeDeclaration);

                            CodeMemberField codeProperty = new CodeMemberField()
                            {
                                Name = property.Name,
                                Attributes = MemberAttributes.Public,
                                Type = new CodeTypeReference(property.PropertyTypeDefinition.SearchDescriptorName),
                            };

                            typeDeclaration.Members.Add(codeProperty);
                            typeDeclaration = nestedTypeDeclaration;
                        }

                        PopulateProperties(visitedTypeDefinitions, usedMemberNames, searchType, property.PropertyTypeDefinition, typeDeclaration);
                    }
                }
            }
        }

        public void GenerateBuilders()
        {

        }

        public void GenerateBuilder(TypeDefinition type)
        {
            CodeTypeDeclaration builder = new CodeTypeDeclaration(type.ClassName + "Builder");
            builder.IsClass = true;
            builder.IsPartial = true;
            // WIP: Private class nested in Builder type 
            builder.Attributes = MemberAttributes.Private;

            var constructor = new CodeConstructor()
            {
                Name = builder.Name,
                Attributes = MemberAttributes.Public,
            };

            builder.Members.Add(constructor);

            foreach (var property in type.Properties)
            {
                var propertyDeclaration = new CodeMemberProperty()
                {
                    Name = property.Name,
                    Attributes = MemberAttributes.Public,
                    HasGet = true,
                    HasSet = !property.IsList,
                    Type = GetBuilderPropertyTypeName(property.PropertyInfo.PropertyType)
                };

                // Create the lists in the constructor
                if (property.IsList)
                {
                    constructor.Statements.Add(
                        new CodeObjectCreateExpression(propertyDeclaration.Type));
                }

                builder.Members.Add(propertyDeclaration);
            }
        }

        public CodeTypeReference GetBuilderPropertyTypeName(Type type)
        {
            TypeDefinition typeDefinition;
            if (type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                return new CodeTypeReference("List", GetBuilderPropertyTypeName(type.GetGenericArguments()[0]));
            }
            else if (DefinitionsByType.TryGetValue(type, out typeDefinition))
            {
                return new CodeTypeReference(typeDefinition.BuilderClassName);
            }
            else
            {
                return new CodeTypeReference(type);
            }
        }
    }

    public static class CodeDomHelper
    {
        public static T Apply<T>(this T codeObject, Action<T> action)
            where T : CodeObject
        {
            action(codeObject);
            return codeObject;
        }

        public static CodeTypeParameter WithClassConstraint(this CodeTypeParameter ct)
        {
            ct.Constraints.Add(" class");
            return ct;
        }

        public static CodeMemberMethod AsyncMethod(this CodeMemberMethod method)
        {
            var returnTypeArgumentReferences = method.ReturnType.TypeArguments.OfType<CodeTypeReference>().ToArray();

            var asyncReturnType = new CodeTypeReference($"async {method.ReturnType.BaseType}", returnTypeArgumentReferences);
            method.ReturnType = asyncReturnType;
            return method;
        }

        public static CodeMethodInvokeExpression AwaitExpression(this CodeMethodInvokeExpression expression)
        {
            var variableExpression = expression.Method.TargetObject as CodeVariableReferenceExpression;
            expression.Method.TargetObject = new CodeVariableReferenceExpression($"await {variableExpression?.VariableName ?? "this"}");
            return expression;
        }
    }
}
