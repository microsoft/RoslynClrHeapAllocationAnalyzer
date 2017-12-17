using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer {
    public abstract class AllocationAnalyzer : DiagnosticAnalyzer
    {
        protected abstract SyntaxKind[] Expressions { get; }

        protected abstract string[] Rules { get; }

        protected abstract void AnalyzeNode(SyntaxNodeAnalysisContext context, EnabledRules rules);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, Expressions);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            EnabledRules rules = AllocationRules.GetEnabled(Rules);
            if (rules.AnyEnabled)
            {
                AnalyzeNode(context, rules);
            }
        }
    }
}
