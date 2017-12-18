using System.Runtime.InteropServices;
using System.Windows.Forms;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.VisualStudio.Shell;

namespace ClrHeapAllocationAnalyzer.Vsix {
    [Guid("00000000-0000-0000-0000-000000000000")]
    public class RulesOptionPage : DialogPage
    {
        private RulesOptionsControl page;

        protected override IWin32Window Window => page ?? (page = new RulesOptionsControl());

        protected override void OnApply(PageApplyEventArgs args) {
            base.OnApply(args);

            foreach (var d in page.GetDescriptions())
            {
                AllocationRules.Settings.SetSeverity(d.Id, d.Severity);
            }
        }
    }
}
