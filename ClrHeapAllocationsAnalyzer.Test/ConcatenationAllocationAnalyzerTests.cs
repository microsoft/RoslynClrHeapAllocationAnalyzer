using System.Collections.Immutable;
using System.Linq;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ClrHeapAllocationAnalyzer.Test
{
    [TestClass]
    public class ConcatenationAllocationAnalyzerTests : AllocationAnalyzerTests {

        [TestMethod]
        public void ConcatenationAllocation_Basic() {
            var snippet0 = @"string s0 = ""hello"" + 0.ToString() + ""world"" + 1.ToString();";
            var snippet1 = @"string s2 = ""ohell"" + 2.ToString() + ""world"" + 3.ToString() + 4.ToString();";

            var analyser = new ConcatenationAllocationAnalyzer();
            var info0 = ProcessCode(analyser, snippet0, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
            var info1 = ProcessCode(analyser, snippet1, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
            //Assert.AreEqual(1, info.Allocations.Count(d => d.Id == AllocationRules.ValueTypeToReferenceTypeInAStringConcatenationRule.Id)); //TODO
            //Assert.AreEqual(4, info.Allocations.Count(d => d.Id == AllocationRules.StringConcatenationAllocationRule.Id)); //TODO
       
            Assert.AreEqual(0, info0.Allocations.Count(d => d.Id == AllocationRules.StringConcatenationAllocationRule.Id));
            Assert.AreEqual(1, info1.Allocations.Count(d => d.Id == AllocationRules.StringConcatenationAllocationRule.Id));
            //AssertEx.ContainsDiagnostic(info.Allocations, id: AllocationRules.ValueTypeToReferenceTypeInAStringConcatenationRule.Id, line: 3, character: 33); //TODO
            //AssertEx.ContainsDiagnostic(info.Allocations, id: AllocationRules.StringConcatenationAllocationRule.Id, line: 3, character: 31); //TODO
            //AssertEx.ContainsDiagnostic(info.Allocations, id: AllocationRules.StringConcatenationAllocationRule.Id, line: 3, character: 37); //TODO
            AssertEx.ContainsDiagnostic(info1.Allocations, id: AllocationRules.StringConcatenationAllocationRule.Id, line: 1, character: 13);
            //AssertEx.ContainsDiagnostic(info.Allocations, id: AllocationRules.StringConcatenationAllocationRule.Id, line: 4, character: 34); //TODO
            //AssertEx.ContainsDiagnostic(info.Allocations, id: AllocationRules.StringConcatenationAllocationRule.Id, line: 4, character: 40); //TODO
        }

        [TestMethod]
        public void ConcatenationAllocation_DoNotWarnForOptimizedValueTypes() {
            var snippets = new[]
            {
                @"string s0 = nameof(System.String) + '-';",
                @"string s0 = nameof(System.String) + true;",
                @"string s0 = nameof(System.String) + new System.IntPtr();",
                @"string s0 = nameof(System.String) + new System.UIntPtr();"
            };

            var analyser = new ConcatenationAllocationAnalyzer();
            foreach (var snippet in snippets) {
                var info = ProcessCode(analyser, snippet, ImmutableArray.Create(SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
                Assert.AreEqual(0, info.Allocations.Count(x => x.Id == AllocationRules.ValueTypeToReferenceTypeInAStringConcatenationRule.Id));
            }
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
