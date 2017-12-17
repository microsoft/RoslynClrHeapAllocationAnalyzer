using System;
using System.Windows.Forms;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Vsix {
    public partial class RulesOptionsControl : UserControl
    {
        private readonly BindingSource bindingSource = new BindingSource();

        public RulesOptionsControl()
        {
            InitializeComponent();
            Load += OnLoad;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            foreach (var d in AllocationRules.GetDescriptions())
            {
                bindingSource.Add(AllocationRuleDescription.FromFullDescription(d));
            }

            gvRules.AutoGenerateColumns = false;
            gvRules.AutoSize = true;
            gvRules.DataSource = bindingSource;
            gvRules.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            DataGridViewColumn column = new DataGridViewTextBoxColumn();
            column.DataPropertyName = "Id";
            column.Name = "ID";
            column.FillWeight = 0.15f;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            gvRules.Columns.Add(column);

            column = new DataGridViewTextBoxColumn();
            column.DataPropertyName = "Title";
            column.Name = "Title";
            column.FillWeight = 0.65f;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            gvRules.Columns.Add(column);

            DataGridViewComboBoxColumn combo = new DataGridViewComboBoxColumn();
            combo.DataSource = Enum.GetValues(typeof(DiagnosticSeverity));
            combo.DataPropertyName = "Severity";
            combo.Name = "Severity";
            combo.FillWeight = 0.20f;
            combo.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            gvRules.Columns.Add(combo);
        }

        /// <summary>
        /// Mutable version used for showing in the UI.
        /// </summary>
        private class AllocationRuleDescription
        {
            public string Id { get; }
            public string Title { get; }
            public DiagnosticSeverity Severity { get; set; }

            public AllocationRuleDescription(string id, string title, DiagnosticSeverity severity)
            {
                Id = id;
                Title = title;
                Severity = severity;
            }

            public static AllocationRuleDescription FromFullDescription(Common.AllocationRuleDescription d)
            {
                return new AllocationRuleDescription(d.Id, d.Title, d.Severity);
            }
        }
    }
}
