namespace ClrHeapAllocationAnalyzer
{
    using System;
    using System.Collections.Immutable;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CallSiteImplicitAllocationAnalyzer : ISyntaxNodeAnalyzer<SyntaxKind>
    {
        internal static DiagnosticDescriptor ParamsParameterRule = new DiagnosticDescriptor("Array allocation for params parameter", string.Empty, "This call site is calling into a function with a 'params' parameter. This results in an array allocation even if no parameter is passed in for the params parameter", "Performance", DiagnosticSeverity.Warning, true);

        internal static DiagnosticDescriptor ValueTypeNonOverridenCallRule = new DiagnosticDescriptor("Non-overriden virtual method call on value type", string.Empty, "Non-overriden virtual method call on a value type adds a boxing or constrained instruction", "Performance", DiagnosticSeverity.Warning, true);

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(ValueTypeNonOverridenCallRule, ParamsParameterRule);
            }
        }

        public ImmutableArray<SyntaxKind> SyntaxKindsOfInterest
        {
            get
            {
                return ImmutableArray.Create(SyntaxKind.InvocationExpression);
            }
        }

        public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            string filePath = node.SyntaxTree.FilePath;

            var invocationExpression = node as InvocationExpressionSyntax;
            var methodInfo = semanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
            if (methodInfo != null)
            {
                var parameters = methodInfo.Parameters;
                if (parameters != null)
                {
                    foreach (var parameter in parameters)
                    {
                        if (parameter.IsParams)
                        {
                            addDiagnostic(Diagnostic.Create(ParamsParameterRule, node.GetLocation()));
                            HeapAllocationAnalyzerEventSource.Logger.ParamsAllocation(filePath);
                        }
                    }
                }

                if (methodInfo.ContainingType != null)
                {
                    // hack? Hmmm.
                    var containingType = methodInfo.ContainingType.ToString();
                    if (string.Equals(containingType, "System.ValueType", StringComparison.OrdinalIgnoreCase) || string.Equals(containingType, "System.Enum", StringComparison.OrdinalIgnoreCase))
                    {
                        addDiagnostic(Diagnostic.Create(ValueTypeNonOverridenCallRule, node.GetLocation()));
                        HeapAllocationAnalyzerEventSource.Logger.NonOverridenVirtualMethodCallOnValueType(filePath);
                    }
                }
            }
        }
    }
}