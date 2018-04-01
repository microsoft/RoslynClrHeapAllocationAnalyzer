using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public static partial class AllocationRules
    {
        private static IHeapAllocationAnalyzerSettings settings;

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

                // We now have access to settings. Load the severities for the
                // rules.
                LoadSeverities();

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
            DiagnosticSeverity severity = d.Severity;
            if (settings != null)
            {
                severity = settings.GetSeverity(d);
            }
             
            bool isEnabled = severity != DiagnosticSeverity.Hidden;
            return new DiagnosticDescriptor(d.Id, d.Title, d.MessageFormat, "Performance", severity, isEnabled, helpLinkUri: d.HelpLinkUri);
        }

        public static IEnumerable<AllocationRuleDescription> GetDescriptions()
        {
            return Descriptions.Values;
        }

        public static EnabledRules GetEnabled(IEnumerable<string> ruleIds)
        {
            if (!settings.Enabled)
            {
                return EnabledRules.None;
            }

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

            return result != null ? new EnabledRules(result) : EnabledRules.None;
        }

        public static bool IsEnabled(string ruleId) {
            if (!Descriptions.ContainsKey(ruleId)) {
                throw new ArgumentException($"Cannot find description for rule {ruleId}", nameof(ruleId));
            }

            return Settings.Enabled && Descriptions[ruleId].Severity != DiagnosticSeverity.Hidden;
        }

        private static void OnSettingsChanged(object sender, EventArgs eventArgs)
        {
            LoadSeverities();
        }

        private static void LoadSeverities()
        {
            var descriptionsCopy = new Dictionary<string, AllocationRuleDescription>(Descriptions);
            foreach (var d in descriptionsCopy) {
                DiagnosticSeverity severity = Settings.GetSeverity(d.Key, d.Value.Severity);
                if (Descriptions[d.Key].Severity != severity) {
                    Descriptions[d.Key] = Descriptions[d.Key].WithSeverity(severity);
                }
            }
        }
    }
}
