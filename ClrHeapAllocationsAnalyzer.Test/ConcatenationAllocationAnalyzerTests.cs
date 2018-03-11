using ClrHeapAllocationAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;

namespace ClrHeapAllocationsAnalyzer.Test
{
    [TestClass]
    public class ConcatenationAllocationAnalyzerTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void ConcatenationAllocation_Basic()
        {
            var sampleProgram =
@"using System;

var withBoxing = 5.ToString() + ':' + 8.ToString(); // Boxing on ':' 
var withoutBoxing = 5.ToString() + "":"" + 8.ToString();
";

            var analyser = new ConcatenationAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));

            Assert.AreEqual(1, info.Allocations.Count(d => d.Id == "HeapAnalyzerBoxingRule"));
            Assert.AreEqual(4, info.Allocations.Count(d => d.Id == "HeapAnalyzerStringConcatRule"));

            //### CODE ### 5.ToString() + ':' + 8.ToString()
            //*** Diagnostic: (9,45): warning HeapAnalyzerBoxingRule: Value type (char) is being boxed to a reference type for a string concatenation.
            AssertEx.ContainsDiagnostic(info.Allocations, id: ConcatenationAllocationAnalyzer.ValueTypeToReferenceTypeInAStringConcatenationRule.Id, line: 3, character: 33);
            // Diagnostic: (9,43): warning HeapAnalyzerStringConcatRule: Considering using StringBuilder
            AssertEx.ContainsDiagnostic(info.Allocations, id: ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id, line: 3, character: 31);
            // Diagnostic: (9,49): warning HeapAnalyzerStringConcatRule: Considering using StringBuilder
            AssertEx.ContainsDiagnostic(info.Allocations, id: ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id, line: 3, character: 37);

            //### CODE ### 5.ToString() + ":" + 8.ToString()
            // Diagnostic: (10,46): warning HeapAnalyzerStringConcatRule: Considering using StringBuilder
            AssertEx.ContainsDiagnostic(info.Allocations, id: ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id, line: 4, character: 34);
            // Diagnostic: (10,52): warning HeapAnalyzerStringConcatRule: Considering using StringBuilder
            AssertEx.ContainsDiagnostic(info.Allocations, id: ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id, line: 4, character: 40);
        }

        [TestMethod]
        public void ConcatenationAllocation_DoNotWarnForConst() {
            var snippets = new[]
            {
                @"const string s0 = nameof(System.String) + ""."" + nameof(System.String);",
                @"const string s0 = nameof(System.String) + ""."";",
                @"string s0 = nameof(System.String) + ""."" + nameof(System.String);",
                @"string s0 = nameof(System.String) + ""."";"
            };

            var analyser = new ConcatenationAllocationAnalyzer();
            foreach (var snippet in snippets) {
                var info = ProcessCode(analyser, snippet, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
                Assert.AreEqual(0, info.Allocations.Count);
            }
        }
    }
}
