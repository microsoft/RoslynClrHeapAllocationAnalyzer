using System.ComponentModel;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.VisualStudio.Shell;

namespace ClrHeapAllocationAnalyzer.Vsix {
    public class GeneralOptionsPage : DialogPage
    {
        [Category("Heap Allocation Analyzer")]
        [DisplayName("Enabled")]
        [Description("Determines whether to run any analysis or not. Note that the extension is still loaded and executed. Use the 'Extension and Updates' to disable or remove it permanentely.")]
        public bool Enabled { get; set; }

        public GeneralOptionsPage()
        {
            Enabled = AllocationRules.Settings.Enabled;
        }

        protected override void OnApply(PageApplyEventArgs args)
        {
            base.OnApply(args);
            AllocationRules.Settings.Enabled = Enabled;
        }
    }
}
