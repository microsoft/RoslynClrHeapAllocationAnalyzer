namespace ClrHeapAllocationAnalyzer
{
    using System;
    using System.Collections.Immutable;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using System.Runtime.CompilerServices;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CallSiteImplicitAllocationAnalyzer : ISyntaxNodeAnalyzer<SyntaxKind>
    {
        internal static DiagnosticDescriptor ParamsParameterRule = new DiagnosticDescriptor("HeapAnalyzerImplicitParamsRule", "Array allocation for params parameter", "This call site is calling into a function with a 'params' parameter. This results in an array allocation even if no parameter is passed in for the params parameter", "Performance", DiagnosticSeverity.Warning, true);

        internal static DiagnosticDescriptor ValueTypeNonOverridenCallRule = new DiagnosticDescriptor("HeapAnalyzerValueTypeNonOverridenCallRule", "Non-overriden virtual method call on value type", "Non-overriden virtual method call on a value type adds a boxing or constrained instruction", "Performance", DiagnosticSeverity.Warning, true);

        internal static object[] EmptyMessageArgs = { };

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
                CheckNonOverridenMethodOnStruct(methodInfo, addDiagnostic, invocationExpression, filePath);
                if (methodInfo.Parameters.Length > 0 && invocationExpression.ArgumentList != null)
                {
                    var lastParam = methodInfo.Parameters[methodInfo.Parameters.Length - 1];
                    if (lastParam.IsParams)
                    {
                        CheckParam(invocationExpression, methodInfo, semanticModel, addDiagnostic, filePath);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckParam(InvocationExpressionSyntax invocationExpression, IMethodSymbol methodInfo, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, string filePath)
        {
            var arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments.Count != methodInfo.Parameters.Length)
            {
                addDiagnostic(Diagnostic.Create(ParamsParameterRule, invocationExpression.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.ParamsAllocation(filePath);
            }
            else
            {
                var lastIndex = arguments.Count - 1;
                var lastArgumentTypeInfo = semanticModel.GetTypeInfo(arguments[lastIndex].Expression);
                if (lastArgumentTypeInfo.Type != null && !lastArgumentTypeInfo.Type.Equals(methodInfo.Parameters[lastIndex].Type))
                {
                    addDiagnostic(Diagnostic.Create(ParamsParameterRule, invocationExpression.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.ParamsAllocation(filePath);
                }
            }
        }

        private static void CheckNonOverridenMethodOnStruct(IMethodSymbol methodInfo, Action<Diagnostic> addDiagnostic, SyntaxNode node, string filePath)
        {
            if (methodInfo.ContainingType != null)
            {
                // hack? Hmmm.
                var containingType = methodInfo.ContainingType.ToString();
                if (string.Equals(containingType, "System.ValueType", StringComparison.OrdinalIgnoreCase) || string.Equals(containingType, "System.Enum", StringComparison.OrdinalIgnoreCase))
                {
                    addDiagnostic(Diagnostic.Create(ValueTypeNonOverridenCallRule, node.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.NonOverridenVirtualMethodCallOnValueType(filePath);
                }
            }
        }
    }
}