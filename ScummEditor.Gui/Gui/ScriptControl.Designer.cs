namespace ScummEditor.Gui
{
    partial class ScriptControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.code = new System.Windows.Forms.TextBox();
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            //
            // Contents
            //
            this.Contents.Controls.Add(this.code);
            //
            // code
            //
            this.code.BackColor = System.Drawing.Color.White;
            this.code.Dock = System.Windows.Forms.DockStyle.Fill;
            this.code.Font = new System.Drawing.Font("Consolas", 9F);
            this.code.HideSelection = false;
            this.code.MaxLength = 0;
            this.code.Multiline = true;
            this.code.Name = "code";
            this.code.ReadOnly = true;
            this.code.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.code.TabIndex = 0;
            this.code.WordWrap = false;
            //
            // ScriptControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ScriptControl";
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TextBox code;
    }
}
