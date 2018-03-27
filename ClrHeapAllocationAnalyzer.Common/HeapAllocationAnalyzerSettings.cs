using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Common {
    public class HeapAllocationAnalyzerSettings : IHeapAllocationAnalyzerSettings
    {
        private const string CollectionPath = "HeapAllocationAnalyzer";

        private readonly IWritableSettingsStore settingsStore;

        private bool enabled;

        private readonly IDictionary<string, DiagnosticSeverity> ruleSeverities = new Dictionary<string, DiagnosticSeverity>();

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

        public HeapAllocationAnalyzerSettings(IWritableSettingsStore settingsStore)
        {
            this.settingsStore = settingsStore;
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

                foreach (var rule in ruleSeverities)
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
