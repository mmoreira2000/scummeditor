namespace ScummEditor.Gui
{
    partial class ZPlaneControl
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
            this.ExportTable = new System.Windows.Forms.Button();
            this.StripCount = new System.Windows.Forms.Label();
            this.Strips = new System.Windows.Forms.ListView();
            this.Offset = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ImageData = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.ExportTable);
            this.Contents.Controls.Add(this.StripCount);
            this.Contents.Controls.Add(this.Strips);
            // 
            // ExportTable
            // 
            this.ExportTable.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ExportTable.Location = new System.Drawing.Point(404, 3);
            this.ExportTable.Name = "ExportTable";
            this.ExportTable.Size = new System.Drawing.Size(75, 23);
            this.ExportTable.TabIndex = 8;
            this.ExportTable.Text = "Export Table";
            this.ExportTable.UseVisualStyleBackColor = true;
            this.ExportTable.Click += new System.EventHandler(this.ExportTable_Click);
            // 
            // StripCount
            // 
            this.StripCount.AutoSize = true;
            this.StripCount.Location = new System.Drawing.Point(-2, 0);
            this.StripCount.Name = "StripCount";
            this.StripCount.Size = new System.Drawing.Size(35, 13);
            this.StripCount.TabIndex = 7;
            this.StripCount.Text = "label4";
            // 
            // Strips
            // 
            this.Strips.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Strips.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Offset,
            this.ImageData});
            this.Strips.FullRowSelect = true;
            this.Strips.Location = new System.Drawing.Point(1, 26);
            this.Strips.Name = "Strips";
            this.Strips.Size = new System.Drawing.Size(481, 261);
            this.Strips.TabIndex = 6;
            this.Strips.UseCompatibleStateImageBehavior = false;
            this.Strips.View = System.Windows.Forms.View.Details;
            // 
            // Offset
            // 
            this.Offset.Text = "Offset";
            this.Offset.Width = 100;
            // 
            // ImageData
            // 
            this.ImageData.Text = "Image Data Length";
            this.ImageData.Width = 255;
            // 
            // ZPlaneStripTableControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ZPlaneStripTableControl";
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button ExportTable;
        private System.Windows.Forms.Label StripCount;
        private System.Windows.Forms.ListView Strips;
        private System.Windows.Forms.ColumnHeader Offset;
        private System.Windows.Forms.ColumnHeader ImageData;
    }
}
