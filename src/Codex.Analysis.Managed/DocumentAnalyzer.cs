using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codex.Analysis.Managed;
using Codex.Analysis.Managed.Symbols;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using CS = Microsoft.CodeAnalysis.CSharp;

namespace Codex.Analysis
{
    class DocumentAnalyzer
    {
        private Document _document;
        private Compilation _compilation;
        private CompilationServices CompilationServices;

        private ConcurrentDictionary<INamedTypeSymbol, (ILookup<ISymbol, ISymbol> memberByImplementedLookup, IDictionary<ISymbol, ISymbol> interfaceMemberToImplementationMap)> interfaceMemberImplementationMap 
            = new ConcurrentDictionary<INamedTypeSymbol, (ILookup<ISymbol, ISymbol> memberByImplementedLookup, IDictionary<ISymbol, ISymbol> interfaceMemberToImplementationMap)>();
        private Dictionary<ISymbol, int> localSymbolIdMap = new Dictionary<ISymbol, int>();

        public SemanticModel SemanticModel;
        private AnalyzedProject _analyzedProject;
        private BoundSourceFileBuilder boundSourceFile;
        private List<ReferenceSpan> references;
        private AnalyzedProjectContext context;
        private SourceText DocumentText;
        private readonly SemanticServices semanticServices;

        public DocumentAnalyzer(
            SemanticServices semanticServices,
            Document document,
            CompilationServices compilationServices,
            string logicalPath,
            AnalyzedProjectContext context,
            BoundSourceFileBuilder boundSourceFile)
        {
            _document = document;
            CompilationServices = compilationServices;
            _compilation = compilationServices.Compilation;
            _analyzedProject = context.Project;
            this.semanticServices = semanticServices;
            this.context = context;

            references = new List<ReferenceSpan>();

            this.boundSourceFile = boundSourceFile;
        }

        private string Language => _document.Project.Language;

        public string GetText(ClassifiedSpan span)
        {
            return DocumentText.ToString(span.TextSpan);
        }

