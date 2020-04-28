using Microsoft.CodeDom.Providers.DotNetCompilerPlatform;
using System;
using System.Collections.Generic;
using System.CodeDom;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Codex.Framework.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.CompilerServices;
using Codex.Utilities;
using Codex.Sdk.Search;
using System.Diagnostics;

namespace Codex.Framework.Generation
{
    class Generator
    {
        public CSharpCodeProvider CodeProvider;

        public List<TypeDefinition> Types = new List<TypeDefinition>();
        public List<StoreGenerator> StoreGenerators = new List<StoreGenerator>();
        public HashSet<Type> MigratedTypes = new HashSet<Type>();
        public Dictionary<Type, TypeDefinition> DefinitionsByType = new Dictionary<Type, TypeDefinition>();
        public Dictionary<string, TypeDefinition> DefinitionsByTypeMetadataName = new Dictionary<string, TypeDefinition>();
        private CSharpCompilation Compilation;
        private string ProjectDirectory;
        private CodeTypeDeclaration CodexTypeUtilitiesClass = new CodeTypeDeclaration("CodexTypeUtilities")
        {
            IsClass = true,
            IsPartial = true,
            Attributes = MemberAttributes.Static
        };

        private CodeTypeDeclaration MappingsClass = new CodeTypeDeclaration("Mappings")
        {
            IsClass = true,
            IsPartial = true,
        };

        private CodeTypeDeclaration ClientInterface = new CodeTypeDeclaration("IClient")
        {
            IsInterface = true,
            IsPartial = true,
        };

