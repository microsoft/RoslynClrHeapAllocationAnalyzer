﻿using System.Collections.Generic;
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

        // Used by DisplayClassAllocationAnalyzer
        public static readonly AllocationRuleDescription ClosureDriverRule =
            new AllocationRuleDescription("HAA0301", "Closure Allocation Source", "Heap allocation of closure Captures: {0}", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription ClosureCaptureRule =
            new AllocationRuleDescription("HAA0302", "Display class allocation to capture closure", "The compiler will emit a class that will hold this as a field to allow capturing of this closure", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription LambaOrAnonymousMethodInGenericMethodRule =
            new AllocationRuleDescription("HAA0303", "Lambda or anonymous method in a generic method allocates a delegate instance", "Considering moving this out of the generic method", DiagnosticSeverity.Warning);


        public static IEnumerable<AllocationRuleDescription> DefaultValues() {
            yield return ParamsParameterRule;
            yield return ValueTypeNonOverridenCallRule;
            yield return StringConcatenationAllocationRule;
            yield return ValueTypeToReferenceTypeInAStringConcatenationRule;
            yield return ClosureDriverRule;
            yield return ClosureCaptureRule;
            yield return LambaOrAnonymousMethodInGenericMethodRule;
        }
    }
}
