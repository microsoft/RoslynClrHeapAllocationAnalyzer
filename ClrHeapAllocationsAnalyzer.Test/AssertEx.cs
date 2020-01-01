using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace ClrHeapAllocationAnalyzer.Test
{
    public static class AssertEx
    {
        public static void ContainsDiagnostic(List<Diagnostic> diagnostics, string id, int line, int character)
        {
            var msg = string.Format("\r\nExpected {0} at ({1},{2}), i.e. line {1}, at character position {2})\r\nDiagnostics:\r\n{3}\r\n", 
                                    id,  line, character, string.Join("\r\n", diagnostics));
            var reportingOccurenceCount = CountReportedDiagnostics(diagnostics, id, line, character);
            Assert.AreEqual(1, reportingOccurenceCount, message: msg);
        }
        
        public static void ContainNoDiagnostic(List<Diagnostic> diagnostics, string id, int line, int character)
        {
            var msg = string.Format("\r\nExpected no {0} at ({1},{2}), i.e. line {1}, at character position {2})\r\nDiagnostics:\r\n{3}\r\n", 
                                    id,  line, character, string.Join("\r\n", diagnostics));
            var reportingOccurenceCount = CountReportedDiagnostics(diagnostics, id, line, character);
            Assert.AreEqual(0, reportingOccurenceCount, message: msg);
        }

        private static int CountReportedDiagnostics(IReadOnlyCollection<Diagnostic> diagnostics, string id, int line, int character)
        {
            return diagnostics.Where(d => d.Id == id).Count(d =>
            {
                var startLinePosition = d.Location.GetLineSpan().StartLinePosition;
                return startLinePosition.Line + 1 == line && startLinePosition.Character + 1 == character;
            });
        }
    }
}
