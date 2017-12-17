using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CallSiteImplicitAllocationAnalyzer : AllocationAnalyzer
    {
        protected override string[] Rules => new[] {AllocationRules.ParamsParameterRule.Id, AllocationRules.ValueTypeNonOverridenCallRule.Id};

        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.InvocationExpression };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => 
            ImmutableArray.Create(
                AllocationRules.GetDescriptor(AllocationRules.ParamsParameterRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.ValueTypeNonOverridenCallRule.Id)
            );

        private static readonly object[] EmptyMessageArgs = { };

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context, EnabledRules rules)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            string filePath = node.SyntaxTree.FilePath;

            var invocationExpression = node as InvocationExpressionSyntax;

            if (semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is IMethodSymbol methodInfo)
            {
                if (rules.IsEnabled(AllocationRules.ValueTypeNonOverridenCallRule.Id))
                {
                    CheckNonOverridenMethodOnStruct(rules.Get(AllocationRules.ValueTypeNonOverridenCallRule.Id), methodInfo, reportDiagnostic, invocationExpression, filePath);
                }

                if (rules.IsEnabled(AllocationRules.ParamsParameterRule.Id))
                {
                    if (methodInfo.Parameters.Length > 0 && invocationExpression.ArgumentList != null)
                    {
                        var lastParam = methodInfo.Parameters[methodInfo.Parameters.Length - 1];
                        if (lastParam.IsParams) {
                            CheckParam(rules.Get(AllocationRules.ParamsParameterRule.Id), invocationExpression, methodInfo, semanticModel, reportDiagnostic, filePath, cancellationToken);
                        }
                    }
                }  
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckParam(DiagnosticDescriptor rule, InvocationExpressionSyntax invocationExpression, IMethodSymbol methodInfo, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments.Count != methodInfo.Parameters.Length)
            {
                reportDiagnostic(Diagnostic.Create(rule, invocationExpression.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.ParamsAllocation(filePath);
            }
            else
            {
                var lastIndex = arguments.Count - 1;
                var lastArgumentTypeInfo = semanticModel.GetTypeInfo(arguments[lastIndex].Expression, cancellationToken);
                if (lastArgumentTypeInfo.Type != null && !lastArgumentTypeInfo.Type.Equals(methodInfo.Parameters[lastIndex].Type))
                {
                    reportDiagnostic(Diagnostic.Create(rule, invocationExpression.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.ParamsAllocation(filePath);
                }
            }
        }

        private static void CheckNonOverridenMethodOnStruct(DiagnosticDescriptor rule, IMethodSymbol methodInfo, Action<Diagnostic> reportDiagnostic, SyntaxNode node, string filePath)
        {
            if (methodInfo.ContainingType != null)
            {
                // hack? Hmmm.
                var containingType = methodInfo.ContainingType.ToString();
                if (string.Equals(containingType, "System.ValueType", StringComparison.OrdinalIgnoreCase) || string.Equals(containingType, "System.Enum", StringComparison.OrdinalIgnoreCase))
                {
                    reportDiagnostic(Diagnostic.Create(rule, node.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.NonOverridenVirtualMethodCallOnValueType(filePath);
                }
            }
        }
    }
}