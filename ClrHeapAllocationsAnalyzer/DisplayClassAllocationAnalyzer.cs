using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DisplayClassAllocationAnalyzer : AllocationAnalyzer
    {
        protected override string[] Rules => new[] {AllocationRules.ClosureCaptureRule.Id, AllocationRules.ClosureDriverRule.Id, AllocationRules.LambaOrAnonymousMethodInGenericMethodRule.Id };

        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression };

        private static readonly object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                AllocationRules.GetDescriptor(AllocationRules.ClosureCaptureRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.ClosureDriverRule.Id),
                AllocationRules.GetDescriptor(AllocationRules.LambaOrAnonymousMethodInGenericMethodRule.Id)
            );

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context, EnabledRules rules)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;

            bool genericRuleEnabled = rules.TryGet(AllocationRules.LambaOrAnonymousMethodInGenericMethodRule.Id, out DiagnosticDescriptor genericRule);
            bool driverRuleEnabled = rules.TryGet(AllocationRules.ClosureDriverRule.Id, out DiagnosticDescriptor driverRule);
            bool captureRuleEnabled = rules.TryGet(AllocationRules.ClosureCaptureRule.Id, out DiagnosticDescriptor captureRule);

            var anonExpr = node as AnonymousMethodExpressionSyntax;
            if (anonExpr?.Block?.ChildNodes() != null && anonExpr.Block.ChildNodes().Any())
            {
                if (genericRuleEnabled)
                {
                    GenericMethodCheck(genericRule, semanticModel, node, anonExpr.DelegateKeyword.GetLocation(), reportDiagnostic, cancellationToken);
                }

                if (captureRuleEnabled || driverRuleEnabled)
                {
                    ClosureCaptureDataFlowAnalysis(captureRule, driverRule, semanticModel.AnalyzeDataFlow(anonExpr.Block.ChildNodes().First(), anonExpr.Block.ChildNodes().Last()), reportDiagnostic, anonExpr.DelegateKeyword.GetLocation());
                }
                return;
            }

            if (node is SimpleLambdaExpressionSyntax lambdaExpr)
            {
                if (genericRuleEnabled)
                {
                    GenericMethodCheck(genericRule, semanticModel, node,
                        lambdaExpr.ArrowToken.GetLocation(), reportDiagnostic, cancellationToken);
                }

                if (captureRuleEnabled || driverRuleEnabled)
                {
                    ClosureCaptureDataFlowAnalysis(captureRule, driverRule, semanticModel.AnalyzeDataFlow(lambdaExpr), reportDiagnostic, lambdaExpr.ArrowToken.GetLocation());
                }
                return;
            }

            if (node is ParenthesizedLambdaExpressionSyntax parenLambdaExpr)
            {
                if (genericRuleEnabled)
                {
                    GenericMethodCheck(genericRule, semanticModel, node,
                        parenLambdaExpr.ArrowToken.GetLocation(), reportDiagnostic, cancellationToken);
                }

                if (captureRuleEnabled || driverRuleEnabled)
                {
                    ClosureCaptureDataFlowAnalysis(captureRule, driverRule, semanticModel.AnalyzeDataFlow(parenLambdaExpr), reportDiagnostic, parenLambdaExpr.ArrowToken.GetLocation());
                }

                return;
            }
        }

        private static void ClosureCaptureDataFlowAnalysis(DiagnosticDescriptor captureRule, DiagnosticDescriptor driverRule, DataFlowAnalysis flow, Action<Diagnostic> reportDiagnostic, Location location)
        {
            if (flow != null && flow.DataFlowsIn != null)
            {
                var captures = new List<string>();
                foreach (var dfaIn in flow.DataFlowsIn)
                {
                    if (dfaIn.Name != null && dfaIn.Locations != null)
                    {
                        captures.Add(dfaIn.Name);
                        if (captureRule.IsEnabledByDefault)
                        {
                            foreach (var l in dfaIn.Locations)
                            {
                                reportDiagnostic(Diagnostic.Create(captureRule, l, EmptyMessageArgs));
                            }
                        }
                    }
                }

                if (driverRule.IsEnabledByDefault && captures.Count > 0)
                {
                    reportDiagnostic(Diagnostic.Create(driverRule, location, new object[] {string.Join(",", captures)}));
                }
            }
        }

        private static void GenericMethodCheck(DiagnosticDescriptor rule, SemanticModel semanticModel, SyntaxNode node, Location location, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {
            if (semanticModel.GetSymbolInfo(node, cancellationToken).Symbol != null)
            {
                var containingSymbol = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol.ContainingSymbol as IMethodSymbol;
                if (containingSymbol != null && containingSymbol.Arity > 0)
                {
                    reportDiagnostic(Diagnostic.Create(rule, location, EmptyMessageArgs));
                }
            }
        }
    }
}