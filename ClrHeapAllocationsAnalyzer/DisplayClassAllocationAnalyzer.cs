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
        protected override string[] Rules => new[] {AllocationRules.ClosureCaptureRule.Id, AllocationRules.ClosureDriverRule.Id, AllocationRules.LambaOrAnonymousMethodInGenericMethodRule.Id };
        //public static DiagnosticDescriptor ClosureDriverRule = new DiagnosticDescriptor("HAA0301", "Closure Allocation Source", "Heap allocation of closure Captures: {0}", "Performance", DiagnosticSeverity.Warning, true);

        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression };
        //public static DiagnosticDescriptor ClosureCaptureRule = new DiagnosticDescriptor("HAA0302", "Display class allocation to capture closure", "The compiler will emit a class that will hold this as a field to allow capturing of this closure", "Performance", DiagnosticSeverity.Warning, true);

        private static readonly object[] EmptyMessageArgs = { };
        //public static DiagnosticDescriptor LambaOrAnonymousMethodInGenericMethodRule = new DiagnosticDescriptor("HAA0303", "Lambda or anonymous method in a generic method allocates a delegate instance", "Considering moving this out of the generic method", "Performance", DiagnosticSeverity.Warning, true);

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
            if (flow?.Captured.Length <= 0)
            {
                return;
            }

            foreach (var capture in flow.Captured)
            {
                if (capture.Name != null && capture.Locations != null)
                {
                    foreach (var l in capture.Locations) { 
                        if (captureRule.IsEnabledByDefault)
                        {
                            reportDiagnostic(Diagnostic.Create(captureRule, l, EmptyMessageArgs));
                        }
                    }
                }
            }

            if (driverRule.IsEnabledByDefault && flow.Captured.Length > 0)
            {
                reportDiagnostic(Diagnostic.Create(driverRule, location, new[] {string.Join(",", flow.Captured.Select(x => x.Name))}));
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