        public async Task<BoundSourceFile> CreateBoundSourceFile()
        {
            // When re-enabling custom analyzer, be sure that no other analyzers are getting run to collect diagnostics below
            //var compilationWithAnalyzer = _compilation.WithAnalyzers(new DiagnosticAnalyzer[] { new DocumentDiagnosticAnalyzer() }.ToImmutableArray());
            //var diagnostics = await compilationWithAnalyzer.GetAnalyzerDiagnosticsAsync();
            var syntaxRoot = await _document.GetSyntaxRootAsync();
            var syntaxTree = syntaxRoot.SyntaxTree;
            SemanticModel = _compilation.GetSemanticModel(syntaxTree);
            DocumentText = await _document.GetTextAsync();

            boundSourceFile.SourceFile.Info.Lines = DocumentText.Lines.Count;
            boundSourceFile.SourceFile.Info.Size = DocumentText.Length;

            var classificationSpans = (IReadOnlyList<ClassifiedSpan>)await Classifier.GetClassifiedSpansAsync(_document, syntaxRoot.FullSpan);
            var text = await _document.GetTextAsync();

            var originalClassificationSpans = classificationSpans;
            classificationSpans = MergeSpans(classificationSpans).ToList();
            var fileClassificationSpans = new List<ClassificationSpan>();

            foreach (var span in classificationSpans)
            {
                if (SkipSpan(span))
                {
                    continue;
                }

                ClassificationSpan classificationSpan = new ClassificationSpan();
                fileClassificationSpans.Add(classificationSpan);

                classificationSpan.Start = span.TextSpan.Start;
                classificationSpan.Length = span.TextSpan.Length;
                classificationSpan.Classification = span.ClassificationType;

                if (!IsSemanticSpan(span))
                {
                    continue;
                }

                var token = syntaxRoot.FindToken(span.TextSpan.Start, findInsideTrivia: true);

                if (!semanticServices.IsPossibleSemanticToken(token))
                {
                    continue;
                }

                ISymbol symbol = null;
                ISymbol declaredSymbol = null;
                SyntaxNode bindableParentNode = null;
                bool isThis = false;

                if (span.ClassificationType != ClassificationTypeNames.Keyword)
                {
                    declaredSymbol = SemanticModel.GetDeclaredSymbol(token.Parent);
                }

                var usingExpression = semanticServices.TryGetUsingExpressionFromToken(token);
                if (usingExpression != null)
                {
                    var disposeSymbol = CompilationServices.IDisposable_Dispose.Value;
                    if (disposeSymbol != null)
                    {
                        var typeInfo = SemanticModel.GetTypeInfo(usingExpression);
                        var disposeImplSymbol = typeInfo.Type?.FindImplementationForInterfaceMember(disposeSymbol);
                        if (disposeImplSymbol != null)
                        {
                            SymbolSpan usingSymbolSpan = CreateSymbolSpan(syntaxTree, text, span, classificationSpan);
                            references.Add(usingSymbolSpan.CreateReference(GetReferenceSymbol(disposeImplSymbol, ReferenceKind.UsingDispose)));
                        }
                    }
                }

                if (semanticServices.IsOverrideKeyword(token))
                {
                    var bindableNode = token.Parent;
                    bindableNode = semanticServices.GetEventField(bindableNode);

                    var parentSymbol = SemanticModel.GetDeclaredSymbol(bindableNode);

                    SymbolSpan parentSymbolSpan = CreateSymbolSpan(syntaxTree, text, span, classificationSpan);

                    // Don't allow this to show up in search. It's only added for go to definition navigation
                    // on override keyword.
                    AddReferencesToOverriddenMembers(parentSymbolSpan, parentSymbol, excludeFromSearch: true);
                }

                if (symbol == null)
                {
                    symbol = declaredSymbol;

                    if (declaredSymbol == null)
                    {
                        bindableParentNode = GetBindableParent(token);
                        if (bindableParentNode != null)
                        {
                            symbol = GetSymbol(bindableParentNode, out isThis);
                        }
                    }
                }

                if (symbol == null || symbol.ContainingAssembly == null)
                {
                    continue;
                }

                if (symbol.Kind == SymbolKind.Local ||
                    symbol.Kind == SymbolKind.Parameter ||
                    symbol.Kind == SymbolKind.TypeParameter ||
                    symbol.Kind == SymbolKind.RangeVariable)
                {
                    //  Just generate group ids rather than full ref/def
                    var localSymbolId = localSymbolIdMap.GetOrAdd(symbol, localSymbolIdMap.Count + 1);
                    classificationSpan.LocalGroupId = localSymbolId;
                    continue;
                }

                if ((symbol.Kind == SymbolKind.Event ||
                     symbol.Kind == SymbolKind.Field ||
                     symbol.Kind == SymbolKind.Method ||
                     symbol.Kind == SymbolKind.NamedType ||
                     symbol.Kind == SymbolKind.Property) &&
                     symbol.Locations.Length >= 1)
                {
                    var documentationId = GetDocumentationCommentId(symbol);

                    if (string.IsNullOrEmpty(documentationId))
                    {
                        continue;
                    }

                    SymbolSpan symbolSpan = CreateSymbolSpan(syntaxTree, text, span, classificationSpan);

                    if (declaredSymbol != null)
                    {
                        // This is a definition
                        var definitionSymbol = GetDefinitionSymbol(symbol, documentationId);
                        var definitionSpan = symbolSpan.CreateDefinition(definitionSymbol);
                        boundSourceFile.AddDefinition(definitionSpan);

                        // A reference symbol for the definition is added so the definition is found in find all references
                        var definitionReferenceSymbol = GetReferenceSymbol(symbol, referenceKind: ReferenceKind.Definition);
                        references.Add(symbolSpan.CreateReference(definitionReferenceSymbol));

                        ProcessDefinitionAndAddAdditionalReferenceSymbols(symbol, symbolSpan, definitionSymbol, token);
                    }
                    else
                    {
                        // This is a reference
                        var referenceSpan = GetReferenceSpan(symbolSpan, symbol, documentationId, token);

                        // This parameter should not show up in find all references search
                        // but should navigate to type for go to definition
                        referenceSpan.Reference.ExcludeFromSearch = isThis;// token.IsKind(SyntaxKind.ThisKeyword) || token.IsKind(SyntaxKind.BaseKeyword);
                        references.Add(referenceSpan);

                        AddAdditionalReferenceSymbols(symbol, referenceSpan, token);
                    }
                }
            }

            boundSourceFile.AddClassifications(fileClassificationSpans);
            boundSourceFile.AddReferences(references);
            var result = boundSourceFile.Build();
            ManagedAnalysisHost.Instance.OnDocumentFinished(result);
            return result;
        }

