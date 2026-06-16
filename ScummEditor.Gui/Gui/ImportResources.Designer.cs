namespace ScummEditor.Gui
{
    partial class ImportResources
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.FilesFound = new System.Windows.Forms.Label();
            this.FilesExportedLabel = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.ImportLocation = new System.Windows.Forms.TextBox();
            this.SelectFolder = new System.Windows.Forms.Button();
            this.Progress = new System.Windows.Forms.ProgressBar();
            this.Cancel = new System.Windows.Forms.Button();
            this.OK = new System.Windows.Forms.Button();
            this.FilesImported = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // FilesFound
            // 
            this.FilesFound.AutoSize = true;
            this.FilesFound.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FilesFound.Location = new System.Drawing.Point(134, 48);
            this.FilesFound.Name = "FilesFound";
            this.FilesFound.Size = new System.Drawing.Size(16, 17);
            this.FilesFound.TabIndex = 21;
            this.FilesFound.Text = "0";
            // 
            // FilesExportedLabel
            // 
            this.FilesExportedLabel.AutoSize = true;
            this.FilesExportedLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FilesExportedLabel.Location = new System.Drawing.Point(12, 48);
            this.FilesExportedLabel.Name = "FilesExportedLabel";
            this.FilesExportedLabel.Size = new System.Drawing.Size(85, 17);
            this.FilesExportedLabel.TabIndex = 20;
            this.FilesExportedLabel.Text = "Files Found:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 5);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(88, 13);
            this.label1.TabIndex = 19;
            this.label1.Text = "Images Location:";
            // 
            // ImportLocation
            // 
            this.ImportLocation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ImportLocation.Location = new System.Drawing.Point(12, 21);
            this.ImportLocation.Name = "ImportLocation";
            this.ImportLocation.ReadOnly = true;
            this.ImportLocation.Size = new System.Drawing.Size(320, 20);
            this.ImportLocation.TabIndex = 18;
            // 
            // SelectFolder
            // 
            this.SelectFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SelectFolder.Location = new System.Drawing.Point(338, 18);
            this.SelectFolder.Name = "SelectFolder";
            this.SelectFolder.Size = new System.Drawing.Size(32, 23);
            this.SelectFolder.TabIndex = 17;
            this.SelectFolder.Text = "...";
            this.SelectFolder.UseVisualStyleBackColor = true;
            this.SelectFolder.Click += new System.EventHandler(this.SelectFolder_Click);
            // 
            // Progress
            // 
            this.Progress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Progress.Location = new System.Drawing.Point(12, 103);
            this.Progress.Name = "Progress";
            this.Progress.Size = new System.Drawing.Size(193, 23);
            this.Progress.TabIndex = 16;
            // 
            // Cancel
            // 
            this.Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Cancel.Location = new System.Drawing.Point(295, 103);
            this.Cancel.Name = "Cancel";
            this.Cancel.Size = new System.Drawing.Size(75, 23);
            this.Cancel.TabIndex = 15;
            this.Cancel.Text = "&Cancel";
            this.Cancel.UseVisualStyleBackColor = true;
            this.Cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // OK
            // 
            this.OK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.OK.Location = new System.Drawing.Point(211, 103);
            this.OK.Name = "OK";
            this.OK.Size = new System.Drawing.Size(75, 23);
            this.OK.TabIndex = 14;
            this.OK.Text = "&OK";
            this.OK.UseVisualStyleBackColor = true;
            this.OK.Click += new System.EventHandler(this.OK_Click);
            // 
            // FilesImported
            // 
            this.FilesImported.AutoSize = true;
            this.FilesImported.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FilesImported.Location = new System.Drawing.Point(134, 74);
            this.FilesImported.Name = "FilesImported";
            this.FilesImported.Size = new System.Drawing.Size(16, 17);
            this.FilesImported.TabIndex = 23;
            this.FilesImported.Text = "0";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(12, 74);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(100, 17);
            this.label3.TabIndex = 22;
            this.label3.Text = "Files Imported:";
            // 
            // ImportResources
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(382, 138);
            this.Controls.Add(this.FilesImported);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.FilesFound);
            this.Controls.Add(this.FilesExportedLabel);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ImportLocation);
            this.Controls.Add(this.SelectFolder);
            this.Controls.Add(this.Progress);
            this.Controls.Add(this.Cancel);
            this.Controls.Add(this.OK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "ImportResources";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Import Resources";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label FilesFound;
        private System.Windows.Forms.Label FilesExportedLabel;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox ImportLocation;
        private System.Windows.Forms.Button SelectFolder;
        private System.Windows.Forms.ProgressBar Progress;
        private System.Windows.Forms.Button Cancel;
        private System.Windows.Forms.Button OK;
        private System.Windows.Forms.Label FilesImported;
        private System.Windows.Forms.Label label3;
    }
}