namespace ClrHeapAllocationAnalyzer
{
    using System;
    using System.Collections.Immutable;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Operations;

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CallSiteImplicitAllocationAnalyzer : AllocationAnalyzer
    {
        public static DiagnosticDescriptor ParamsParameterRule = new DiagnosticDescriptor("HAA0101", "Array allocation for params parameter", "This call site is calling into a function with a 'params' parameter. This results in an array allocation", "Performance", DiagnosticSeverity.Warning, true);

        public static DiagnosticDescriptor ValueTypeNonOverridenCallRule = new DiagnosticDescriptor("HAA0102", "Non-overridden virtual method call on value type", "Non-overridden virtual method call on a value type adds a boxing or constrained instruction", "Performance", DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(ParamsParameterRule, ValueTypeNonOverridenCallRule);

        protected override SyntaxKind[] Expressions => new[] { SyntaxKind.InvocationExpression };

        private static readonly object[] EmptyMessageArgs = { };

        protected override void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node;
            var semanticModel = context.SemanticModel;
            Action<Diagnostic> reportDiagnostic = context.ReportDiagnostic;
            var cancellationToken = context.CancellationToken;
            string filePath = node.SyntaxTree.FilePath;

            var invocationOperation = semanticModel.GetOperation(node, cancellationToken) as IInvocationOperation;
            if(invocationOperation is null)
            {
                return;
            }

            var targetMethod = invocationOperation.TargetMethod;

            if (targetMethod.IsOverride)
            {
                CheckNonOverridenMethodOnStruct(targetMethod, reportDiagnostic, node, filePath);
            }

            bool compilationHasSystemArrayEmpty = !semanticModel.Compilation.GetSpecialType(SpecialType.System_Array).GetMembers("Empty").IsEmpty;

            // Loop on every argument because params argument may not be the last one.
            //     static void Fun1() => Fun2(args: "", i: 5);
            //     static void Fun2(int i = 0, params object[] args) {}
            foreach (var argument in invocationOperation.Arguments)
            {
                if (argument.ArgumentKind != ArgumentKind.ParamArray)
                {
                    continue;
                }

                bool isEmpty = (argument.Value as IArrayCreationOperation)?.Initializer.ElementValues.IsEmpty == true;

                // Up to net45 the System.Array.Empty<T> singleton didn't existed so an empty params array was still causing some memory allocation.
                if (argument.IsImplicit && (!isEmpty || !compilationHasSystemArrayEmpty))
                {
                    reportDiagnostic(Diagnostic.Create(ParamsParameterRule, node.GetLocation(), EmptyMessageArgs));
                }

                break;
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
