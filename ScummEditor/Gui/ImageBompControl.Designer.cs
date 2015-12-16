namespace ScummEditor.Gui
{
    partial class ImageBompControl
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
            this.label4 = new System.Windows.Forms.Label();
            this.Unknown = new System.Windows.Forms.TextBox();
            this.Padding1 = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.ImageWidth = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.ImageHeight = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.DataSize = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.helpProvider1 = new System.Windows.Forms.HelpProvider();
            this.ImageData = new Be.Windows.Forms.HexBox();
            this.Padding2 = new System.Windows.Forms.TextBox();
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.ImageData);
            this.Contents.Controls.Add(this.label9);
            this.Contents.Controls.Add(this.DataSize);
            this.Contents.Controls.Add(this.label8);
            this.Contents.Controls.Add(this.ImageHeight);
            this.Contents.Controls.Add(this.label7);
            this.Contents.Controls.Add(this.ImageWidth);
            this.Contents.Controls.Add(this.label6);
            this.Contents.Controls.Add(this.Padding2);
            this.Contents.Controls.Add(this.Padding1);
            this.Contents.Controls.Add(this.label5);
            this.Contents.Controls.Add(this.Unknown);
            this.Contents.Controls.Add(this.label4);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(0, 6);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(56, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "Unknown:";
            // 
            // Unknown
            // 
            this.Unknown.Location = new System.Drawing.Point(56, 3);
            this.Unknown.Name = "Unknown";
            this.Unknown.ReadOnly = true;
            this.Unknown.Size = new System.Drawing.Size(100, 20);
            this.Unknown.TabIndex = 1;
            // 
            // Padding1
            // 
            this.Padding1.Location = new System.Drawing.Point(214, 3);
            this.Padding1.Name = "Padding1";
            this.Padding1.ReadOnly = true;
            this.Padding1.Size = new System.Drawing.Size(51, 20);
            this.Padding1.TabIndex = 3;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(159, 6);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(49, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "Padding:";
            // 
            // ImageWidth
            // 
            this.ImageWidth.Location = new System.Drawing.Point(56, 40);
            this.ImageWidth.Name = "ImageWidth";
            this.ImageWidth.ReadOnly = true;
            this.ImageWidth.Size = new System.Drawing.Size(100, 20);
            this.ImageWidth.TabIndex = 5;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(0, 43);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(35, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "Width";
            // 
            // ImageHeight
            // 
            this.ImageHeight.Location = new System.Drawing.Point(214, 40);
            this.ImageHeight.Name = "ImageHeight";
            this.ImageHeight.ReadOnly = true;
            this.ImageHeight.Size = new System.Drawing.Size(107, 20);
            this.ImageHeight.TabIndex = 7;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(159, 43);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(41, 13);
            this.label7.TabIndex = 6;
            this.label7.Text = "Height:";
            // 
            // DataSize
            // 
            this.DataSize.Location = new System.Drawing.Point(68, 79);
            this.DataSize.Name = "DataSize";
            this.DataSize.ReadOnly = true;
            this.DataSize.Size = new System.Drawing.Size(85, 20);
            this.DataSize.TabIndex = 9;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(159, 82);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(65, 13);
            this.label8.TabIndex = 8;
            this.label8.Text = "Image Data:";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(0, 82);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(62, 13);
            this.label9.TabIndex = 10;
            this.label9.Text = "Image Size:";
            // 
            // ImageData
            // 
            this.ImageData.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ImageData.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ImageData.HexCasing = Be.Windows.Forms.HexCasing.Lower;
            this.ImageData.LineInfoForeColor = System.Drawing.Color.Empty;
            this.ImageData.Location = new System.Drawing.Point(0, 98);
            this.ImageData.Name = "ImageData";
            this.ImageData.ReadOnly = true;
            this.ImageData.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.ImageData.Size = new System.Drawing.Size(481, 189);
            this.ImageData.StringViewVisible = true;
            this.ImageData.TabIndex = 11;
            this.ImageData.UseFixedBytesPerLine = true;
            this.ImageData.VScrollBarVisible = true;
            // 
            // Padding2
            // 
            this.Padding2.Location = new System.Drawing.Point(271, 3);
            this.Padding2.Name = "Padding2";
            this.Padding2.ReadOnly = true;
            this.Padding2.Size = new System.Drawing.Size(50, 20);
            this.Padding2.TabIndex = 3;
            // 
            // ImageBompControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ImageBompControl";
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox DataSize;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox ImageHeight;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox ImageWidth;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox Padding1;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox Unknown;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.HelpProvider helpProvider1;
        private Be.Windows.Forms.HexBox ImageData;
        private System.Windows.Forms.TextBox Padding2;
    }
}
