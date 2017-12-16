using System;
using System.Diagnostics;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Settings;

namespace ClrHeapAllocationAnalyzer.Vsix {
    public class HeapAllocationAnalyzerSettings : IHeapAllocationAnalyzerSettings
    {
        private const string CollectionPath = "HeapAllocationAnalyzer";
        private readonly WritableSettingsStore settingsStore;

        private bool enabled;

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

        public DiagnosticSeverity? GetSeverity(string ruleId)
        {
            return null; // TODO
        }

        public HeapAllocationAnalyzerSettings(WritableSettingsStore settingsStore)
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
                if (settingsStore.PropertyExists(CollectionPath, "Enabled"))
                {
                    Enabled = settingsStore.GetBoolean(CollectionPath, "Enabled");
                } 
                else
                {
                    Enabled = true;
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
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }
    }
}
