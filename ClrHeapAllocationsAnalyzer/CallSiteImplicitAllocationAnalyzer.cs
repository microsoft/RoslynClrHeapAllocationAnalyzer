using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    public sealed class CallSiteImplicitAllocationAnalyzer : DiagnosticAnalyzer {
        public static readonly AllocationRuleDescription ParamsParameterRule =
            new AllocationRuleDescription("HAA0101", "Array allocation for params parameter", "This call site is calling into a function with a 'params' parameter. This results in an array allocation even if no parameter is passed in for the params parameter", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription ValueTypeNonOverridenCallRule =
            new AllocationRuleDescription("HAA0102", "Non-overridden virtual method call on value type", "Non-overridden virtual method call on a value type adds a boxing or constrained instruction", DiagnosticSeverity.Warning);

        private static readonly string[] AllRules = { ParamsParameterRule.Id, ValueTypeNonOverridenCallRule.Id };

        private static readonly object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(AllocationRules.GetDescriptor(ParamsParameterRule.Id), AllocationRules.GetDescriptor(ValueTypeNonOverridenCallRule.Id));

        public override void Initialize(AnalysisContext context) {
            AllocationRules.RegisterAnalyzerRule(ParamsParameterRule);
            AllocationRules.RegisterAnalyzerRule(ValueTypeNonOverridenCallRule);

            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var enabledRules = AllocationRules.GetEnabled(AllRules);
            if (enabledRules.Any())
            {
                AnalyzeNode(context, enabledRules);
            }
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context, IReadOnlyDictionary<string, DiagnosticDescriptor> enabledRules)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            string filePath = node.SyntaxTree.FilePath;

            var invocationExpression = node as InvocationExpressionSyntax;

            if (semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is IMethodSymbol methodInfo)
            {
                if (enabledRules.ContainsKey(ValueTypeNonOverridenCallRule.Id))
                {
                    CheckNonOverridenMethodOnStruct(enabledRules[ValueTypeNonOverridenCallRule.Id], methodInfo, reportDiagnostic, invocationExpression, filePath);
                }

                if (enabledRules.ContainsKey(ParamsParameterRule.Id))
                {
                    if (methodInfo.Parameters.Length > 0 && invocationExpression.ArgumentList != null)
                    {
                        var lastParam = methodInfo.Parameters[methodInfo.Parameters.Length - 1];
                        if (lastParam.IsParams) {
                            CheckParam(enabledRules[ParamsParameterRule.Id], invocationExpression, methodInfo, semanticModel, reportDiagnostic, filePath, cancellationToken);
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