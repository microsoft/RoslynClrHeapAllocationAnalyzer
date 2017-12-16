using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public static class AllocationRules
    {
        private static IHeapAllocationAnalyzerSettings settings;

        private static readonly IReadOnlyDictionary<string, DiagnosticDescriptor> Empty =
            new Dictionary<string, DiagnosticDescriptor>();

        private static readonly Dictionary<string, AllocationRuleDescription> Descriptions =
            new Dictionary<string, AllocationRuleDescription>();

        public static IHeapAllocationAnalyzerSettings Settings
        {
            get => settings;
            set
            {
                if (settings != null)
                {
                    settings.SettingsChanged -= OnSettingsChanged;
                }

                settings = value;

                if (settings != null)
                {
                    settings.SettingsChanged += OnSettingsChanged;
                }
            }
        }

        public static void RegisterAnalyzerRule(AllocationRuleDescription defaultDescription)
        {
            if (Descriptions.ContainsKey(defaultDescription.Id))
            {
                throw new ArgumentException($"Already have a rule with id '{defaultDescription.Id}'", nameof(defaultDescription));
            }

            DiagnosticSeverity severity = Settings.GetSeverity(defaultDescription.Id) ?? defaultDescription.Severity;
            Descriptions.Add(defaultDescription.Id, defaultDescription.WithSeverity(severity));
        }

        public static DiagnosticDescriptor GetDescriptor(string ruleId)
        {
            if (!Descriptions.ContainsKey(ruleId))
            {
                throw new ArgumentException($"Cannot find description for rule {ruleId}", nameof(ruleId));
            }

            AllocationRuleDescription d = Descriptions[ruleId];
            return new DiagnosticDescriptor(d.Id, d.Title, d.MessageFormat, "Performance", d.Severity, true);
        }

        public static IEnumerable<AllocationRuleDescription> GetDescriptions()
        {
            return Descriptions.Values;
        }

        public static IReadOnlyDictionary<string, DiagnosticDescriptor> GetEnabled(IEnumerable<string> ruleIds)
        {
            Dictionary<string, DiagnosticDescriptor> result = null;
            foreach (var ruleId in ruleIds) {
                if (IsEnabled(ruleId)) {
                    if (result == null)
                    {
                        result = new Dictionary<string, DiagnosticDescriptor>();
                    }

                    result.Add(ruleId, GetDescriptor(ruleId));
                }
            }

            return result ?? Empty;
        }

        public static bool IsEnabled(string ruleId) {
            if (!Descriptions.ContainsKey(ruleId)) {
                throw new ArgumentException($"Cannot find description for rule {ruleId}", nameof(ruleId));
            }

            return Settings.Enabled && Descriptions[ruleId].Severity != DiagnosticSeverity.Hidden;
        }

        private static void OnSettingsChanged(object sender, EventArgs eventArgs) {
            foreach (var d in Descriptions)
            {
                DiagnosticSeverity severity = Settings.GetSeverity(d.Key) ?? throw new Exception($"Cannot find severity for rule {d.Key}");
                if (Descriptions[d.Key].Severity != severity)
                {
                    Descriptions[d.Key] = Descriptions[d.Key].WithSeverity(severity);
                }
            }
        }
    }
}