        private SymbolDisplayFormat TypeNameDisplayFormat = new SymbolDisplayFormat(genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

        public Generator([CallerFilePath] string filePath = null)
        {
            ProjectDirectory = Path.GetDirectoryName(filePath);
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
            var assembly = typeof(ObjectStage).Assembly;
            CSharpCommandLineArguments arguments = CSharpCommandLineParser.Default.Parse(File.ReadAllLines("csc.args.txt"), ProjectDirectory, null);

            MigratedTypes.Add(typeof(ISearchEntity));
            MigratedTypes.Add(typeof(ICodeSymbol));
            MigratedTypes.Add(typeof(IReferenceSymbol));
            MigratedTypes.Add(typeof(ISpan));
            MigratedTypes.Add(typeof(IClassificationSpan));
            MigratedTypes.Add(typeof(IReferenceSpan));
            MigratedTypes.Add(typeof(IDefinitionSpan));
            MigratedTypes.Add(typeof(ISymbolSpan));
            //MigratedTypes.Add(typeof(ILineSpan));
            MigratedTypes.Add(typeof(IClassificationStyle));
            MigratedTypes.Add(typeof(IDefinitionSymbol));
            MigratedTypes.Add(typeof(IPropertyMap));
            MigratedTypes.Add(typeof(IProject));
            MigratedTypes.Add(typeof(IProjectFileLink));
            MigratedTypes.Add(typeof(ICommitFileLink));
            MigratedTypes.Add(typeof(ISourceFile));
            MigratedTypes.Add(typeof(ISourceFileInfo));
            MigratedTypes.Add(typeof(IBoundSourceFile));
            MigratedTypes.Add(typeof(IEncodingDescription));
            MigratedTypes.Add(typeof(IBoundSourceInfo));
            MigratedTypes.Add(typeof(IReferencedProject));
            MigratedTypes.Add(typeof(ISymbolLineSpanList));
            MigratedTypes.Add(typeof(IReferenceSearchModel));
            MigratedTypes.Add(typeof(ITextSourceSearchModel));

            Compilation = CSharpCompilation.Create("TempGeneratorAssembly").AddReferences(PortableExecutableReference.CreateFromFile(assembly.Location,
                documentation: XmlDocumentationProvider.CreateFromFile(Path.ChangeExtension(assembly.Location, ".xml"))));
            var symbol = Compilation.GetTypeByMetadataName(typeof(IBoundSourceFile).FullName);
            var comment = symbol.GetDocumentationCommentXml();

            Types = assembly
                .GetTypes()
                .Where(t => t.IsInterface && !t.IsGenericType && t.GetMethods().Where(m => !m.IsSpecialName).Count() == 0)
                //.Where(t => t.IsAssignableFrom(typeof(ISearchEntity)))
                .Select(ToTypeDefinition)
                .ToList();

            foreach (var typeDefinition in Types)
            {
                typeDefinition.TypeSymbol = Compilation.GetTypeByMetadataName(typeDefinition.Type.FullName);
                DefinitionsByType[typeDefinition.Type] = typeDefinition;
                DefinitionsByTypeMetadataName[typeDefinition.Type.FullName] = typeDefinition;
            }

            foreach (var typeDefinition in Types)
            {
                Dictionary<string, INamedTypeSymbol> interfacesByMetadataName = new Dictionary<string, INamedTypeSymbol>();
                foreach (var baseInterface in typeDefinition.TypeSymbol.AllInterfaces)
                {
                    interfacesByMetadataName[baseInterface.ContainingNamespace + "." + baseInterface.MetadataName] = baseInterface;
                }

                foreach (var baseType in typeDefinition.Type.GetInterfaces())
                {
                    if (baseType.Assembly == assembly && baseType.IsGenericType)
                    {
                        if (!DefinitionsByType.ContainsKey(baseType))
                        {
                            var baseTypeDefinition = ToTypeDefinition(baseType);
                            baseTypeDefinition.TypeSymbol = interfacesByMetadataName[baseType.GetMetadataName()];
                            DefinitionsByType[baseType] = baseTypeDefinition;
                        }
                    }
                }
            }

            foreach (var typeDefinition in DefinitionsByType.Values)
            {
                var interfaces = typeDefinition.Type.GetInterfaces();
                Type baseType = null;
                int baseTypeCount = 0;
                IEnumerable<Type> baseTypeEnumerable = Enumerable.Empty<Type>();
                foreach (var i in interfaces)
                {
                    if (i == typeof(ISearchEntity))
                    {
                        baseType = i;
                        baseTypeCount = 1;
                        break;
                    }

                    if (i.GetInterfaces().Length == interfaces.Length - 1 && DefinitionsByType.ContainsKey(i))
                    {
                        baseType = i;
                        baseTypeCount++;
                    }
                }

                if (baseTypeCount == 1)
                {
                    typeDefinition.BaseType = baseType;
                    typeDefinition.BaseTypeDefinition = DefinitionsByType[baseType];
                    baseTypeEnumerable = new[] { baseType }.Concat(baseType.GetInterfaces());
                }

                typeDefinition.Interfaces.AddRange(interfaces.Except(baseTypeEnumerable).Where(t => DefinitionsByType.ContainsKey(t)).Select(t => DefinitionsByType[t]));
            }

            foreach (var typeDefinition in DefinitionsByType.Values)
            {
                typeDefinition.Migrated = true;// MigratedTypes.Contains(typeDefinition.Type);

                var declarationType = typeDefinition.Type;
                if (declarationType.IsGenericType)
                {
                    declarationType = declarationType.GetGenericTypeDefinition();
                }

                var typeSymbol = typeDefinition.TypeSymbol ?? Compilation.GetTypeByMetadataName(declarationType.FullName);
                typeDefinition.ClassName = typeDefinition.ExplicitClassName ?? typeSymbol.ToDisplayString(TypeNameDisplayFormat).Substring(1);

                foreach (var typeParameter in typeSymbol.TypeParameters)
                {
                    typeDefinition.TypeParameters.Add(new CodeTypeReference(new CodeTypeParameter(typeParameter.Name)));
                }

                typeDefinition.Comments.AddComments(typeSymbol.GetDocumentationCommentXml());

                var members = typeSymbol.GetMembers()
                    .Where(m => m.Kind == SymbolKind.Property)
                    .ToDictionary(m => m.Name);

                foreach (var property in typeDefinition.Properties)
                {
                    DefinitionsByType.TryGetValue(property.PropertyType, out property.PropertyTypeDefinition);
                    property.ImmutablePropertyType = new CodeTypeReference(property.PropertyInfo.PropertyType);
                    property.Comments.AddComments(members[property.Name].GetDocumentationCommentXml());

                    property.MutablePropertyType = property.PropertyTypeDefinition != null ? new CodeTypeReference(property.PropertyTypeDefinition.ClassName) : new CodeTypeReference(property.PropertyType);
                    if (property.IsList)
                    {
                        property.InitPropertyType = new CodeTypeReference(typeof(List<object>)).Apply(ct => ct.TypeArguments[0] = property.MutablePropertyType);
                        property.MutablePropertyType = property.IsReadOnlyList ?
                            new CodeTypeReference(typeof(IReadOnlyList<object>)).Apply(ct => ct.TypeArguments[0] = property.MutablePropertyType) :
                            property.InitPropertyType;
                    }
                }
            }

            ComputeSearchRelevantTypes();
        }

        private void ComputeSearchRelevantTypes()
        {
            var visitedTypes = new HashSet<TypeDefinition>();

            foreach (var type in SearchTypes.RegisteredSearchTypes
                .Select(t => DefinitionsByType[t.Type]))
            {
                isSearchRelevant(type);
            }

            bool isSearchRelevant(TypeDefinition type)
            {
                if (!visitedTypes.Add(type))
                {
                    return type.IsSearchRelevant;
                }

                if (type.BaseTypeDefinition != null)
                {
                    type.IsSearchRelevant |= isSearchRelevant(type.BaseTypeDefinition);
                }

                foreach (var property in type.Properties)
                {
                    if (property.PropertyTypeDefinition != null)
                    {
                        type.IsSearchRelevant |= isSearchRelevant(property.PropertyTypeDefinition);
                    }
                    else if (!property.ExcludeFromIndexing)
                    {
                        type.IsSearchRelevant = true;
                    }
                }

                return type.IsSearchRelevant;
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

            CodeCompileUnit elasticSearchTypesFile = new CodeCompileUnit();
            var elasticSearchStoreGenerator = new StoreGenerator(namespaceName: "Codex.ElasticSearch", storeTypeName: "ElasticSearchStoreBase", genericTypedStoreName: "ElasticSearchEntityStore");
            elasticSearchTypesFile.Namespaces.Add(elasticSearchStoreGenerator.StoreNamespace);

            CodeCompileUnit searchDescriptors = new CodeCompileUnit();
            CodeNamespace modelNamespace = new CodeNamespace("Codex.ObjectModel");
            CodeNamespace typesNamespace = new CodeNamespace("Codex.Framework.Types");
            searchDescriptors.Namespaces.Add(modelNamespace);
            searchDescriptors.Namespaces.Add(typesNamespace);
            typesNamespace.Imports.Add(new CodeNamespaceImport(typeof(Task<>).Namespace));
            modelNamespace.Imports.Add(new CodeNamespaceImport(typeof(Task<>).Namespace));
            modelNamespace.Imports.Add(new CodeNamespaceImport("Codex.Framework.Types"));
            modelNamespace.Imports.Add(new CodeNamespaceImport("Codex"));

            modelNamespace.Types.Add(CodexTypeUtilitiesClass);
            modelNamespace.Types.Add(MappingsClass);
            modelNamespace.Types.Add(ClientInterface);

            modelNamespace.Imports.Add(new CodeNamespaceImport("static Codex.ObjectModel.Mappings"));

            var typeMappingCreatorMethod = new CodeMemberMethod()
            {
                Name = "CreateTypeMapping",
                Attributes = MemberAttributes.Static | MemberAttributes.Private,
                ReturnType = typeof(Dictionary<Type, Type>).AsReference()
            };

            var typeMappingVariableName = "typeMapping";
            typeMappingCreatorMethod.Statements.Add(
                new CodeVariableDeclarationStatement(typeof(Dictionary<Type, Type>),
                typeMappingVariableName,
                new CodeObjectCreateExpression(typeof(Dictionary<Type, Type>))));

            var typeMapping = new CodeMemberField(typeof(IReadOnlyDictionary<Type, Type>), "s_typeMappings")
            {
                InitExpression = new CodeMethodInvokeExpression(null, typeMappingCreatorMethod.Name),
                Attributes = MemberAttributes.Static | MemberAttributes.Private
            };

            CodexTypeUtilitiesClass.Members.Add(typeMapping);
            CodexTypeUtilitiesClass.Members.Add(typeMappingCreatorMethod);

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

            typesNamespace.Types.Add(storeTypeDeclaration);

            foreach (var searchType in SearchTypes.RegisteredSearchTypes)
            {
                var typeDefinition = DefinitionsByType[searchType.Type];
                indexTypeDeclaration.Members.Add(new CodeMemberField(typeDefinition.SearchDescriptorName, typeDefinition.SearchDescriptorName));

                if (visitedSearchTypeDefinitions.Add(typeDefinition))
                {
                    MappingsClass.AddMappingProperty(searchType.Name,
                        typeDefinition.MappingTypeReference.Specialize(searchType.Type.AsReference()));

                    ClientInterface.Members.Add(new CodeMemberProperty()
                    {
                        Name = searchType.Name + "Index",
                        Type = typeof(IIndex<>).MakeGenericTypeReference(searchType.Type.AsReference()),
                        HasGet = true
                    });

                    typeDefinition.SearchType = searchType;
                    CodeTypeDeclaration typeDeclaration = new CodeTypeDeclaration(typeDefinition.SearchDescriptorName);
                    //searchDescriptorsNamespace.Types.Add(typeDeclaration);

                    var constructor = new CodeConstructor();
                    constructor.Parameters.Add(new CodeParameterDeclarationExpression(typeof(Index<bool>), "index"));
                    typeDeclaration.Members.Add(constructor);

                    visitedTypeDefinitions.Clear();
                    usedMemberNames.Clear();

                    PopulateIndexProperties(visitedTypeDefinitions, usedMemberNames, new CodeTypeReference(typeDeclaration.Name), typeDefinition, typeDeclaration);
                }
                else
                {
                    throw new Exception($"Duplicate search types ({searchType.Name}, {typeDefinition.SearchType.Name}) for type: {typeDefinition.Type.Name}");
                }
            }

            foreach (var typeDefinition in this.Types)
            {
                // Exclude interfaces for which aren't data types
                if (typeDefinition.Type.GetMethods().Where(m => !m.IsSpecialName).Count() != 0)
                {
                    continue;
                }

                visitedTypeDefinitions.Clear();
                usedMemberNames.Clear();
                var typeDeclaration = new CodeTypeDeclaration(typeDefinition.ClassName)
                {
                    IsClass = true,
                    IsPartial = true,
                };

                var mappingConstructor = new CodeConstructor()
                {
                    Parameters =
                    {
                        // MappingInfo info
                        new CodeParameterDeclarationExpression(typeof(ObjectModel.MappingInfo), "info")
                    },
                    BaseConstructorArgs =
                    {
                        new CodeArgumentReferenceExpression("info")
                    },
                    Attributes = MemberAttributes.Public
                };

                typeDefinition.MappingTypeDeclaration.Members.Add(mappingConstructor);

                typeDefinition.MappingTypeDeclaration.BaseTypes.Add(typeDefinition.SearchRelevantBase?.MappingTypeReference ?? new CodeTypeReference(typeof(ObjectModel.MappingBase)));

                typeDefinition.MappingTypeDeclaration.BaseTypes.Add(typeof(ObjectModel.IMapping<>).MakeGenericTypeReference(
                    typeDefinition.Type.AsReference()));

                if (typeDefinition.IsSearchRelevant)
                {
                    MappingsClass.Members.Add(typeDefinition.MappingTypeDeclaration);
                }

                typeDeclaration.Comments.AddRange(typeDefinition.Comments.ToArray());

                typeDeclaration.CustomAttributes.Add(
                    new CodeAttributeDeclaration(
                        typeof(SerializationInterfaceAttribute).AsReference(),
                        new CodeAttributeArgument(new CodeTypeOfExpression(typeDefinition.Type))));

                typeMappingCreatorMethod.Statements.Add(new CodeMethodInvokeExpression(
                    new CodeVariableReferenceExpression(typeMappingVariableName),
                    "Add",
                    new CodeTypeOfExpression(typeDefinition.Type),
                    new CodeTypeOfExpression(typeDefinition.ClassName)));

                typeMappingCreatorMethod.Statements.Add(new CodeMethodInvokeExpression(
                    new CodeVariableReferenceExpression(typeMappingVariableName),
                    "Add",
                    new CodeTypeOfExpression(typeDefinition.ClassName),
                    new CodeTypeOfExpression(typeDefinition.Type)));

                if (typeDefinition.Migrated)
                {
                    typesNamespace.Imports.Add(new CodeNamespaceImport($"{typeDefinition.ClassName} = {modelNamespace.Name}.{typeDefinition.ClassName}"));
                }

                if (typeDefinition.SearchType != null)
                {
                    var searchTypeMethod = new CodeMemberMethod()
                    {
                        Name = nameof(ISearchEntityBase.GetSearchType),
                        Attributes = MemberAttributes.Override | MemberAttributes.Public,
                        ReturnType = new CodeTypeReference(typeof(SearchType)),
                    };

                    var searchTypeReferenceExpression = new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(typeof(SearchTypes)), typeDefinition.SearchType.Name);
                    searchTypeMethod.Statements.Add(new CodeMethodReturnStatement(searchTypeReferenceExpression));

                    var routingKeyMethod = new CodeMemberMethod()
                    {
                        Name = nameof(ISearchEntityBase.GetRoutingKey),
                        Attributes = MemberAttributes.Override | MemberAttributes.Public,
                        ReturnType = new CodeTypeReference(typeof(string)),
                    };

                    routingKeyMethod.Statements.Add(
                        new CodeMethodReturnStatement(
                            new CodeMethodInvokeExpression(
                                new CodeThisReferenceExpression(),
                                routingKeyMethod.Name,
                                searchTypeReferenceExpression,
                                new CodeThisReferenceExpression()
                        )));

                    typeDeclaration.Members.Add(routingKeyMethod);
                }

                var nspace = typeDefinition.Migrated ? modelNamespace : typesNamespace;
                nspace.Types.Add(typeDeclaration);

                if (typeDefinition.TypeParameters.Count == 0)
                {
                    typeDeclaration.Members.Add(new CodeConstructor()
                    {
                        Attributes = MemberAttributes.Public
                    }.EnsureInitialize(typeDefinition));
                }


                PopulateProperties(visitedTypeDefinitions, usedMemberNames, typeDefinition, typeDeclaration);

                var visitMethod = new CodeMemberMethod()
                {
                    Name = "Visit",
                    Attributes = MemberAttributes.Public,
                    Parameters =
                    {
                        new CodeParameterDeclarationExpression(typeof(ObjectModel.IVisitor), "visitor"),
                        new CodeParameterDeclarationExpression(typeDefinition.Type, "value")
                    }
                };

                typeDefinition.MappingTypeDeclaration.Members.Add(visitMethod);

                if (typeDefinition.SearchRelevantBase != null)
                {
                    visitMethod.Statements.Add(new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(new CodeBaseReferenceExpression(), visitMethod.Name),
                        new CodeArgumentReferenceExpression("visitor"),
                        new CodeArgumentReferenceExpression("value")
                        ));
                }

                foreach (var property in typeDefinition.Properties)
                {

                    if (property.ExcludeFromIndexing)
                    {
                        continue;
                    }

                    if (property.Name == "StableIds")
                    {

                    }

                    visitMethod.Statements.Add(new CodeMethodInvokeExpression(
                        new CodeMethodReferenceExpression(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), property.Name), visitMethod.Name),
                        new CodeArgumentReferenceExpression("visitor"),
                        new CodeFieldReferenceExpression(new CodeArgumentReferenceExpression("value"), property.Name)));

                    //if (property.PropertyTypeDefinition != null)
                    //{

                    //    //).ApplyIf(property.IsList, m =>
                    //    //{
                    //    //    m.Parameters.Add(new CodeSnippetExpression($"(item, v, m) => item.Visit(v, m)"));
                    //    //    return m;
                    //    //}));
                    //}
                    //else
                    //{
                    //    visitMethod.Statements.Add(new CodeMethodInvokeExpression(
                    //        new CodeMethodReferenceExpression(new CodeArgumentReferenceExpression("visitor"), visitMethod.Name),
                    //        new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), property.Name),
                    //        new CodeFieldReferenceExpression(new CodeArgumentReferenceExpression("value"), property.Name)
                    //    ).ApplyIf(property.IsList, m =>
                    //    {
                    //        m.Parameters.Add(new CodeSnippetExpression($"(item, v, m) => v.Visit(m, item)"));
                    //        return m;
                    //    }));
                    //}
                }

