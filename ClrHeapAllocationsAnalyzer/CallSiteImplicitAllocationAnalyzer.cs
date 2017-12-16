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
        private static readonly string ParamsParameterRuleId = "HAA0101";
        private static readonly string ValueTypeNonOverridenCallRuleId = "HAA0102";
        private static readonly string[] IDs = { ParamsParameterRuleId, ValueTypeNonOverridenCallRuleId };

        public static DiagnosticDescriptor ParamsParameterRule = new DiagnosticDescriptor(ParamsParameterRuleId, "Array allocation for params parameter", "This call site is calling into a function with a 'params' parameter. This results in an array allocation even if no parameter is passed in for the params parameter", "Performance", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor ValueTypeNonOverridenCallRule = new DiagnosticDescriptor(ValueTypeNonOverridenCallRuleId, "Non-overridden virtual method call on value type", "Non-overridden virtual method call on a value type adds a boxing or constrained instruction", "Performance", DiagnosticSeverity.Warning, true);

        internal static object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ParamsParameterRule, ValueTypeNonOverridenCallRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var enabledRules = Settings.GetEnabled(IDs);
            if (enabledRules.Any())
            {
                AnalyzeNode(context, enabledRules);
            }
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context, IReadOnlyCollection<string> enabledRules)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            string filePath = node.SyntaxTree.FilePath;

            var invocationExpression = node as InvocationExpressionSyntax;

            if (semanticModel.GetSymbolInfo(invocationExpression, cancellationToken).Symbol is IMethodSymbol methodInfo)
            {
                if (enabledRules.Contains(ValueTypeNonOverridenCallRuleId))
                {
                    CheckNonOverridenMethodOnStruct(methodInfo, reportDiagnostic, invocationExpression, filePath);
                }

                if (enabledRules.Contains(ParamsParameterRuleId))
                {
                    if (methodInfo.Parameters.Length > 0 && invocationExpression.ArgumentList != null)
                    {
                        var lastParam = methodInfo.Parameters[methodInfo.Parameters.Length - 1];
                        if (lastParam.IsParams) {
                            CheckParam(invocationExpression, methodInfo, semanticModel, reportDiagnostic, filePath, cancellationToken);
                        }
                    }
                }  
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void CheckParam(InvocationExpressionSyntax invocationExpression, IMethodSymbol methodInfo, SemanticModel semanticModel, Action<Diagnostic> reportDiagnostic, string filePath, CancellationToken cancellationToken)
        {
            var arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments.Count != methodInfo.Parameters.Length)
            {
                reportDiagnostic(Diagnostic.Create(ParamsParameterRule, invocationExpression.GetLocation(), EmptyMessageArgs));
                HeapAllocationAnalyzerEventSource.Logger.ParamsAllocation(filePath);
            }
            else
            {
                var lastIndex = arguments.Count - 1;
                var lastArgumentTypeInfo = semanticModel.GetTypeInfo(arguments[lastIndex].Expression, cancellationToken);
                if (lastArgumentTypeInfo.Type != null && !lastArgumentTypeInfo.Type.Equals(methodInfo.Parameters[lastIndex].Type))
                {
                    reportDiagnostic(Diagnostic.Create(ParamsParameterRule, invocationExpression.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.ParamsAllocation(filePath);
                }
            }
        }

        private static void CheckNonOverridenMethodOnStruct(IMethodSymbol methodInfo, Action<Diagnostic> reportDiagnostic, SyntaxNode node, string filePath)
        {
            if (methodInfo.ContainingType != null)
            {
                // hack? Hmmm.
                var containingType = methodInfo.ContainingType.ToString();
                if (string.Equals(containingType, "System.ValueType", StringComparison.OrdinalIgnoreCase) || string.Equals(containingType, "System.Enum", StringComparison.OrdinalIgnoreCase))
                {
                    reportDiagnostic(Diagnostic.Create(ValueTypeNonOverridenCallRule, node.GetLocation(), EmptyMessageArgs));
                    HeapAllocationAnalyzerEventSource.Logger.NonOverridenVirtualMethodCallOnValueType(filePath);
                }
            }
        }
    }
}