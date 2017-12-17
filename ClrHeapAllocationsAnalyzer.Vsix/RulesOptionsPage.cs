using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;

namespace ClrHeapAllocationAnalyzer.Vsix {
    [Guid("00000000-0000-0000-0000-000000000000")]
    public class RulesOptionPage : DialogPage {
        public string OptionString { get; set; } = "alpha";

        protected override IWin32Window Window {
            get {
                RulesOptionsControl page = new RulesOptionsControl();
                return page;
            }
        }
    }
}
