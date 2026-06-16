namespace ScummEditor.Gui
{
    partial class PaletteDataControl
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.PaletteTable = new System.Windows.Forms.FlowLayoutPanel();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.PaletteTable);
            // 
            // PaletteTable
            // 
            this.PaletteTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PaletteTable.Location = new System.Drawing.Point(0, 0);
            this.PaletteTable.Name = "PaletteTable";
            this.PaletteTable.Size = new System.Drawing.Size(481, 287);
            this.PaletteTable.TabIndex = 6;
            // 
            // PaletteDataControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "PaletteDataControl";
            this.Contents.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel PaletteTable;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
