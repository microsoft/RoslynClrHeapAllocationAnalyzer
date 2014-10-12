namespace ClrHeapAllocationAnalyzer
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using System.Collections.Generic;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class DisplayClassAllocationAnalyzer : ISyntaxNodeAnalyzer<SyntaxKind>
    {
        internal static DiagnosticDescriptor ClosureDriverRule = new DiagnosticDescriptor("HeapAnalyzerClosureSourceRule", "Closure Allocation Source", "Heap allocation of closure Captures: {0}", "Performance", DiagnosticSeverity.Warning, true);

        internal static DiagnosticDescriptor ClosureCaptureRule = new DiagnosticDescriptor("HeapAnalyzerClosureCaptureRule", "Display class allocation to capture closure", "The compiler will emit a class that will hold this as a field to allow capturing of this closure", "Performance", DiagnosticSeverity.Warning, true);

        internal static DiagnosticDescriptor LambaOrAnonymousMethodInGenericMethodRule = new DiagnosticDescriptor("HeapAnalyzerLambdaInGenericMethodRule", "Lambda or anonymous method in a generic method allocates a delegate instance", "Considering moving this out of the generic method", "Performance", DiagnosticSeverity.Warning, true);

        internal static object[] EmptyMessageArgs = { };

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(ClosureCaptureRule, ClosureDriverRule, LambaOrAnonymousMethodInGenericMethodRule);
            }
        }

        public ImmutableArray<SyntaxKind> SyntaxKindsOfInterest
        {
            get
            {
                return ImmutableArray.Create(SyntaxKind.ParenthesizedLambdaExpression, SyntaxKind.SimpleLambdaExpression, SyntaxKind.AnonymousMethodExpression);
            }
        }

        public void AnalyzeNode(SyntaxNode node, SemanticModel semanticModel, Action<Diagnostic> addDiagnostic, AnalyzerOptions options, CancellationToken cancellationToken)
        {
            var anonExpr = node as AnonymousMethodExpressionSyntax;
            if (anonExpr != null && anonExpr.Block != null && anonExpr.Block.ChildNodes() != null && anonExpr.Block.ChildNodes().Any())
            {
                GenericMethodCheck(semanticModel, node, anonExpr.DelegateKeyword.GetLocation(), addDiagnostic);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(anonExpr.Block.ChildNodes().First(), anonExpr.Block.ChildNodes().Last()), addDiagnostic, anonExpr.DelegateKeyword.GetLocation());
                return;
            }

            var lambdaExpr = node as SimpleLambdaExpressionSyntax;
            if (lambdaExpr != null)
            {
                GenericMethodCheck(semanticModel, node, lambdaExpr.ArrowToken.GetLocation(), addDiagnostic);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(lambdaExpr), addDiagnostic, lambdaExpr.ArrowToken.GetLocation());
                return;
            }

            var parenLambdaExpr = node as ParenthesizedLambdaExpressionSyntax;
            if (parenLambdaExpr != null)
            {
                GenericMethodCheck(semanticModel, node, parenLambdaExpr.ArrowToken.GetLocation(), addDiagnostic);
                ClosureCaptureDataFlowAnalysis(semanticModel.AnalyzeDataFlow(parenLambdaExpr), addDiagnostic, parenLambdaExpr.ArrowToken.GetLocation());
                return;
            }
        }

        private static void ClosureCaptureDataFlowAnalysis(DataFlowAnalysis flow, Action<Diagnostic> addDiagnostic, Location location)
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
                            addDiagnostic(Diagnostic.Create(ClosureCaptureRule, l, EmptyMessageArgs));
                        }
                    }
                }

                if (captures.Count > 0)
                {
                    addDiagnostic(Diagnostic.Create(ClosureDriverRule, location, new object[] { string.Join(",", captures) }));
                }
            }
        }

        private static void GenericMethodCheck(SemanticModel semanticModel, SyntaxNode node, Location location, Action<Diagnostic> addDiagnostic)
        {
            if (semanticModel.GetSymbolInfo(node).Symbol != null)
            {
                var containingSymbol = semanticModel.GetSymbolInfo(node).Symbol.ContainingSymbol as IMethodSymbol;
                if (containingSymbol != null && containingSymbol.Arity > 0)
                {
                    addDiagnostic(Diagnostic.Create(LambaOrAnonymousMethodInGenericMethodRule, location, EmptyMessageArgs));
                }
            }
        }
    }
}