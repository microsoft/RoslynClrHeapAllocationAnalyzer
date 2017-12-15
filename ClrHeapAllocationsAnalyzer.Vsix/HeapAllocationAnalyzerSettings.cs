using System;
using System.Diagnostics;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.VisualStudio.Settings;

namespace ClrHeapAllocationAnalyzer.Vsix {
    public class HeapAllocationAnalyzerSettings : IHeapAllocationAnalyzerSettings
    {
        private const string CollectionPath = "HeapAllocationAnalyzer";
        private readonly WritableSettingsStore settingsStore;

        private bool enabled;

        public bool Enabled
        {
            get => enabled;
            set {
                if (value != enabled)
                {
                    enabled = value;
                    SaveSettings();
                }
            }
        }

        public HeapAllocationAnalyzerSettings(WritableSettingsStore settingsStore)
        {
            this.settingsStore = settingsStore;
            LoadSettings();
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
            catch (Exception ex) {
                Debug.Fail(ex.Message);
            }
        }

        private void SaveSettings() {
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
