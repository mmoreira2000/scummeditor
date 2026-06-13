namespace ScummEditor.Gui
{
    partial class ObjectCodeControl
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
            this.split = new System.Windows.Forms.SplitContainer();
            this.grid = new System.Windows.Forms.DataGridView();
            this.code = new System.Windows.Forms.TextBox();
            this.Contents.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split)).BeginInit();
            this.split.Panel1.SuspendLayout();
            this.split.Panel2.SuspendLayout();
            this.split.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            this.SuspendLayout();
            //
            // Contents
            //
            this.Contents.Controls.Add(this.split);
            //
            // split
            //
            this.split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split.Name = "split";
            this.split.Orientation = System.Windows.Forms.Orientation.Vertical;
            //
            // split.Panel1
            //
            this.split.Panel1.Controls.Add(this.grid);
            //
            // split.Panel2
            //
            this.split.Panel2.Controls.Add(this.code);
            this.split.SplitterDistance = 230;
            this.split.TabIndex = 0;
            //
            // grid
            //
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.grid.Name = "grid";
            this.grid.ReadOnly = true;
            this.grid.RowHeadersVisible = false;
            this.grid.AllowUserToResizeRows = false;
            this.grid.TabIndex = 0;
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
            // ObjectCodeControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ObjectCodeControl";
            this.split.Panel1.ResumeLayout(false);
            this.split.Panel2.ResumeLayout(false);
            this.split.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split)).EndInit();
            this.split.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            this.Contents.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.SplitContainer split;
        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.TextBox code;
    }
}
