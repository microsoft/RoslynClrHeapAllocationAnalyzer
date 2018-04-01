using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ClrHeapAllocationAnalyzer.Common;
using Microsoft.CodeAnalysis;

namespace ClrHeapAllocationAnalyzer.Vsix {
    public partial class RulesOptionsControl : UserControl
    {
        private readonly BindingSource bindingSource = new BindingSource();
        private readonly IDictionary<string, DiagnosticSeverity> newSeverities = new Dictionary<string, DiagnosticSeverity>();

        public RulesOptionsControl()
        {
            InitializeComponent();
            InitializeGridView();
        }

        public IEnumerable<Common.AllocationRuleDescription> GetDescriptions() {
            foreach (AllocationRuleDescription d in bindingSource) {
                Console.WriteLine(d.Id + newSeverities.ContainsKey(d.Id));
                if (newSeverities.ContainsKey(d.Id)) {
                    yield return d.ToFullDescription(newSeverities[d.Id]);
                } else {
                    yield return d.ToFullDescription();
                }
            }
        }

        private void InitializeGridView()
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

            gvRules.EditingControlShowing += GridView_EditingControlShowing;
        }

        private void GridView_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e) {
            var comboBox = e.Control as ComboBox;
            if (comboBox == null) return;
            int row = gvRules.CurrentCell.RowIndex;
            var ruleDescription = bindingSource[row] as AllocationRuleDescription;
            comboBox.Tag = ruleDescription.Id;
            comboBox.SelectedIndexChanged += ComboBox_SelectedIndexChanged;
        }

        private void ComboBox_SelectedIndexChanged(object sender, EventArgs e) {
            var comboBox = sender as ComboBox;
            if (comboBox.SelectedValue == null) return;
            newSeverities[comboBox.Tag as string] = (DiagnosticSeverity)comboBox.SelectedValue;
        }

        /// <summary>
        /// Mutable version used for showing in the UI.
        /// </summary>
        private class AllocationRuleDescription
        {
            public string Id { get; }
            public string Title { get; }
            public string MessageFormat { get; }
            public DiagnosticSeverity Severity { get; set; }
            public string HelpLinkUri { get; }

            public AllocationRuleDescription(string id, string title, string messageFormat, DiagnosticSeverity severity, string helpLinkUri)
            {
                Id = id;
                Title = title;
                MessageFormat = messageFormat;
                Severity = severity;
                HelpLinkUri = helpLinkUri;
            }

            public Common.AllocationRuleDescription ToFullDescription() {
                return new Common.AllocationRuleDescription(Id, Title, MessageFormat, Severity, HelpLinkUri);
            }

            public Common.AllocationRuleDescription ToFullDescription(DiagnosticSeverity newSeverity) {
                return new Common.AllocationRuleDescription(Id, Title, MessageFormat, newSeverity, HelpLinkUri);
            }

            public static AllocationRuleDescription FromFullDescription(Common.AllocationRuleDescription d) {
                return new AllocationRuleDescription(d.Id, d.Title, d.MessageFormat, d.Severity, d.HelpLinkUri);
            }
        }
    }
}
