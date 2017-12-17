using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public static partial class AllocationRules {
        // Used by CallSiteImplicitAllocationAnalyzer
        public static readonly AllocationRuleDescription ParamsParameterRule =
            new AllocationRuleDescription("HAA0101", "Array allocation for params parameter", "This call site is calling into a function with a 'params' parameter. This results in an array allocation even if no parameter is passed in for the params parameter", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription ValueTypeNonOverridenCallRule =
            new AllocationRuleDescription("HAA0102", "Non-overridden virtual method call on value type", "Non-overridden virtual method call on a value type adds a boxing or constrained instruction", DiagnosticSeverity.Warning);

        // Used by ConcatenationAllocationAnalyzer
        public static readonly AllocationRuleDescription StringConcatenationAllocationRule =
            new AllocationRuleDescription("HAA0201", "Implicit string concatenation allocation", "Consider using StringBuilder", DiagnosticSeverity.Warning, "http://msdn.microsoft.com/en-us/library/2839d5h5(v=vs.110).aspx");

        public static readonly AllocationRuleDescription ValueTypeToReferenceTypeInAStringConcatenationRule =
            new AllocationRuleDescription("HAA0202", "Value type to reference type conversion allocation for string concatenation", "Value type ({0}) is being boxed to a reference type for a string concatenation.", DiagnosticSeverity.Warning, "http://msdn.microsoft.com/en-us/library/yz2be5wk.aspx");

        public static IEnumerable<AllocationRuleDescription> DefaultValues() {
            yield return ParamsParameterRule;
            yield return ValueTypeNonOverridenCallRule;
            yield return StringConcatenationAllocationRule;
            yield return ValueTypeToReferenceTypeInAStringConcatenationRule;
        }
    }
}
