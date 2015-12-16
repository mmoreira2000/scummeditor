namespace ScummEditor.Gui
{
    partial class RoomBlockImageControl
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
            this.pictureBackground = new System.Windows.Forms.PictureBox();
            this.TesteReencode = new System.Windows.Forms.Button();
            this.ImportFromDisk = new System.Windows.Forms.Button();
            this.SaveToDisk = new System.Windows.Forms.Button();
            this.pictureScroll = new System.Windows.Forms.Panel();
            this.Export8Bits = new System.Windows.Forms.CheckBox();
            this.CompressionMethod = new System.Windows.Forms.ComboBox();
            this.CompressionMethodLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBackground)).BeginInit();
            this.pictureScroll.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBackground
            // 
            this.pictureBackground.Location = new System.Drawing.Point(0, -1);
            this.pictureBackground.Name = "pictureBackground";
            this.pictureBackground.Size = new System.Drawing.Size(167, 114);
            this.pictureBackground.TabIndex = 11;
            this.pictureBackground.TabStop = false;
            // 
            // TesteReencode
            // 
            this.TesteReencode.Location = new System.Drawing.Point(76, 2);
            this.TesteReencode.Name = "TesteReencode";
            this.TesteReencode.Size = new System.Drawing.Size(31, 23);
            this.TesteReencode.TabIndex = 15;
            this.TesteReencode.Text = "!!";
            this.TesteReencode.UseVisualStyleBackColor = true;
            this.TesteReencode.Click += new System.EventHandler(this.TesteReencode_Click);
            // 
            // ImportFromDisk
            // 
            this.ImportFromDisk.Location = new System.Drawing.Point(189, 2);
            this.ImportFromDisk.Name = "ImportFromDisk";
            this.ImportFromDisk.Size = new System.Drawing.Size(70, 23);
            this.ImportFromDisk.TabIndex = 14;
            this.ImportFromDisk.Text = "Import";
            this.ImportFromDisk.UseVisualStyleBackColor = true;
            this.ImportFromDisk.Click += new System.EventHandler(this.ImportFromDisk_Click);
            // 
            // SaveToDisk
            // 
            this.SaveToDisk.Location = new System.Drawing.Point(113, 2);
            this.SaveToDisk.Name = "SaveToDisk";
            this.SaveToDisk.Size = new System.Drawing.Size(70, 23);
            this.SaveToDisk.TabIndex = 13;
            this.SaveToDisk.Text = "Export";
            this.SaveToDisk.UseVisualStyleBackColor = true;
            this.SaveToDisk.Click += new System.EventHandler(this.SaveToDisk_Click);
            // 
            // pictureScroll
            // 
            this.pictureScroll.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureScroll.AutoScroll = true;
            this.pictureScroll.Controls.Add(this.pictureBackground);
            this.pictureScroll.Location = new System.Drawing.Point(3, 58);
            this.pictureScroll.Name = "pictureScroll";
            this.pictureScroll.Size = new System.Drawing.Size(259, 220);
            this.pictureScroll.TabIndex = 16;
            // 
            // Export8Bits
            // 
            this.Export8Bits.AutoSize = true;
            this.Export8Bits.Checked = true;
            this.Export8Bits.CheckState = System.Windows.Forms.CheckState.Checked;
            this.Export8Bits.Location = new System.Drawing.Point(3, 6);
            this.Export8Bits.Name = "Export8Bits";
            this.Export8Bits.Size = new System.Drawing.Size(51, 17);
            this.Export8Bits.TabIndex = 12;
            this.Export8Bits.Text = "8 bits";
            this.Export8Bits.UseVisualStyleBackColor = true;
            // 
            // CompressionMethod
            // 
            this.CompressionMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.CompressionMethod.FormattingEnabled = true;
            this.CompressionMethod.Location = new System.Drawing.Point(113, 31);
            this.CompressionMethod.Name = "CompressionMethod";
            this.CompressionMethod.Size = new System.Drawing.Size(146, 21);
            this.CompressionMethod.TabIndex = 17;
            this.CompressionMethod.Visible = false;
            // 
            // CompressionMethodLabel
            // 
            this.CompressionMethodLabel.AutoSize = true;
            this.CompressionMethodLabel.Location = new System.Drawing.Point(3, 34);
            this.CompressionMethodLabel.Name = "CompressionMethodLabel";
            this.CompressionMethodLabel.Size = new System.Drawing.Size(109, 13);
            this.CompressionMethodLabel.TabIndex = 18;
            this.CompressionMethodLabel.Text = "Compression Method:";
            this.CompressionMethodLabel.Visible = false;
            // 
            // RoomBlockImageControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.CompressionMethodLabel);
            this.Controls.Add(this.CompressionMethod);
            this.Controls.Add(this.Export8Bits);
            this.Controls.Add(this.pictureScroll);
            this.Controls.Add(this.TesteReencode);
            this.Controls.Add(this.ImportFromDisk);
            this.Controls.Add(this.SaveToDisk);
            this.Name = "RoomBlockImageControl";
            this.Size = new System.Drawing.Size(265, 281);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBackground)).EndInit();
            this.pictureScroll.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBackground;
        private System.Windows.Forms.Button TesteReencode;
        private System.Windows.Forms.Button ImportFromDisk;
        private System.Windows.Forms.Button SaveToDisk;
        private System.Windows.Forms.Panel pictureScroll;
        private System.Windows.Forms.CheckBox Export8Bits;
        private System.Windows.Forms.ComboBox CompressionMethod;
        private System.Windows.Forms.Label CompressionMethodLabel;
    }
}
