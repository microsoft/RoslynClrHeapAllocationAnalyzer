using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public class HeapAllocationAnalyzerSettings : IHeapAllocationAnalyzerSettings
    {
        private const string CollectionPath = "HeapAllocationAnalyzer";

        private readonly IWritableSettingsStore settingsStore;

        private bool enabled = true;

        private readonly IDictionary<string, DiagnosticSeverity> ruleSeverities;

        public event EventHandler SettingsChanged;

        public bool Enabled
        {
            get => enabled;
            set {
                if (value != enabled)
                {
                    enabled = value;
                    OnSettingsChanged();
                }
            }
        }

        public DiagnosticSeverity GetSeverity(string ruleId, DiagnosticSeverity defaultSeverity)
        {
            if (ruleSeverities.ContainsKey(ruleId))
            {
                return ruleSeverities[ruleId];
            }
            else
            {
                ruleSeverities.Add(ruleId, defaultSeverity);
                return defaultSeverity;
            }
        }

        public DiagnosticSeverity GetSeverity(AllocationRuleDescription defaultDescription)
        {
            return GetSeverity(defaultDescription.Id, defaultDescription.Severity);
        }

        public void SetSeverity(string ruleId, DiagnosticSeverity severity)
        {
            if (ruleSeverities.ContainsKey(ruleId))
            {
                if (ruleSeverities[ruleId] == severity)
                {
                    return;
                }
                else
                {
                    ruleSeverities[ruleId] = severity;
                }
            }
            else
            {
                ruleSeverities.Add(ruleId, severity);
            }

            OnSettingsChanged();
        }

        public HeapAllocationAnalyzerSettings(IWritableSettingsStore store, IEnumerable<AllocationRuleDescription> allRules = null)
        {
            settingsStore = store;
            ruleSeverities = allRules?.ToDictionary(x => x.Id, x => x.Severity) ?? new Dictionary<string, DiagnosticSeverity>();
            LoadSettings();
        }


        private void OnSettingsChanged()
        {
            SaveSettings();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void LoadSettings()
        {
            try
            {
                Enabled = settingsStore.GetBoolean(CollectionPath, "Enabled", true);

                var ruleSeveritiesCopy = new Dictionary<string, DiagnosticSeverity>(ruleSeverities);
                foreach (var rule in ruleSeveritiesCopy)
                {
                    int severity = settingsStore.GetInt32(CollectionPath, rule.Key, (int)rule.Value);
                    ruleSeverities[rule.Key] = (DiagnosticSeverity)severity;
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        private void SaveSettings()
        {
            try
            {
                if (!settingsStore.CollectionExists(CollectionPath))
                {
                    settingsStore.CreateCollection(CollectionPath);
                }

                settingsStore.SetBoolean(CollectionPath, "Enabled", Enabled);

                foreach (var rule in ruleSeverities)
                {
                    settingsStore.SetInt32(CollectionPath, rule.Key, (int)rule.Value);
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }
    }
}
