namespace ClrHeapAllocationAnalyzer.Vsix {
    partial class RulesOptionsControl {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.gvRules = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.gvRules)).BeginInit();
            this.SuspendLayout();
            // 
            // gvRules
            // 
            this.gvRules.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gvRules.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gvRules.Location = new System.Drawing.Point(0, 0);
            this.gvRules.Name = "gvRules";
            this.gvRules.RowHeadersVisible = false;
            this.gvRules.Size = new System.Drawing.Size(722, 364);
            this.gvRules.TabIndex = 1;
            // 
            // RulesOptionsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.Controls.Add(this.gvRules);
            this.Name = "RulesOptionsControl";
            this.Size = new System.Drawing.Size(722, 364);
            ((System.ComponentModel.ISupportInitialize)(this.gvRules)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView gvRules;
    }
}
