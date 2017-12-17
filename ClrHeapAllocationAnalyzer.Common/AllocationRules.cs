using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public static partial class AllocationRules
    {
        private static IHeapAllocationAnalyzerSettings settings;

        private static readonly IReadOnlyDictionary<string, DiagnosticDescriptor> Empty =
            new Dictionary<string, DiagnosticDescriptor>();

        private static readonly Dictionary<string, AllocationRuleDescription> Descriptions =
            new Dictionary<string, AllocationRuleDescription>();

        static AllocationRules()
        {
            foreach (AllocationRuleDescription rule in DefaultValues())
            {
                Descriptions.Add(rule.Id, rule);
            }
        }

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
        
        public static DiagnosticDescriptor GetDescriptor(string ruleId)
        {
            if (!Descriptions.ContainsKey(ruleId))
            {
                throw new ArgumentException($"Cannot find description for rule {ruleId}", nameof(ruleId));
            }

            AllocationRuleDescription d = Descriptions[ruleId];
            bool isEnabled = d.Severity != DiagnosticSeverity.Hidden;
            return new DiagnosticDescriptor(d.Id, d.Title, d.MessageFormat, "Performance", d.Severity, isEnabled, helpLinkUri: d.HelpLinkUri);
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

        private static void OnSettingsChanged(object sender, EventArgs eventArgs)
        {
            var descriptionsCopy = new Dictionary<string, AllocationRuleDescription>(Descriptions);
            foreach (var d in descriptionsCopy)
            {
                DiagnosticSeverity severity = Settings.GetSeverity(d.Key, d.Value.Severity);
                if (Descriptions[d.Key].Severity != severity)
                {
                    Descriptions[d.Key] = Descriptions[d.Key].WithSeverity(severity);
                }
            }
        }
    }
}