        private static SymbolSpan CreateSymbolSpan(SyntaxTree syntaxTree, SourceText text, ClassifiedSpan span, ClassificationSpan classificationSpan)
        {
            var lineSpan = syntaxTree.GetLineSpan(span.TextSpan);
            var symbolSpan = new SymbolSpan()
            {
                Start = classificationSpan.Start,
                Length = classificationSpan.Length,
            };

            return symbolSpan;
        }

        private void AddAdditionalReferenceSymbols(ISymbol symbol, ReferenceSpan symbolSpan, SyntaxToken token)
        {
            //var bindableParent = GetBindableParent(token);

            if (symbol.Kind == SymbolKind.Method)
            {
                // Case: Constructor
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    if (methodSymbol.MethodKind == MethodKind.Constructor)
                    {
                        // Add special reference kind for instantiation
                        references.Add(symbolSpan.CreateReference(GetReferenceSymbol(methodSymbol.ContainingType, ReferenceKind.Instantiation)));
                    }
                }

            }
            else if (symbol.Kind == SymbolKind.Property)
            {

            }
            else if (symbol.Kind == SymbolKind.Field)
            {

            }
            else if (symbol.Kind == SymbolKind.NamedType)
            {
                // Case: Derived class

                // Case: Partial class

                if (symbolSpan.Reference.ReferenceKind == nameof(ReferenceKind.InterfaceImplementation))
                {
                    // TODO: Add references to all member implementations that are not defined on this type specifically
                    
                }
            }
        }

