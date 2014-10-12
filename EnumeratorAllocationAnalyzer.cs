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
    public sealed class EnumeratorAllocationAnalyzer : ISyntaxNodeAnalyzer<SyntaxKind>
    {
        internal static DiagnosticDescriptor ReferenceTypeEnumeratorRule = new DiagnosticDescriptor("Reference type enumerator, possible allocation", string.Empty, "Enumerator is not of a value type, and so it is possible that this results in an enumerator allocation", "Performance", DiagnosticSeverity.Warning, true);

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(ReferenceTypeEnumeratorRule);
            }
        }

        public ImmutableArray<SyntaxKind> SyntaxKindsOfInterest
        {
            get
            {
                return ImmutableArray.Create(SyntaxKind.ForEachStatement, SyntaxKind.InvocationExpression);
            }
        }

        public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            string filePath = node.SyntaxTree.FilePath;

            var foreachExpression = node as ForEachStatementSyntax;
            if (foreachExpression != null)
            {
                var typeInfo = semanticModel.GetTypeInfo(foreachExpression.Expression);
                if (typeInfo.Type != null)
                {
                    var enumerator = typeInfo.Type.GetMembers("GetEnumerator");
                    if (enumerator != null && enumerator.Length > 0)
                    {
                        var methodSymbol = enumerator[0] as IMethodSymbol; // probably should do something better here, hack.
                        if (methodSymbol != null)
                        {
                            if (methodSymbol.ReturnType.IsReferenceType)
                            {
                                addDiagnostic(Diagnostic.Create(ReferenceTypeEnumeratorRule, foreachExpression.InKeyword.GetLocation()));
                                HeapAllocationAnalyzerEventSource.Logger.EnumeratorAllocation(filePath);
                            }
                        }
                    }
                }

                return;
            }

            var invocationExpression = node as InvocationExpressionSyntax;
            if (invocationExpression != null)
            {
                var methodInfo = semanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
                if (methodInfo != null)
                {
                    if (methodInfo.ReturnType != null && methodInfo.ReturnType.IsReferenceType)
                    {
                        if (methodInfo.ReturnType.AllInterfaces != null)
                        {
                            foreach (var @interface in methodInfo.ReturnType.AllInterfaces)
                            {
                                if (@interface.SpecialType == SpecialType.System_Collections_Generic_IEnumerator_T || @interface.SpecialType == SpecialType.System_Collections_IEnumerator)
                                {
                                    addDiagnostic(Diagnostic.Create(ReferenceTypeEnumeratorRule, invocationExpression.GetLocation()));
                                    HeapAllocationAnalyzerEventSource.Logger.EnumeratorAllocation(filePath);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}