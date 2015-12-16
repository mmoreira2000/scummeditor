namespace ScummEditor.Gui
{
    partial class ExportResources
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
            this.OK = new System.Windows.Forms.Button();
            this.Cancel = new System.Windows.Forms.Button();
            this.ExportWithTransparency = new System.Windows.Forms.CheckBox();
            this.ExportBackgrounds = new System.Windows.Forms.CheckBox();
            this.ExportObjects = new System.Windows.Forms.CheckBox();
            this.Progress = new System.Windows.Forms.ProgressBar();
            this.SelectFolder = new System.Windows.Forms.Button();
            this.ExportLocation = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.ExportCostumes = new System.Windows.Forms.CheckBox();
            this.ExportBackgroundZPlanes = new System.Windows.Forms.CheckBox();
            this.ExportObjectsZPlanes = new System.Windows.Forms.CheckBox();
            this.FilesExportedLabel = new System.Windows.Forms.Label();
            this.FilesExported = new System.Windows.Forms.Label();
            this.Export8Bits = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // OK
            // 
            this.OK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.OK.Location = new System.Drawing.Point(256, 191);
            this.OK.Name = "OK";
            this.OK.Size = new System.Drawing.Size(75, 23);
            this.OK.TabIndex = 0;
            this.OK.Text = "&OK";
            this.OK.UseVisualStyleBackColor = true;
            this.OK.Click += new System.EventHandler(this.OK_Click);
            // 
            // Cancel
            // 
            this.Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Cancel.Location = new System.Drawing.Point(340, 191);
            this.Cancel.Name = "Cancel";
            this.Cancel.Size = new System.Drawing.Size(75, 23);
            this.Cancel.TabIndex = 1;
            this.Cancel.Text = "&Cancel";
            this.Cancel.UseVisualStyleBackColor = true;
            this.Cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // ExportWithTransparency
            // 
            this.ExportWithTransparency.AutoSize = true;
            this.ExportWithTransparency.Location = new System.Drawing.Point(12, 56);
            this.ExportWithTransparency.Name = "ExportWithTransparency";
            this.ExportWithTransparency.Size = new System.Drawing.Size(394, 17);
            this.ExportWithTransparency.TabIndex = 2;
            this.ExportWithTransparency.Text = "Export with transparency (only recommended if you do not plan to import back)";
            this.ExportWithTransparency.UseVisualStyleBackColor = true;
            // 
            // ExportBackgrounds
            // 
            this.ExportBackgrounds.AutoSize = true;
            this.ExportBackgrounds.Checked = true;
            this.ExportBackgrounds.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ExportBackgrounds.Location = new System.Drawing.Point(13, 125);
            this.ExportBackgrounds.Name = "ExportBackgrounds";
            this.ExportBackgrounds.Size = new System.Drawing.Size(122, 17);
            this.ExportBackgrounds.TabIndex = 3;
            this.ExportBackgrounds.Text = "Export Backgrounds";
            this.ExportBackgrounds.UseVisualStyleBackColor = true;
            // 
            // ExportObjects
            // 
            this.ExportObjects.AutoSize = true;
            this.ExportObjects.Checked = true;
            this.ExportObjects.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ExportObjects.Location = new System.Drawing.Point(13, 148);
            this.ExportObjects.Name = "ExportObjects";
            this.ExportObjects.Size = new System.Drawing.Size(95, 17);
            this.ExportObjects.TabIndex = 4;
            this.ExportObjects.Text = "Export Objects";
            this.ExportObjects.UseVisualStyleBackColor = true;
            // 
            // Progress
            // 
            this.Progress.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Progress.Location = new System.Drawing.Point(12, 191);
            this.Progress.Name = "Progress";
            this.Progress.Size = new System.Drawing.Size(238, 23);
            this.Progress.TabIndex = 5;
            // 
            // SelectFolder
            // 
            this.SelectFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.SelectFolder.Location = new System.Drawing.Point(383, 27);
            this.SelectFolder.Name = "SelectFolder";
            this.SelectFolder.Size = new System.Drawing.Size(32, 23);
            this.SelectFolder.TabIndex = 6;
            this.SelectFolder.Text = "...";
            this.SelectFolder.UseVisualStyleBackColor = true;
            this.SelectFolder.Click += new System.EventHandler(this.SelectFolder_Click);
            // 
            // ExportLocation
            // 
            this.ExportLocation.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ExportLocation.Location = new System.Drawing.Point(12, 29);
            this.ExportLocation.Name = "ExportLocation";
            this.ExportLocation.ReadOnly = true;
            this.ExportLocation.Size = new System.Drawing.Size(365, 20);
            this.ExportLocation.TabIndex = 7;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(63, 13);
            this.label1.TabIndex = 8;
            this.label1.Text = "Destination:";
            // 
            // ExportCostumes
            // 
            this.ExportCostumes.AutoSize = true;
            this.ExportCostumes.Checked = true;
            this.ExportCostumes.CheckState = System.Windows.Forms.CheckState.Checked;
            this.ExportCostumes.Location = new System.Drawing.Point(13, 171);
            this.ExportCostumes.Name = "ExportCostumes";
            this.ExportCostumes.Size = new System.Drawing.Size(105, 17);
            this.ExportCostumes.TabIndex = 9;
            this.ExportCostumes.Text = "Export Costumes";
            this.ExportCostumes.UseVisualStyleBackColor = true;
            // 
            // ExportBackgroundZPlanes
            // 
            this.ExportBackgroundZPlanes.AutoSize = true;
            this.ExportBackgroundZPlanes.Location = new System.Drawing.Point(218, 125);
            this.ExportBackgroundZPlanes.Name = "ExportBackgroundZPlanes";
            this.ExportBackgroundZPlanes.Size = new System.Drawing.Size(162, 17);
            this.ExportBackgroundZPlanes.TabIndex = 10;
            this.ExportBackgroundZPlanes.Text = "Export Background Z-Planes";
            this.ExportBackgroundZPlanes.UseVisualStyleBackColor = true;
            // 
            // ExportObjectsZPlanes
            // 
            this.ExportObjectsZPlanes.AutoSize = true;
            this.ExportObjectsZPlanes.Location = new System.Drawing.Point(218, 148);
            this.ExportObjectsZPlanes.Name = "ExportObjectsZPlanes";
            this.ExportObjectsZPlanes.Size = new System.Drawing.Size(140, 17);
            this.ExportObjectsZPlanes.TabIndex = 11;
            this.ExportObjectsZPlanes.Text = "Export Objects Z-Planes";
            this.ExportObjectsZPlanes.UseVisualStyleBackColor = true;
            // 
            // FilesExportedLabel
            // 
            this.FilesExportedLabel.AutoSize = true;
            this.FilesExportedLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FilesExportedLabel.Location = new System.Drawing.Point(215, 171);
            this.FilesExportedLabel.Name = "FilesExportedLabel";
            this.FilesExportedLabel.Size = new System.Drawing.Size(116, 17);
            this.FilesExportedLabel.TabIndex = 12;
            this.FilesExportedLabel.Text = "Files Exported:";
            this.FilesExportedLabel.Visible = false;
            // 
            // FilesExported
            // 
            this.FilesExported.AutoSize = true;
            this.FilesExported.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FilesExported.Location = new System.Drawing.Point(337, 171);
            this.FilesExported.Name = "FilesExported";
            this.FilesExported.Size = new System.Drawing.Size(17, 17);
            this.FilesExported.TabIndex = 13;
            this.FilesExported.Text = "0";
            this.FilesExported.Visible = false;
            // 
            // Export8Bits
            // 
            this.Export8Bits.AutoSize = true;
            this.Export8Bits.Checked = true;
            this.Export8Bits.CheckState = System.Windows.Forms.CheckState.Checked;
            this.Export8Bits.Location = new System.Drawing.Point(12, 79);
            this.Export8Bits.Name = "Export8Bits";
            this.Export8Bits.Size = new System.Drawing.Size(204, 17);
            this.Export8Bits.TabIndex = 14;
            this.Export8Bits.Text = "Export as 8 bits indexed palette image";
            this.Export8Bits.UseVisualStyleBackColor = true;
            // 
            // ExportResources
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(427, 226);
            this.Controls.Add(this.Export8Bits);
            this.Controls.Add(this.FilesExported);
            this.Controls.Add(this.FilesExportedLabel);
            this.Controls.Add(this.ExportObjectsZPlanes);
            this.Controls.Add(this.ExportBackgroundZPlanes);
            this.Controls.Add(this.ExportCostumes);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.ExportLocation);
            this.Controls.Add(this.SelectFolder);
            this.Controls.Add(this.Progress);
            this.Controls.Add(this.ExportObjects);
            this.Controls.Add(this.ExportBackgrounds);
            this.Controls.Add(this.ExportWithTransparency);
            this.Controls.Add(this.Cancel);
            this.Controls.Add(this.OK);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Name = "ExportResources";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Export Resources";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button OK;
        private System.Windows.Forms.Button Cancel;
        private System.Windows.Forms.CheckBox ExportWithTransparency;
        private System.Windows.Forms.CheckBox ExportBackgrounds;
        private System.Windows.Forms.CheckBox ExportObjects;
        private System.Windows.Forms.ProgressBar Progress;
        private System.Windows.Forms.Button SelectFolder;
        private System.Windows.Forms.TextBox ExportLocation;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.CheckBox ExportCostumes;
        private System.Windows.Forms.CheckBox ExportBackgroundZPlanes;
        private System.Windows.Forms.CheckBox ExportObjectsZPlanes;
        private System.Windows.Forms.Label FilesExportedLabel;
        private System.Windows.Forms.Label FilesExported;
        private System.Windows.Forms.CheckBox Export8Bits;
    }
}