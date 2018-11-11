using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Immutable;

namespace ClrHeapAllocationAnalyzer.Test
{
    [TestClass]
    public class HotPathTests : AllocationAnalyzerTests
    {
        [TestMethod]
        public void AnalyzeProgram_MethodWithDefaultAttribute_OtherMethodIgnored()
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
        public void AnalyzeProgram_MethodsWithDefaultAttribute_BothAnalyzed()
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

        [TestMethod]
        public void AnalyzeProgram_MethodWithExplicitAttribute_OnlyExplicitWarning()
        {
            const string sampleProgram =
                @"using System;
                using Microsoft.Diagnostics;
                
                [PerformanceCritical(Allocations = AllocationKind.Explicit)]
                public void CreateString1() {
                    string s0 = new string('a', 5);
                    string s1 = ""this will box"" + 1;
                }";

            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ExplicitAllocationAnalyzer(), new ConcatenationAllocationAnalyzer());
            var info = ProcessCode(analyzers, sampleProgram, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression, SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
            Assert.AreEqual(1, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 6);
        }

        [TestMethod]
        public void AnalyzeProgram_MethodWithTwoAttributes_WarnForAll()
        {
            const string sampleProgram =
                @"using System;
                using Microsoft.Diagnostics;
                
                [PerformanceCritical(Allocations = AllocationKind.Explicit)]
                [PerformanceCritical(Allocations = AllocationKind.All)]
                public void CreateString1() {
                    string s0 = new string('a', 5);
                    string s1 = ""this will box"" + 1;
                }";

            var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ExplicitAllocationAnalyzer(), new ConcatenationAllocationAnalyzer());
            var info = ProcessCode(analyzers, sampleProgram, ImmutableArray.Create(SyntaxKind.ObjectInitializerExpression, SyntaxKind.AddExpression, SyntaxKind.AddAssignmentExpression));
            Assert.AreEqual(2, info.Allocations.Count);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ExplicitAllocationAnalyzer.NewObjectRule.Id, line: 7);
            AssertEx.ContainsDiagnostic(info.Allocations, id: ConcatenationAllocationAnalyzer.ValueTypeToReferenceTypeInAStringConcatenationRule.Id, line: 8);
        }
    }
}