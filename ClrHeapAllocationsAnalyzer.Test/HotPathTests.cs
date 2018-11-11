using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Immutable;

namespace ClrHeapAllocationAnalyzer.Test
{
    [TestClass]
    public class HotPathTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void AnalyzeProgram_MethodWithAttribute_OtherMethodIgnored()
        {
            const string sampleProgram =
                @"using System;
                using Microsoft.Diagnostics;
                
                [PerformanceCritical]
                public void CreateString1() {
                    string str = new string('a', 5);
                }

                public void CreateString2() {
                    string str = new string('b', 5);
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(1, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 6);
        }

        [TestMethod]
        public void AnalyzeProgram_MethodsWithAttribute_BothAnalyzed()
        {
            const string sampleProgram =
                @"using System;
                using Microsoft.Diagnostics;
                
                [PerformanceCritical]
                public void CreateString1() {
                    string str = new string('a', 5);
                }
                
                [PerformanceCritical]
                public void CreateString2() {
                    string str = new string('a', 5);
                }";

            var analyser = new ExplicitAllocationAnalyzer();
            var info = ProcessCode(analyser, sampleProgram, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression));
            Assert.AreEqual(2, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 6);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 11);
        }
    }
}
