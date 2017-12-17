using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public struct AllocationRuleDescription {
        public string Id { get; }
        public string Title { get; }
        public string MessageFormat { get; }
        public DiagnosticSeverity Severity { get; }
        public string HelpLinkUri { get; }

        public AllocationRuleDescription(string id, string title, string messageFormat, DiagnosticSeverity severity) {
            Id = id;
            Title = title;
            MessageFormat = messageFormat;
            Severity = severity;
            HelpLinkUri = null;
        }

        public AllocationRuleDescription(string id, string title, string messageFormat, DiagnosticSeverity severity, string helpLinkUri) {
            Id = id;
            Title = title;
            MessageFormat = messageFormat;
            Severity = severity;
            HelpLinkUri = helpLinkUri;
        }

        public AllocationRuleDescription WithSeverity(DiagnosticSeverity severity) {
            return new AllocationRuleDescription(Id, Title, MessageFormat, severity);
        }
    }
}
