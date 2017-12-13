using ClrHeapAllocationAnalyzer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;

namespace ClrHeapAllocationsAnalyzer.Test
{
    [TestClass]
    public class ConcatenationAllocationAnalyzerTests : AllocationAnalyzerTests {
        [TestMethod]
        public void ConcatenationAllocation_Basic() {
            var snippet0 = @"string s0 = ""hello"" + 0.ToString() + ""world"" + 1.ToString();";
            var snippet1 = @"string s2 = ""hello"" + 2.ToString() + ""world"" + 3.ToString() + 4.ToString();";

            var analyser = new ConcatenationAllocationAnalyzer();
            var info0 = ProcessCode(analyser, snippet0, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
            var info1 = ProcessCode(analyser, snippet1, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));

            Assert.AreEqual(0, info0.Allocations.Count(d => d.Id == ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id));
            Assert.AreEqual(1, info1.Allocations.Count(d => d.Id == ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id));

            AssertEx.ContainsDiagnostic(info1.Allocations, id: ConcatenationAllocationAnalyzer.StringConcatenationAllocationRule.Id, line: 1, character: 13);
        }
    }
}
