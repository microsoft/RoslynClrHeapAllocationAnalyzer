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

        // Used by DisplayClassAllocationAnalyzer
        public static readonly AllocationRuleDescription ClosureDriverRule =
            new AllocationRuleDescription("HAA0301", "Closure Allocation Source", "Heap allocation of closure Captures: {0}", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription ClosureCaptureRule =
            new AllocationRuleDescription("HAA0302", "Display class allocation to capture closure", "The compiler will emit a class that will hold this as a field to allow capturing of this closure", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription LambaOrAnonymousMethodInGenericMethodRule =
            new AllocationRuleDescription("HAA0303", "Lambda or anonymous method in a generic method allocates a delegate instance", "Considering moving this out of the generic method", DiagnosticSeverity.Warning);

        // Used by EnumeratorAllocationAnalyzer
        public static readonly AllocationRuleDescription ReferenceTypeEnumeratorRule =
            new AllocationRuleDescription("HAA0401", "Possible allocation of reference type enumerator", "Non-ValueType enumerator may result in an heap allocation", DiagnosticSeverity.Warning);

        // Used by ExplicitAllocationAnalyzer
        public static readonly AllocationRuleDescription NewArrayRule =
            new AllocationRuleDescription("HAA0501", "Explicit new array type allocation", "Explicit new array type allocation", DiagnosticSeverity.Info);

        public static readonly AllocationRuleDescription NewObjectRule =
            new AllocationRuleDescription("HAA0502", "Explicit new reference type allocation", "Explicit new reference type allocation", DiagnosticSeverity.Info);

        public static readonly AllocationRuleDescription AnonymousNewObjectRule =
            new AllocationRuleDescription("HAA0503", "Explicit new anonymous object allocation", "Explicit new anonymous object allocation", DiagnosticSeverity.Info, "http://msdn.microsoft.com/en-us/library/bb397696.aspx");

        public static readonly AllocationRuleDescription ImplicitArrayCreationRule =
            new AllocationRuleDescription("HAA0504", "Implicit new array creation allocation", "Implicit new array creation allocation", DiagnosticSeverity.Info);

        public static readonly AllocationRuleDescription InitializerCreationRule =
            new AllocationRuleDescription("HAA0505", "Initializer reference type allocation", "Initializer reference type allocation", DiagnosticSeverity.Info);

        public static readonly AllocationRuleDescription LetCauseRule =
            new AllocationRuleDescription("HAA0506", "Let clause induced allocation", "Let clause induced allocation", DiagnosticSeverity.Info);

        /// Used by TypeConversionAllocationAnalyzer
        public static readonly AllocationRuleDescription ValueTypeToReferenceTypeConversionRule =
            new AllocationRuleDescription("HAA0601", "Value type to reference type conversion causing boxing allocation", "Value type to reference type conversion causes boxing at call site (here), and unboxing at the callee-site. Consider using generics if applicable", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription DelegateOnStructInstanceRule =
            new AllocationRuleDescription("HAA0602", "Delegate on struct instance caused a boxing allocation", "Struct instance method being used for delegate creation, this will result in a boxing instruction", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription MethodGroupAllocationRule =
            new AllocationRuleDescription("HAA0603", "Delegate allocation from a method group", "This will allocate a delegate instance", DiagnosticSeverity.Warning);

        public static readonly AllocationRuleDescription ReadonlyMethodGroupAllocationRule =
            new AllocationRuleDescription("HAA0604", "Delegate allocation from a readonly method group", "This will allocate a delegate instance", DiagnosticSeverity.Info);

        public static IEnumerable<AllocationRuleDescription> DefaultValues() {
            yield return ParamsParameterRule;
            yield return ValueTypeNonOverridenCallRule;
            yield return StringConcatenationAllocationRule;
            yield return ValueTypeToReferenceTypeInAStringConcatenationRule;
            yield return ClosureDriverRule;
            yield return ClosureCaptureRule;
            yield return LambaOrAnonymousMethodInGenericMethodRule;
            yield return ReferenceTypeEnumeratorRule;
            yield return NewArrayRule;
            yield return NewObjectRule;
            yield return AnonymousNewObjectRule;
            yield return ImplicitArrayCreationRule;
            yield return InitializerCreationRule;
            yield return LetCauseRule;
            yield return ValueTypeToReferenceTypeConversionRule;
            yield return DelegateOnStructInstanceRule;
            yield return MethodGroupAllocationRule;
            yield return ReadonlyMethodGroupAllocationRule;
        }
    }
}
