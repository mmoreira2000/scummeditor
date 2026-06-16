namespace ScummEditor.Gui
{
    partial class DiskBlockControl
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
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.TreeImages = new System.Windows.Forms.TreeView();
            this.DecodeTransparent = new System.Windows.Forms.CheckBox();
            this.Palettes = new System.Windows.Forms.ComboBox();
            this.Contents.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.splitContainer1);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.TreeImages);
            this.splitContainer1.Size = new System.Drawing.Size(481, 287);
            this.splitContainer1.SplitterDistance = 155;
            this.splitContainer1.TabIndex = 0;
            // 
            // TreeImages
            // 
            this.TreeImages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.TreeImages.FullRowSelect = true;
            this.TreeImages.HideSelection = false;
            this.TreeImages.Location = new System.Drawing.Point(0, 0);
            this.TreeImages.Name = "TreeImages";
            this.TreeImages.Size = new System.Drawing.Size(155, 287);
            this.TreeImages.TabIndex = 0;
            this.TreeImages.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.TreeImages_AfterSelect);
            // 
            // DecodeTransparent
            // 
            this.DecodeTransparent.AutoSize = true;
            this.DecodeTransparent.Location = new System.Drawing.Point(228, 35);
            this.DecodeTransparent.Name = "DecodeTransparent";
            this.DecodeTransparent.Size = new System.Drawing.Size(124, 17);
            this.DecodeTransparent.TabIndex = 8;
            this.DecodeTransparent.Text = "Decode Transparent";
            this.DecodeTransparent.UseVisualStyleBackColor = true;
            this.DecodeTransparent.CheckedChanged += new System.EventHandler(this.DecodeTransparent_CheckedChanged);
            // 
            // Palettes
            // 
            this.Palettes.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.Palettes.FormattingEnabled = true;
            this.Palettes.Location = new System.Drawing.Point(350, 32);
            this.Palettes.Name = "Palettes";
            this.Palettes.Size = new System.Drawing.Size(137, 21);
            this.Palettes.TabIndex = 9;
            this.Palettes.SelectedIndexChanged += new System.EventHandler(this.Palettes_SelectedIndexChanged);
            // 
            // DiskBlockControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.Palettes);
            this.Controls.Add(this.DecodeTransparent);
            this.Name = "DiskBlockControl";
            this.Controls.SetChildIndex(this.Contents, 0);
            this.Controls.SetChildIndex(this.DecodeTransparent, 0);
            this.Controls.SetChildIndex(this.Palettes, 0);
            this.Contents.ResumeLayout(false);
            this.splitContainer1.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TreeView TreeImages;
        private System.Windows.Forms.CheckBox DecodeTransparent;
        private System.Windows.Forms.ComboBox Palettes;
    }
}
