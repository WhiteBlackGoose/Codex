using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System;
using System.Collections.Generic;
using System.CodeDom;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        }

        public void LoadTypeInformation()
        {
            var interfaces = typeof(ObjectStage)
                .Assembly
                .GetTypes()
                .Where(t => t.IsInterface)
                .Select(ToTypeDefinition);
        }

        private TypeDefinition ToTypeDefinition(Type type)
        {
            return new TypeDefinition(type);
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