        private void ProcessDefinitionAndAddAdditionalReferenceSymbols(ISymbol symbol, SymbolSpan symbolSpan, DefinitionSymbol definition, SyntaxToken token)
        {
            // Handle potentially virtual or interface member implementations
            if (symbol.Kind == SymbolKind.Method || symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Event)
            {
                AddReferencesToOverriddenMembers(symbolSpan, symbol, relatedDefinition: definition.Id);

                AddReferencesToImplementedMembers(symbolSpan, symbol, definition.Id);
            }

            if (symbol.Kind == SymbolKind.Method)
            {
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    // Case: Constructor and Static Constructor
                    if (methodSymbol.MethodKind == MethodKind.Constructor || methodSymbol.MethodKind == MethodKind.StaticConstructor)
                    {
                        // Exclude constructors from default search
                        // Add a constructor reference with the containing type
                        definition.ExcludeFromDefaultSearch = true;
                        references.Add(symbolSpan.CreateReference(GetReferenceSymbol(methodSymbol.ContainingType, ReferenceKind.Constructor)));
                    }
                }
            }
            else if (symbol.Kind == SymbolKind.Field)
            {
                // Handle enum fields
                if (symbol is IFieldSymbol fieldSymbol 
                    && fieldSymbol.HasConstantValue 
                    && symbol.ContainingType.TypeKind == TypeKind.Enum)
                {
                    definition.Keywords.Add(fieldSymbol.ConstantValue.ToString());
                }
            }
        }

        private static string GetSymbolKind(ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.Method)
            {
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    // Case: Constructor and Static Constructor
                    if (methodSymbol.MethodKind == MethodKind.Constructor || methodSymbol.MethodKind == MethodKind.StaticConstructor)
                    {
                        // Exclude constructors from default search
                        // Add a constructor reference with the containing type
                        return nameof(MethodKind.Constructor);
                    }
                    else if (methodSymbol.MethodKind == MethodKind.UserDefinedOperator)
                    {
                        return nameof(SymbolKinds.Operator);
                    }
                }

            }
            else if (symbol.Kind == SymbolKind.NamedType)
            {
                INamedTypeSymbol typeSymbol = symbol as INamedTypeSymbol;
                if (typeSymbol != null)
                {
                    switch (typeSymbol.TypeKind)
                    {
                        case TypeKind.Class:
                        case TypeKind.Delegate:
                        case TypeKind.Enum:
                        case TypeKind.Interface:
                        case TypeKind.Struct:
                            return typeSymbol.TypeKind.GetString();
                        default:
                            break;
                    }
                }
            }

            return symbol.Kind.GetString();
        }

        private void AddReferencesToImplementedMembers(
            SymbolSpan symbolSpan,
            ISymbol declaredSymbol,
            SymbolId relatedDefinition = default(SymbolId))
        {
            var declaringType = declaredSymbol.ContainingType;
            ILookup<ISymbol, ISymbol> implementationLookup = GetImplementedMemberLookup(declaringType).memberByImplementedLookup;

            foreach (var implementedMember in implementationLookup[declaredSymbol])
            {
                references.Add(symbolSpan.CreateReference(GetReferenceSymbol(implementedMember, ReferenceKind.InterfaceMemberImplementation), relatedDefinition));
            }
        }

        private (ILookup<ISymbol, ISymbol> memberByImplementedLookup, IDictionary<ISymbol, ISymbol> interfaceMemberToImplementationMap) GetImplementedMemberLookup(INamedTypeSymbol declaringType)
        {
            return interfaceMemberImplementationMap.GetOrAdd(declaringType, type =>
            {
                (ILookup<ISymbol, ISymbol> memberByImplementedLookup, IDictionary<ISymbol, ISymbol> interfaceMemberToImplementationMap) result = default;
                result.interfaceMemberToImplementationMap = type.AllInterfaces
                    .SelectMany(implementedInterface =>
                        implementedInterface.GetMembers()
                            .Select(member => (implementation: type.FindImplementationForInterfaceMember(member), implemented: member))
                            .Where(kvp => kvp.implementation != null))
                    .ToDictionarySafe(kvp => kvp.implemented, kvp => kvp.implementation);

                result.memberByImplementedLookup = result.interfaceMemberToImplementationMap.ToLookup(kvp => kvp.Value, kvp => kvp.Key);

                var directInterfaceImplementations = new HashSet<INamedTypeSymbol>(type.Interfaces);
                foreach (var entry in result.interfaceMemberToImplementationMap)
                {
                    var interfaceMember = entry.Key;
                    var implementation = entry.Value;

                    if (Features.AddDefinitionForInheritedInterfaceImplementations)
                    {
                        if (implementation.ContainingType != type && directInterfaceImplementations.Contains(interfaceMember.ContainingType))
                        {
                            var reparentedSymbol = BaseSymbolWrapper.WrapWithOverrideContainer(implementation, type);

                            // Call to trigger addition of the symbol to the set of symbols referenced by the project
                            // TODO: Specify that the definition should not show up in the referenced definitions
                            GetReferenceSymbol(reparentedSymbol, ReferenceKind.Definition);
                        }
                    }
                }

                return result;
            });
        }

        private KeyValuePair<TKey, TValue> CreateKeyValuePair<TKey, TValue>(TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }

        private void AddReferencesToOverriddenMembers(
            SymbolSpan symbolSpan,
            ISymbol declaredSymbol,
            bool excludeFromSearch = false,
            SymbolId relatedDefinition = default(SymbolId))
        {
            if (!declaredSymbol.IsOverride)
            {
                return;
            }

            var overriddenSymbol = GetOverriddenSymbol(declaredSymbol);
            if (overriddenSymbol != null)
            {
                references.Add(symbolSpan.CreateReference(GetReferenceSymbol(overriddenSymbol, ReferenceKind.Override), relatedDefinition));

                if (excludeFromSearch)
                {
                    references[references.Count - 1].Reference.ExcludeFromSearch = true;
                }
            }

            // TODO: Should we add transitive overrides
        }

        private ISymbol GetOverriddenSymbol(ISymbol declaredSymbol)
        {
            IMethodSymbol method = declaredSymbol as IMethodSymbol;
            if (method != null)
            {
                return method.OverriddenMethod;
            }

            IPropertySymbol property = declaredSymbol as IPropertySymbol;
            if (property != null)
            {
                return property.OverriddenProperty;
            }

            IEventSymbol eventSymbol = declaredSymbol as IEventSymbol;
            if (eventSymbol != null)
            {
                return eventSymbol.OverriddenEvent;
            }

            return null;
        }

        private ISymbol GetExplicitlyImplementedMember(ISymbol symbol)
        {
            IMethodSymbol methodSymbol = symbol as IMethodSymbol;
            if (methodSymbol != null)
            {
                return methodSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            IPropertySymbol propertySymbol = symbol as IPropertySymbol;
            if (propertySymbol != null)
            {
                return propertySymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            IEventSymbol eventSymbol = symbol as IEventSymbol;
            if (eventSymbol != null)
            {
                return eventSymbol.ExplicitInterfaceImplementations.FirstOrDefault();
            }

            return null;
        }

        private static DefinitionSymbol GetDefinitionSymbol(ISymbol symbol, string id = null)
        {
            // Use unspecialized generic
            symbol = symbol.OriginalDefinition;
            id = id ?? GetDocumentationCommentId(symbol);

            ISymbol displaySymbol = symbol;

            bool isMember = symbol.Kind == SymbolKind.Field ||
                symbol.Kind == SymbolKind.Method ||
                symbol.Kind == SymbolKind.Event ||
                symbol.Kind == SymbolKind.Property;

            if (isMember)
            {
                if (symbol.Kind == SymbolKind.Method && symbol is IMethodSymbol methodSymbol)
                {
                    if (methodSymbol.MethodKind == MethodKind.UserDefinedOperator)
                    {
                        displaySymbol = new OperatorMethodSymbolDisplayOverride(methodSymbol);
                    }
                }
            }

            string containerQualifierName = string.Empty;
            if (symbol.ContainingSymbol != null)
            {
                containerQualifierName = symbol.ContainingSymbol.ToDisplayString(DisplayFormats.QualifiedNameDisplayFormat);
            }

            var boundSymbol = new DefinitionSymbol()
            {
                ProjectId = symbol.ContainingAssembly.Name,
                Id = CreateSymbolId(id),
                DisplayName = displaySymbol.GetDisplayString(),
                ShortName = displaySymbol.ToDisplayString(DisplayFormats.ShortNameDisplayFormat),
                ContainerQualifiedName = containerQualifierName,
                Kind = GetSymbolKind(symbol),
                Glyph = symbol.GetGlyph(),
                SymbolDepth = symbol.GetSymbolDepth(),
                Comment = symbol.GetDocumentationCommentXml(),
                DeclarationName = displaySymbol.ToDeclarationName(),
                TypeName = GetTypeName(symbol)
            };

            return boundSymbol;
        }

        /// <summary>
        /// Transforms a documentation id into a symbol id
        /// </summary>
        private static SymbolId CreateSymbolId(string id)
        {
            // First process the id from standard form T:System.String
            // to form System.String:T which ensure most common prefixes
            if (id != null && id.Length > 3 && id[1] == ':')
            {
                id = id.Substring(2) + ":" + id[0];
            }

            return SymbolId.CreateFromId(id);
        }

        private ReferenceSpan GetReferenceSpan(SymbolSpan span, ISymbol symbol, string id, SyntaxToken token)
        {
            (ReferenceKind referenceKind, SymbolId relatedDefinitionId) = DetermineReferenceKind(symbol, token);

            var referenceSymbol = GetReferenceSymbol(symbol, referenceKind, id);

            return span.CreateReference(referenceSymbol, relatedDefinitionId);
        }

        private (ReferenceKind kind, SymbolId relatedDefinitionId) DetermineReferenceKind(ISymbol referencedSymbol, SyntaxToken token)
        {
            (ReferenceKind kind, SymbolId relatedDefinitionId) result = (ReferenceKind.Reference, default);
            // Case: nameof() - Do we really care about distinguishing this case.

            if (referencedSymbol.Kind == SymbolKind.NamedType)
            {
                var node = GetBindableParent(token);
                var typeArgumentList = (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.TypeArgumentListSyntax>();
                if (typeArgumentList != null)
                {
                    return result;
                }

                typeArgumentList = (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.VisualBasic.Syntax.TypeArgumentListSyntax>();
                if (typeArgumentList != null)
                {
                    return result;
                }

                var baseList =
                    (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.CSharp.Syntax.BaseListSyntax>() ??
                    (SyntaxNode)node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.VisualBasic.Syntax.InheritsStatementSyntax>() ??
                    node.FirstAncestorOrSelf<Microsoft.CodeAnalysis.VisualBasic.Syntax.ImplementsStatementSyntax>();
                if (baseList != null)
                {
                    var typeDeclaration = baseList.Parent;
                    if (typeDeclaration != null)
                    {
                        var derivedType = SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
                        if (derivedType != null)
                        {
                            INamedTypeSymbol baseSymbol = referencedSymbol as INamedTypeSymbol;
                            if (baseSymbol != null)
                            {
                                void setRelatedTypeKind(ReferenceKind targetKind)
                                {
                                    result.relatedDefinitionId = CreateSymbolId(GetDocumentationCommentId(derivedType));
                                    result.kind = targetKind;

                                    if (targetKind == ReferenceKind.InterfaceImplementation)
                                    {
                                        AddSyntheticInterfaceMemberImplementations(
                                            interfaceSymbol: baseSymbol,
                                            implementerSymbol: derivedType);
                                    }
                                }

                                if (baseSymbol.TypeKind == TypeKind.Class && baseSymbol.Equals(derivedType.BaseType))
                                {
                                    setRelatedTypeKind(ReferenceKind.DerivedType);
                                }
                                else if (baseSymbol.TypeKind == TypeKind.Interface)
                                {
                                    if (derivedType.Interfaces.Contains(baseSymbol))
                                    {
                                        setRelatedTypeKind(derivedType.TypeKind == TypeKind.Interface 
                                            ? ReferenceKind.InterfaceInheritance
                                            : ReferenceKind.InterfaceImplementation);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (referencedSymbol.Kind == SymbolKind.Field ||
                referencedSymbol.Kind == SymbolKind.Property)
            {
                var node = GetBindableParent(token);
                if (IsWrittenTo(node))
                {
                    result.kind = ReferenceKind.Write;
                }
            }

            return result;
        }

        private void AddSyntheticInterfaceMemberImplementations(INamedTypeSymbol interfaceSymbol, INamedTypeSymbol implementerSymbol)
        {

            GetImplementedMemberLookup(implementerSymbol);
        }

        private ReferenceSymbol GetReferenceSymbol(ISymbol symbol, ReferenceKind referenceKind, string id = null)
        {
            // Use unspecialized generic
            // TODO: Consider adding reference symbol for specialized generic as well so one can find all references
            // to List<string>.Add rather than just List<T>.Add
            symbol = symbol.OriginalDefinition;

            var boundSymbol = new ReferenceSymbol()
            {
                ProjectId = symbol.ContainingAssembly.Name,
                Id = CreateSymbolId(id ?? GetDocumentationCommentId(symbol)),
                Kind = GetSymbolKind(symbol),
                ReferenceKind = referenceKind.GetString(),
                IsImplicitlyDeclared = symbol.IsImplicitlyDeclared
            };

            if (!string.IsNullOrEmpty(symbol.Name))
            {
                DefinitionSymbol definition;
                if (!context.ReferenceDefinitionMap.TryGetValue(boundSymbol.Id.Value, out definition))
                {
                    var createdDefinition = GetDefinitionSymbol(symbol);
                    definition = context.ReferenceDefinitionMap.GetOrAdd(boundSymbol.Id.Value, createdDefinition);
                    if (createdDefinition == definition)
                    {
                        if (symbol.Kind != SymbolKind.Namespace &&
                            symbol.ContainingNamespace != null &&
                            !symbol.ContainingNamespace.IsGlobalNamespace)
                        {
                            var namespaceSymbol = GetReferenceSymbol(symbol.ContainingNamespace, ReferenceKind.Reference);
                            var extData = context.GetReferenceNamespaceData(namespaceSymbol.Id.Value);
                            definition.ExtData = extData;
                        }
                    }
                }

                if (referenceKind != ReferenceKind.Definition)
                {
                    definition.IncrementReferenceCount();
                }
            }

            return boundSymbol;
        }

        private bool IsWrittenTo(SyntaxNode node)
        {
            bool result = semanticServices.IsWrittenTo(SemanticModel, node, CancellationToken.None);
            return result;
        }

        private ISymbol GetSymbol(SyntaxNode node, out bool isThis)
        {
            var symbolInfo = SemanticModel.GetSymbolInfo(node);
            isThis = false;
            ISymbol symbol = symbolInfo.Symbol;
            if (symbol == null)
            {
                return null;
            }

            if (IsThisParameter(symbol))
            {
                isThis = true;
                var typeInfo = SemanticModel.GetTypeInfo(node);
                if (typeInfo.Type != null)
                {
                    return typeInfo.Type;
                }
            }
            else if (IsFunctionValue(symbol))
            {
                var method = symbol.ContainingSymbol as IMethodSymbol;
                if (method != null)
                {
                    if (method.AssociatedSymbol != null)
                    {
                        return method.AssociatedSymbol;
                    }
                    else
                    {
                        return method;
                    }
                }
            }
            else if (symbol.Kind == SymbolKind.Method)
            {
                var method = symbol as IMethodSymbol;
                if (method != null)
                {
                    if (method.ReducedFrom != null)
                    {
                        return method.ReducedFrom;
                    }
                }
            }

            symbol = ResolveAccessorParameter(symbol);

            return symbol;
        }

        private static string GetTypeName(ISymbol symbol)
        {
            if (symbol.Kind == SymbolKind.Method)
            {
                // Case: Constructor
                IMethodSymbol methodSymbol = symbol as IMethodSymbol;
                return methodSymbol.ReturnType?.ToDisplayString(DisplayFormats.TypeNameDisplayFormat);

            }
            else if (symbol.Kind == SymbolKind.Property)
            {
                IPropertySymbol propertySymbol = symbol as IPropertySymbol;
                return propertySymbol.Type?.ToDisplayString(DisplayFormats.TypeNameDisplayFormat);
            }
            else if (symbol.Kind == SymbolKind.Field)
            {
                IFieldSymbol fieldSymbol = symbol as IFieldSymbol;
                return fieldSymbol.Type?.ToDisplayString(DisplayFormats.TypeNameDisplayFormat);
            }

            return null;
        }

        private static string GetDocumentationCommentId(ISymbol symbol)
        {
            string result = null;
            if (!symbol.IsDefinition)
            {
                symbol = symbol.OriginalDefinition;
            }

            result = symbol.GetDocumentationCommentId();
            if (result == null)
            {
                return symbol.ToDisplayString();
            }

            result = result.Replace("#ctor", "ctor");

            return result;
        }

        private ISymbol ResolveAccessorParameter(ISymbol symbol)
        {
            if (symbol == null || !symbol.IsImplicitlyDeclared)
            {
                return symbol;
            }

            var parameterSymbol = symbol as IParameterSymbol;
            if (parameterSymbol == null)
            {
                return symbol;
            }

            var accessorMethod = parameterSymbol.ContainingSymbol as IMethodSymbol;
            if (accessorMethod == null)
            {
                return symbol;
            }

            var property = accessorMethod.AssociatedSymbol as IPropertySymbol;
            if (property == null)
            {
                return symbol;
            }

            int ordinal = parameterSymbol.Ordinal;
            if (property.Parameters.Length <= ordinal)
            {
                return symbol;
            }

            return property.Parameters[ordinal];
        }

        private static bool IsFunctionValue(ISymbol symbol)
        {
            return symbol is ILocalSymbol && ((ILocalSymbol)symbol).IsFunctionValue;
        }

        private static bool IsThisParameter(ISymbol symbol)
        {
            return symbol != null && symbol.Kind == SymbolKind.Parameter && ((IParameterSymbol)symbol).IsThis;
        }

        private IEnumerable<ClassifiedSpan> MergeSpans(IEnumerable<ClassifiedSpan> classificationSpans)
        {
            ClassifiedSpan mergedSpan = default(ClassifiedSpan);
            bool skippedNonWhitespace = false;
            foreach (var span in classificationSpans)
            {
                if (!TryMergeSpan(ref mergedSpan, span, ref skippedNonWhitespace))
                {
                    // Reset skippedNonWhitespace value
                    skippedNonWhitespace = false;
                    yield return mergedSpan;
                    mergedSpan = span;
                }
            }

            if (!string.IsNullOrEmpty(mergedSpan.ClassificationType))
            {
                yield return mergedSpan;
            }
        }

        bool TryMergeSpan(ref ClassifiedSpan current, ClassifiedSpan next, ref bool skippedNonWhitespace)
        {
            if (next.ClassificationType == ClassificationTypeNames.WhiteSpace)
            {
                return true;
            }

            if (SkipSpan(next))
            {
                skippedNonWhitespace = true;
                return true;
            }

            try
            {
                if (string.IsNullOrEmpty(current.ClassificationType))
                {
                    current = next;
                    return true;
                }

                if (current.TextSpan.Equals(next.TextSpan) && !IsSemanticSpan(current) && IsSemanticSpan(next))
                {
                    // If there are completely overlapping spans. Take the span which is semantic over the span which is non-semantic.
                    current = next;
                    return true;
                }

                if (current.TextSpan.Contains(next.TextSpan))
                {
                    return true;
                }

                if (!AllowMerge(next))
                {
                    return false;
                }

                var normalizedClassification = NormalizeClassification(current);
                if (normalizedClassification != NormalizeClassification(next))
                {
                    return false;
                }

                if (current.TextSpan.End < next.TextSpan.Start && (skippedNonWhitespace || !IsWhitespace(current.TextSpan.End, next.TextSpan.Start)))
                {
                    return false;
                }

                current = new ClassifiedSpan(normalizedClassification, new TextSpan(current.TextSpan.Start, next.TextSpan.End - current.TextSpan.Start));
                return true;
            }
            finally
            {
                skippedNonWhitespace = false;
            }
        }

        bool IsWhitespace(int start, int endExclusive)
        {
            for (int i = start; i < endExclusive; i++)
            {
                if (!char.IsWhiteSpace(DocumentText[i]))
                {
                    return false;
                }
            }

            return true;
        }

        static bool AllowMerge(ClassifiedSpan span)
        {
            var classificationType = NormalizeClassification(span);

            switch (classificationType)
            {
                case ClassificationTypeNames.Comment:
                case ClassificationTypeNames.StringLiteral:
                case ClassificationTypeNames.XmlDocCommentComment:
                case ClassificationTypeNames.ExcludedCode:
                    return true;
                default:
                    return false;
            }
        }

        static bool SkipSpan(ClassifiedSpan span)
        {
            if (span.ClassificationType?.Contains("regex") == true)
            {
                return true;
            }

            switch (span.ClassificationType)
            {
                case ClassificationTypeNames.WhiteSpace:
                case ClassificationTypeNames.Punctuation:
                case ClassificationTypeNames.StringEscapeCharacter:
                case ClassificationTypeNames.StaticSymbol:
                        return true;
                default:
                    return false;
            }
        }

        static T ExchangeDefault<T>(ref T value)
        {
            var captured = value;
            value = default(T);
            return captured;
        }

        static string NormalizeClassification(ClassifiedSpan span)
        {
            if (span.ClassificationType == null)
            {
                return null;
            }

            switch (span.ClassificationType)
            {
                case ClassificationTypeNames.XmlDocCommentName:
                case ClassificationTypeNames.XmlDocCommentAttributeName:
                case ClassificationTypeNames.XmlDocCommentAttributeQuotes:
                case ClassificationTypeNames.XmlDocCommentCDataSection:
                case ClassificationTypeNames.XmlDocCommentComment:
                case ClassificationTypeNames.XmlDocCommentDelimiter:
                case ClassificationTypeNames.XmlDocCommentText:
                case ClassificationTypeNames.XmlDocCommentProcessingInstruction:
                    return ClassificationTypeNames.XmlDocCommentComment;
                case ClassificationTypeNames.FieldName:
                case ClassificationTypeNames.EnumMemberName:
                case ClassificationTypeNames.ConstantName:
                case ClassificationTypeNames.LocalName:
                case ClassificationTypeNames.ParameterName:
                case ClassificationTypeNames.MethodName:
                case ClassificationTypeNames.ExtensionMethodName:
                case ClassificationTypeNames.PropertyName:
                case ClassificationTypeNames.EventName:
                    return ClassificationTypeNames.Identifier;
                default:
                    return span.ClassificationType;
            }
        }

        static bool IsSemanticSpan(ClassifiedSpan span)
        {
            switch (NormalizeClassification(span))
            {
                case ClassificationTypeNames.Keyword:
                case ClassificationTypeNames.Identifier:
                case ClassificationTypeNames.Operator:
                case ClassificationTypeNames.ClassName:
                case ClassificationTypeNames.InterfaceName:
                case ClassificationTypeNames.StructName:
                case ClassificationTypeNames.EnumName:
                case ClassificationTypeNames.DelegateName:
                case ClassificationTypeNames.TypeParameterName:
                    return true;
                default:
                    return false;
            }
        }

        private SyntaxNode GetBindableParent(SyntaxToken token)
        {
            return semanticServices.GetBindableParent(token);
        }
    }
}
