using System;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public interface IHeapAllocationAnalyzerSettings
    {
        event EventHandler SettingsChanged;

        bool Enabled { get; set; }

        DiagnosticSeverity GetSeverity(string ruleId, DiagnosticSeverity defaultValue);

        DiagnosticSeverity GetSeverity(AllocationRuleDescription defaultDescription);

        void SetSeverity(string ruleId, DiagnosticSeverity severity);
    }
}
