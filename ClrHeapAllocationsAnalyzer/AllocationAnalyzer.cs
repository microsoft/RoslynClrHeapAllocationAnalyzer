using System.Linq;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer {
    public abstract class AllocationAnalyzer : DiagnosticAnalyzer
    {
        private string[] supportedRules;

        protected abstract SyntaxKind[] Expressions { get; }

        protected abstract void AnalyzeNode(SyntaxNodeAnalysisContext context, EnabledRules rules);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, Expressions);
            supportedRules = SupportedDiagnostics.Select(x => x.Id).ToArray();
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            EnabledRules rules = AllocationRules.GetEnabled(supportedRules);
            if (rules.AnyEnabled)
            {
                AnalyzeNode(context, rules);
            }
        }
    }
}
