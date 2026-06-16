namespace ScummEditor.Gui
{
    partial class NotImplementedDataBlockControl
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
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.label3 = new System.Windows.Forms.Label();
            this.LoadData = new System.Windows.Forms.Button();
            this.AutomaticLoadData = new System.Windows.Forms.CheckBox();
            this.hexBox1 = new Be.Windows.Forms.HexBox();
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.hexBox1);
            this.Contents.Controls.Add(this.AutomaticLoadData);
            this.Contents.Controls.Add(this.LoadData);
            this.Contents.Location = new System.Drawing.Point(3, 59);
            this.Contents.Size = new System.Drawing.Size(611, 355);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.Red;
            this.label3.Location = new System.Drawing.Point(228, 36);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(297, 13);
            this.label3.TabIndex = 5;
            this.label3.Text = "PARSER FOR THIS TYPE NOT IMPLEMENTE YET";
            // 
            // LoadData
            // 
            this.LoadData.Location = new System.Drawing.Point(3, 6);
            this.LoadData.Name = "LoadData";
            this.LoadData.Size = new System.Drawing.Size(75, 23);
            this.LoadData.TabIndex = 6;
            this.LoadData.Text = "Load Data";
            this.LoadData.UseVisualStyleBackColor = true;
            this.LoadData.Click += new System.EventHandler(this.LoadData_Click);
            // 
            // AutomaticLoadData
            // 
            this.AutomaticLoadData.AutoSize = true;
            this.AutomaticLoadData.Location = new System.Drawing.Point(84, 10);
            this.AutomaticLoadData.Name = "AutomaticLoadData";
            this.AutomaticLoadData.Size = new System.Drawing.Size(136, 17);
            this.AutomaticLoadData.TabIndex = 7;
            this.AutomaticLoadData.Text = "Load data automaticaly";
            this.AutomaticLoadData.UseVisualStyleBackColor = true;
            this.AutomaticLoadData.CheckedChanged += new System.EventHandler(this.AutomaticLoadData_CheckedChanged);
            // 
            // hexBox1
            // 
            this.hexBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.hexBox1.Font = new System.Drawing.Font("Courier New", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.hexBox1.HexCasing = Be.Windows.Forms.HexCasing.Lower;
            this.hexBox1.LineInfoForeColor = System.Drawing.Color.Empty;
            this.hexBox1.Location = new System.Drawing.Point(3, 35);
            this.hexBox1.Name = "hexBox1";
            this.hexBox1.ReadOnly = true;
            this.hexBox1.ShadowSelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(60)))), ((int)(((byte)(188)))), ((int)(((byte)(255)))));
            this.hexBox1.Size = new System.Drawing.Size(605, 318);
            this.hexBox1.StringViewVisible = true;
            this.hexBox1.TabIndex = 8;
            this.hexBox1.UseFixedBytesPerLine = true;
            this.hexBox1.VScrollBarVisible = true;
            // 
            // NotImplementedDataBlockControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.label3);
            this.Name = "NotImplementedDataBlockControl";
            this.Size = new System.Drawing.Size(620, 419);
            this.Controls.SetChildIndex(this.Contents, 0);
            this.Controls.SetChildIndex(this.label3, 0);
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button LoadData;
        private System.Windows.Forms.CheckBox AutomaticLoadData;
        private Be.Windows.Forms.HexBox hexBox1;
    }
}
