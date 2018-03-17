using System;
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

            SyntaxNode genericCheckNode = null;
            Location location = null;
            DataFlowAnalysis flowAnalysis = null;

            var anonExpr = node as AnonymousMethodExpressionSyntax;
            if (anonExpr?.Block?.ChildNodes() != null && anonExpr.Block.ChildNodes().Any())
            {
                if (genericRuleEnabled)
                {
                    genericCheckNode = node;
                    location = anonExpr.DelegateKeyword.GetLocation();
                }

                if (captureRuleEnabled || driverRuleEnabled)
                {
                    flowAnalysis = semanticModel.AnalyzeDataFlow(anonExpr.Block.ChildNodes().First(), anonExpr.Block.ChildNodes().Last());
                    location = anonExpr.DelegateKeyword.GetLocation();
                }
            }

            if (node is SimpleLambdaExpressionSyntax lambdaExpr)
            {
                if (genericRuleEnabled)
                {
                    genericCheckNode = node;
                    location = lambdaExpr.ArrowToken.GetLocation();
                }

                if (captureRuleEnabled || driverRuleEnabled)
                {
                    flowAnalysis = semanticModel.AnalyzeDataFlow(lambdaExpr);
                    location = lambdaExpr.ArrowToken.GetLocation();
                }
            }

            if (node is ParenthesizedLambdaExpressionSyntax parenLambdaExpr)
            {
                if (genericRuleEnabled)
                {
                    genericCheckNode = node;
                    location = parenLambdaExpr.ArrowToken.GetLocation();
                }

                if (captureRuleEnabled || driverRuleEnabled)
                {
                    flowAnalysis = semanticModel.AnalyzeDataFlow(parenLambdaExpr);
                    location = parenLambdaExpr.ArrowToken.GetLocation();
                }
            }

            if (genericCheckNode != null)
            {
                GenericMethodCheck(genericRule, semanticModel, genericCheckNode, location, reportDiagnostic, cancellationToken);
            }

            if (captureRuleEnabled)
            {
                ClosureCaptureDataFlowAnalysis(captureRule, flowAnalysis, reportDiagnostic, location);
            }

            if (driverRuleEnabled)
            {
                if (flowAnalysis?.Captured.Length > 0) {
                    reportDiagnostic(Diagnostic.Create(driverRule, location, new[] { string.Join(",", flowAnalysis.Captured.Select(x => x.Name)) }));
                }
            }
        }
        
        private static void ClosureCaptureDataFlowAnalysis(DiagnosticDescriptor captureRule, DataFlowAnalysis flow, Action<Diagnostic> reportDiagnostic, Location location)
        {
            if (flow?.Captured.Length <= 0)
            {
                return;
            }

            foreach (var capture in flow.Captured)
            {
                if (capture.Name != null && capture.Locations != null)
                {
                    foreach (var l in capture.Locations)
                    { 
                        if (captureRule.IsEnabledByDefault)
                        {
                            reportDiagnostic(Diagnostic.Create(captureRule, l, EmptyMessageArgs));
                        }
                    }
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