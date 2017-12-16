using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public struct AllocationRuleDescription {
        public string Id { get; }
        public string Title { get; }
        public string MessageFormat { get; }
        public DiagnosticSeverity Severity { get; }

        public AllocationRuleDescription(string id, string title, string messageFormat, DiagnosticSeverity severity) {
            Id = id;
            Title = title;
            MessageFormat = messageFormat;
            Severity = severity;
        }

        public AllocationRuleDescription WithSeverity(DiagnosticSeverity severity) {
            return new AllocationRuleDescription(Id, Title, MessageFormat, severity);
        }
    }
}
