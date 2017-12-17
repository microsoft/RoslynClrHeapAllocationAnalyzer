using System.Collections.Generic;
using System.Linq;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ClrHeapAllocationAnalyzer {
    public abstract class AllocationAnalyzer : DiagnosticAnalyzer {
        protected abstract SyntaxKind[] Expressions { get; }

        protected abstract string[] Rules { get; }

        protected abstract void AnalyzeNode(SyntaxNodeAnalysisContext context, IReadOnlyDictionary<string, DiagnosticDescriptor> enabledRules);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNode, Expressions);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context) {
            var enabledRules = AllocationRules.GetEnabled(Rules);
            if (enabledRules.Any())
            {
                AnalyzeNode(context, enabledRules);
            }
        }
    }
}