                //if (!typeDefinition.Type.IsGenericType)
                //{
                //    typeDeclaration.BaseTypes.Add(new CodeTypeReference(typeof(IMutable<object, object>))
                //        .Apply(tp => tp.TypeArguments[0] = new CodeTypeReference(typeDeclaration.Name))
                //        .Apply(tp => tp.TypeArguments[1] = new CodeTypeReference(typeDefinition.Type)));
                //}
            }

            typeMappingCreatorMethod.Statements.Add(new CodeMethodReturnStatement(new CodeVariableReferenceExpression(typeMappingVariableName)));

            using (var writer = new StreamWriter(ElasticSearchTypesTarget.Path))
            {
                CodeProvider.GenerateCodeFromCompileUnit(elasticSearchTypesFile, writer, new System.CodeDom.Compiler.CodeGeneratorOptions()
                {
                    BlankLinesBetweenMembers = true,
                    IndentString = "    "
                });
            }

            using (var writer = new StreamWriter(EntityTypesTarget.Path))
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
            TypeDefinition typeDefinition,
            CodeTypeDeclaration typeDeclaration,
            TypeDefinition declarationTypeDefinition = null,
            bool isBaseChain = false)
        {
            declarationTypeDefinition ??= typeDefinition;
            if (!visitedTypeDefinitions.Add(typeDefinition))
            {
                return;
            }

            var applyMethod = new CodeMemberMethod()
            {
                Name = "CopyFrom",
                Attributes = MemberAttributes.Public,
                ReturnType = new CodeTypeReference("TTarget")
            };


            var copyConstructor = new CodeConstructor()
            {
                Attributes = MemberAttributes.Public,
            }.EnsureInitialize(declarationTypeDefinition);

            if (typeDefinition.TypeParameters.Count == 0)
            {
                typeDeclaration.Members.Add(copyConstructor);
            }

            applyMethod.TypeParameters.Add(new CodeTypeParameter("TTarget").Apply(tp => tp.Constraints.Add(typeDeclaration.Name)));
            applyMethod.Parameters.Add(new CodeParameterDeclarationExpression(typeDefinition.Type.AsReference(), "value"));
            copyConstructor.Parameters.AddRange(applyMethod.Parameters);
            if (!isBaseChain)
            {
                copyConstructor.Statements.Add(new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), applyMethod.Name, new CodeVariableReferenceExpression("value"))
                    .Apply(invoke => invoke.Method.TypeArguments.Add(new CodeTypeReference(typeDeclaration.Name))));
            }
            else
            {
                copyConstructor.BaseConstructorArgs.Add(new CodeVariableReferenceExpression("value"));
            }

            foreach (var property in typeDefinition.Properties)
            {
                AddPropertyApplyStatement(applyMethod, property);

                if (isBaseChain)
                {
                    usedMemberNames.Add(property.Name);
                    continue;
                }

                if (property.PropertyTypeDefinition != null || property.IsList)
                {
                    // Need to add explicit interface implementation for
                    // complex property types as the interface specifies the
                    // interface but the property specifies the class

                    typeDeclaration.Members.Add(new CodeMemberProperty()
                    {
                        Type = property.ImmutablePropertyType,
                        Name = property.Name,
                        HasGet = true,
                        PrivateImplementationType = property.PropertyInfo.DeclaringType.AsReference()
                    }
                    .AddComments(property.Comments)
                    .Apply(p => p.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), property.Name)))));
                }

                if (!usedMemberNames.Add(property.Name))
                {
                    continue;
                }

                CodeMemberField backingField = new CodeMemberField()
                {
                    Name = property.BackingFieldName,
                    Attributes = MemberAttributes.Private,
                    Type = property.CoercedSourceType?.AsReference() ?? property.MutablePropertyType,
                };

                if (!isBaseChain && !property.ExcludeFromIndexing)
                {
                    declarationTypeDefinition.MappingTypeDeclaration.AddMappingProperty(
                        property.Name,
                        property.PropertyTypeDefinition?.MappingTypeReference ?? property.PropertyType.AsReference().AsMappingType(),
                        property.SearchBehavior);
                }

                if (property.InitPropertyType != null)
                {
                    if (property.IsReadOnlyList)
                    {
                        backingField.InitExpression = new CodePropertyReferenceExpression(
                            new CodeTypeReferenceExpression(typeof(CollectionUtilities.Empty<>).MakeGenericTypeReference(property.MutablePropertyType.TypeArguments[0])),
                            nameof(CollectionUtilities.Empty<int>.Array));
                    }
                    else
                    {
                        backingField.InitExpression = new CodeObjectCreateExpression(property.InitPropertyType);
                    }
                }

                typeDeclaration.Members.Add(backingField);

                string coercePropertyMethodName = $"Coerce{property.Name}";

                typeDeclaration.Members.Add(new CodeMemberProperty()
                {
                    Type = property.MutablePropertyType,
                    Name = property.Name,
                    Attributes = MemberAttributes.Public,
                    HasGet = true,
                }
                .AddComments(property.Comments)
                .Apply(p => p.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), backingField.Name)
                    .ApplyIf<CodeExpression>(property.CoerceGet, exp => new CodeMethodInvokeExpression(new CodeThisReferenceExpression(), coercePropertyMethodName, exp)))))
                .Apply(p =>
                {
                    p.HasSet = true;
                    p.SetStatements.Add(new CodeAssignStatement(left: new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), backingField.Name), right: new CodePropertySetValueReferenceExpression()));
                }));
            }

            if (visitedTypeDefinitions.Count == 1)
            {
                if (typeDefinition.BaseTypeDefinition != null)
                {
                    typeDeclaration.BaseTypes.Add(typeDefinition.BaseTypeDefinition.ClassName);
                    applyMethod.Statements.Add(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeBaseReferenceExpression(), applyMethod.Name)
                        .Apply(mr => mr.TypeArguments.Add(typeDefinition.BaseTypeDefinition.ClassName)),
                        new CodeCastExpression(new CodeTypeReference(typeDefinition.BaseType), new CodeVariableReferenceExpression("value"))));
                }
                else if (!typeDefinition.IsAdapter)
                {
                    typeDeclaration.BaseTypes.Add(typeof(EntityBase));
                }

                typeDeclaration.BaseTypes.Add(typeDefinition.Type.AsReference());
            }

            if (typeDefinition.BaseTypeDefinition != null)
            {
                PopulateProperties(visitedTypeDefinitions, usedMemberNames, typeDefinition.BaseTypeDefinition, typeDeclaration,
                    declarationTypeDefinition: declarationTypeDefinition,
                    isBaseChain: declarationTypeDefinition == typeDefinition || isBaseChain);
            }

            if (!isBaseChain)
            {
                foreach (var baseDefinition in typeDefinition.Interfaces)
                {
                    foreach (var property in baseDefinition.Properties)
                    {
                        AddPropertyApplyStatement(applyMethod, property);
                    }

                    //applyMethod.Statements.Add(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), applyMethod.Name),
                    //    new CodeCastExpression(baseDefinition.Type, new CodeVariableReferenceExpression("value"))));
                    PopulateProperties(visitedTypeDefinitions, usedMemberNames, baseDefinition, typeDeclaration,
                        declarationTypeDefinition: declarationTypeDefinition,
                        isBaseChain: false);
                }

                applyMethod.Statements.Add(new CodeMethodReturnStatement(new CodeCastExpression("TTarget", new CodeThisReferenceExpression())));
                typeDeclaration.Members.Add(applyMethod);
            }
        }

        private void AddPropertyApplyStatement(CodeMemberMethod applyMethod, PropertyDefinition property)
        {
            applyMethod.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), property.BackingFieldName),
                GetPropertyApplyExpression(property)));
        }

        private CodeExpression GetPropertyApplyExpression(PropertyDefinition property)
        {
            var valuePropertyReferenceExpression = new CodeFieldReferenceExpression(new CodeCastExpression(property.PropertyInfo.DeclaringType.AsReference(), new CodeVariableReferenceExpression("value")), property.Name);
            if (!property.IsList)
            {
                if (property.PropertyTypeDefinition == null)
                {
                    // value.Property;
                    return valuePropertyReferenceExpression;
                }
                else
                {
                    // new PropertyType().CopyFrom(value.Property);
                    return new CodeSnippetExpression($"EntityUtilities.NullOrCopy(value.{valuePropertyReferenceExpression.FieldName}, v => new {property.PropertyTypeDefinition.ClassName}().CopyFrom<{property.PropertyTypeDefinition.ClassName}>(v));");
                }
            }
            else
            {

                if (property.PropertyTypeDefinition == null)
                {
                    // new List<PropertyType>(value.Property);
                    return new CodeObjectCreateExpression(property.InitPropertyType,
                        valuePropertyReferenceExpression);
                }
                else
                {
                    // new List<PropertyType>(value.Property.Select(v => new PropertyType().CopyFrom(v)));
                    return new CodeObjectCreateExpression(property.InitPropertyType,
                        new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(new CodeTypeReferenceExpression(typeof(Enumerable)), nameof(Enumerable.Select)),
                        valuePropertyReferenceExpression,
                            new CodeSnippetExpression($"v => EntityUtilities.NullOrCopy(v, _v => new {property.PropertyTypeDefinition.ClassName}().CopyFrom<{property.PropertyTypeDefinition.ClassName}>(_v))")));
                }

            }
        }

        private void PopulateIndexProperties(
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
                        PopulateIndexProperties(visitedTypeDefinitions, usedMemberNames, searchType, baseDefinition, typeDeclaration);
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

                        PopulateIndexProperties(visitedTypeDefinitions, usedMemberNames, searchType, property.PropertyTypeDefinition, typeDeclaration);
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
        public static CodeThisReferenceExpression This = new CodeThisReferenceExpression();

        public static CodeTypeReference AsMappingType(this CodeTypeReference type)
        {
            return new CodeTypeReference("Mapping", new CodeTypeReference("TRoot"), type);
        }

        public static void AddMappingProperty(this CodeTypeDeclaration declaringType, string name, CodeTypeReference propertyType, SearchBehavior? searchBehavior = null)
        {
            AddLazyProperty(declaringType, name, propertyType, new CodeObjectCreateExpression(propertyType,
                new CodeObjectCreateExpression(
                    typeof(ObjectModel.MappingInfo),
                    new CodePrimitiveExpression(name),
                    new CodeFieldReferenceExpression(null, nameof(ObjectModel.MappingBase.MappingInfo)),
                    searchBehavior == null ? (CodeExpression)new CodePrimitiveExpression(null) : new CodeFieldReferenceExpression(new CodeTypeReferenceExpression(typeof(SearchBehavior)), searchBehavior.ToString())
                    )));
        }

        public static void AddLazyProperty(this CodeTypeDeclaration declaringType, string name, CodeTypeReference propertyType, CodeExpression initializer)
        {
            var field = new CodeMemberField(propertyType, $"_lazy{name}");
            declaringType.Members.Add(field);

            var property = new CodeMemberProperty()
            {
                Name = name,
                Type = propertyType,
                Attributes = MemberAttributes.Public | MemberAttributes.Final,
                GetStatements =
                {
                    // if (_lazyProperty == null)
                    // {
                    //      _lazyProperty = {initializer}
                    // }
                    new CodeConditionStatement(
                        new CodeBinaryOperatorExpression(
                            new CodeFieldReferenceExpression(This, field.Name),
                            CodeBinaryOperatorType.IdentityEquality,
                            new CodeSnippetExpression("null")
                        ),
                        new CodeAssignStatement(
                            new CodeFieldReferenceExpression(This, field.Name),
                            initializer
                        )
                    ),
                    new CodeMethodReturnStatement(new CodeFieldReferenceExpression(This, field.Name))
                }
            };

            declaringType.Members.Add(property);
        }

        public static CodeTypeReference AsReference(this Type type)
        {
            var result = new CodeTypeReference(type);

            if (type.IsGenericTypeDefinition)
            {
                foreach (var typeParameter in type.GetGenericArguments())
                {
                    result.TypeArguments.Add(new CodeTypeReference(new CodeTypeParameter(typeParameter.Name)));
                }
            }

            return result;
        }

        public static CodeTypeReference Specialize(this CodeTypeReference genericType, params CodeTypeReference[] typeArguments)
        {
            Debug.Assert(genericType.TypeArguments.Count == typeArguments.Length);
            var result = new CodeTypeReference(genericType.BaseType);
            result.TypeArguments.Clear();
            result.TypeArguments.AddRange(typeArguments);
            return result;
        }

        public static CodeTypeReference MakeGenericTypeReference(this Type genericType, params CodeTypeReference[] typeArguments)
        {
            var result = new CodeTypeReference(genericType);
            result.TypeArguments.Clear();
            result.TypeArguments.AddRange(typeArguments);
            return result;
        }

        internal static CodeConstructor EnsureInitialize(this CodeConstructor constructor, TypeDefinition typeDefinition)
        {
            if (typeDefinition.BaseTypeDefinition == null)
            {
                constructor.Statements.Add(new CodeMethodInvokeExpression(null, "Initialize"));
            }

            return constructor;
        }

        public static T ApplyIf<T>(this T codeObject, bool condition, Func<T, T> action)
            where T : CodeObject
        {
            if (condition)
            {
                return action(codeObject);
            }

            return codeObject;
        }

        public static T Apply<T>(this T codeObject, Action<T> action)
            where T : CodeObject
        {
            action(codeObject);
            return codeObject;
        }

        public static T AddComments<T>(this T codeObject, IEnumerable<CodeCommentStatement> comments)
            where T : CodeTypeMember
        {
            codeObject.Comments.AddRange(comments.ToArray());
            return codeObject;
        }

        public static CodeTypeParameter WithClassConstraint(this CodeTypeParameter ct)
        {
            ct.Constraints.Add(" class");
            return ct;
        }

        public static void Add(this CodeTypeParameterCollection collection, CodeTypeParameterCollection source)
        {
            collection.AddRange(source);
        }

        public static CodeMemberMethod PartialMethod(this CodeMemberMethod method)
        {
            method.ReturnType = new CodeTypeReference("partial void");
            return method;
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
