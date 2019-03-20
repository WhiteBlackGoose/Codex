using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codex.Analysis.Managed
{
    class DocumentDiagnosticAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CB0001";
        public const string Title = "Avoid using implict types with var";
        public const string Message = "var usage has non-obvious type.  Use an explicit type.";
        private const string Category = "Naming";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, Message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            //context.RegisterOperationBlockAction(ProcessOperationBlock);

            //context.RegisterOperationAction(ProcessOperation);


            context.RegisterSemanticModelAction(semanticModelAnalysisContext =>
            {
                var filePath = semanticModelAnalysisContext.SemanticModel.SyntaxTree.FilePath;
            });
        }

        private void ProcessOperation(OperationAnalysisContext context)
        {
            if (context.Operation is IInvocationOperation invocation)
            {
            }
        }

        private void ProcessOperationBlock(OperationBlockAnalysisContext obj)
        {
        }
    }
}
