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
            HashSet<TypeDefinition> visitedTypeDefinitions = new HashSet<TypeDefinition>();

            CodeCompileUnit searchDescriptors = new CodeCompileUnit();
            CodeNamespace searchDescriptorsNamespace = new CodeNamespace("Codex.Framework.Types");
            searchDescriptors.Namespaces.Add(searchDescriptorsNamespace);

            CodeTypeDeclaration indexTypeDeclaration = new CodeTypeDeclaration(nameof(Index));

            foreach (var typeDefinition in Types)
            {
                if (typeof(ISearchEntity).IsAssignableFrom(typeDefinition.Type))
                {
                    indexTypeDeclaration.Members.Add(new CodeMemberField(typeDefinition.SearchDescriptorName, typeDefinition.SearchDescriptorName));


                    CodeTypeDeclaration typeDeclaration = new CodeTypeDeclaration(typeDefinition.SearchDescriptorName);
                    searchDescriptorsNamespace.Types.Add(typeDeclaration);

                    var constructor = new CodeConstructor();
                    constructor.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Index), "index"));
                    typeDeclaration.Members.Add(constructor);

                    visitedTypeDefinitions.Clear();
                    PopulateProperties(visitedTypeDefinitions, new CodeTypeReference(typeDeclaration.Name), typeDefinition, typeDeclaration);
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

        private void PopulateProperties(HashSet<TypeDefinition> visitedTypeDefinitions, CodeTypeReference searchType, TypeDefinition typeDefinition, CodeTypeDeclaration typeDeclaration)
        {
            if (visitedTypeDefinitions.Add(typeDefinition))
            {
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
                        PopulateProperties(visitedTypeDefinitions, searchType, property.PropertyTypeDefinition, typeDeclaration);
                    }
                }

                foreach (var type in typeDefinition.Type.GetInterfaces())
                {
                    TypeDefinition baseDefinition;
                    if (DefinitionsByType.TryGetValue(type, out baseDefinition))
                    {
                        PopulateProperties(visitedTypeDefinitions, searchType, baseDefinition, typeDeclaration);
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
}
