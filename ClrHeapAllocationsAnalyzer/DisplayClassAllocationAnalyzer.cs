using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DisplayClassAllocationAnalyzer : DiagnosticAnalyzer
    {
        public static DiagnosticDescriptor ClosureDriverRule = new DiagnosticDescriptor("HAA0301", "Closure Allocation Source", "Heap allocation of closure Captures: {0}", "Performance", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor ClosureCaptureRule = new DiagnosticDescriptor("HAA0302", "Display class allocation to capture closure", "The compiler will emit a class that will hold this as a field to allow capturing of this closure", "Performance", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor LambaOrAnonymousMethodInGenericMethodRule = new DiagnosticDescriptor("HAA0303", "Lambda or anonymous method in a generic method allocates a delegate instance", "Considering moving this out of the generic method", "Performance", DiagnosticSeverity.Warning, true);

        internal static object[] EmptyMessageArgs = { };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ClosureCaptureRule, ClosureDriverRule, LambaOrAnonymousMethodInGenericMethodRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            var cancellationToken = context.CancellationToken;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;

            var anonExpr = node as AnonymousMethodExpressionSyntax;
            if (anonExpr?.Block?.ChildNodes() != null && anonExpr.Block.ChildNodes().Any())
            {
                GenericMethodCheck(semanticModel, node, anonExpr.DelegateKeyword.GetLocation(), reportDiagnostic, cancellationToken);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(anonExpr.Block.ChildNodes().First(), anonExpr.Block.ChildNodes().Last()), reportDiagnostic, anonExpr.DelegateKeyword.GetLocation());
                return;
            }

            var lambdaExpr = node as SimpleLambdaExpressionSyntax;
            if (lambdaExpr != null)
            {
                GenericMethodCheck(semanticModel, node, lambdaExpr.ArrowToken.GetLocation(), reportDiagnostic, cancellationToken);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(lambdaExpr), reportDiagnostic, lambdaExpr.ArrowToken.GetLocation());
                return;
            }

            var parenLambdaExpr = node as ParenthesizedLambdaExpressionSyntax;
            if (parenLambdaExpr != null)
            {
                GenericMethodCheck(semanticModel, node, parenLambdaExpr.ArrowToken.GetLocation(), reportDiagnostic, cancellationToken);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(parenLambdaExpr), reportDiagnostic, parenLambdaExpr.ArrowToken.GetLocation());
                return;
            }
        }

        private static void ClosureCaptureDataFlowAnalysis(DataFlowAnalysis flow, Action<Diagnostic> reportDiagnostic, Location location)
        {
            if (flow != null && flow.DataFlowsIn != null)
            {
                var captures = new List<string>();
                foreach (var dfaIn in flow.DataFlowsIn)
                {
                    if (dfaIn.Name != null && dfaIn.Locations != null)
                    {
                        captures.Add(dfaIn.Name);
                        foreach (var l in dfaIn.Locations)
                        {
                            reportDiagnostic(Diagnostic.Create(ClosureCaptureRule, l, EmptyMessageArgs));
                        }
                    }
                }

                if (captures.Count > 0)
                {
                    reportDiagnostic(Diagnostic.Create(ClosureDriverRule, location, new object[] { string.Join(",", captures) }));
                }
            }
        }

        private static void GenericMethodCheck(SemanticModel semanticModel, SyntaxNode node, Location location, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
        {
            if (semanticModel.GetSymbolInfo(node, cancellationToken).Symbol != null)
            {
                var containingSymbol = semanticModel.GetSymbolInfo(node, cancellationToken).Symbol.ContainingSymbol as IMethodSymbol;
                if (containingSymbol != null && containingSymbol.Arity > 0)
                {
                    reportDiagnostic(Diagnostic.Create(LambaOrAnonymousMethodInGenericMethodRule, location, EmptyMessageArgs));
                }
            }
        }
    }
}