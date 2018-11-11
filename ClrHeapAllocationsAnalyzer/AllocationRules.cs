using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ClrHeapAllocationAnalyzer
{
    public class AllocationRules
    {
        private static readonly HashSet<ValueTuple<string, string>> IgnoredAttributes = new HashSet<(string, string)>
        {
            ("System.Runtime.CompilerServices", "CompilerGeneratedAttribute"),
            ("System.CodeDom.Compiler", "GeneratedCodeAttribute")
        };

        public static bool IsIgnoredFile(string filePath)
        {
            return filePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsIgnoredAttribute(AttributeData attribute)
        {
            return IgnoredAttributes.Contains((attribute.AttributeClass.ContainingNamespace.ToString(), attribute.AttributeClass.Name));
        }

        public static EnabledRules GetEnabledRules(ImmutableArray<DiagnosticDescriptor> supportedDiagnostics,
            SyntaxNodeAnalysisContext context)
        {
            return HotPathAnalysis.GetEnabledRules(supportedDiagnostics, context);
        }
    }
}
