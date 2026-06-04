namespace ScummEditor.Gui
{
    partial class SoundBlockControl
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopPlayback();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.grid = new System.Windows.Forms.DataGridView();
            this.toolbar = new System.Windows.Forms.Panel();
            this.btnPlay = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.btnExport = new System.Windows.Forms.Button();
            this.btnExportAll = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.Contents.SuspendLayout();
            this.toolbar.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            this.SuspendLayout();
            //
            // Contents
            //
            this.Contents.Controls.Add(this.grid);
            this.Contents.Controls.Add(this.toolbar);
            //
            // grid
            //
            this.grid.AllowUserToAddRows = false;
            this.grid.AllowUserToDeleteRows = false;
            this.grid.AllowUserToResizeRows = false;
            this.grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grid.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.grid.MultiSelect = false;
            this.grid.Name = "grid";
            this.grid.ReadOnly = true;
            this.grid.RowHeadersVisible = false;
            this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.grid.TabIndex = 0;
            //
            // toolbar
            //
            this.toolbar.Controls.Add(this.lblStatus);
            this.toolbar.Controls.Add(this.btnExportAll);
            this.toolbar.Controls.Add(this.btnExport);
            this.toolbar.Controls.Add(this.btnStop);
            this.toolbar.Controls.Add(this.btnPlay);
            this.toolbar.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.toolbar.Height = 32;
            this.toolbar.Name = "toolbar";
            this.toolbar.TabIndex = 1;
            //
            // btnPlay
            //
            this.btnPlay.Location = new System.Drawing.Point(3, 5);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(60, 23);
            this.btnPlay.TabIndex = 0;
            this.btnPlay.Text = "Play";
            this.btnPlay.UseVisualStyleBackColor = true;
            //
            // btnStop
            //
            this.btnStop.Location = new System.Drawing.Point(69, 5);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(60, 23);
            this.btnStop.TabIndex = 1;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            //
            // btnExport
            //
            this.btnExport.Location = new System.Drawing.Point(145, 5);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(100, 23);
            this.btnExport.TabIndex = 2;
            this.btnExport.Text = "Export selected";
            this.btnExport.UseVisualStyleBackColor = true;
            //
            // btnExportAll
            //
            this.btnExportAll.Location = new System.Drawing.Point(251, 5);
            this.btnExportAll.Name = "btnExportAll";
            this.btnExportAll.Size = new System.Drawing.Size(80, 23);
            this.btnExportAll.TabIndex = 3;
            this.btnExportAll.Text = "Export all";
            this.btnExportAll.UseVisualStyleBackColor = true;
            //
            // lblStatus
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(340, 10);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "";
            //
            // SoundBlockControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "SoundBlockControl";
            this.Contents.ResumeLayout(false);
            this.toolbar.ResumeLayout(false);
            this.toolbar.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.Panel toolbar;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnExportAll;
        private System.Windows.Forms.Label lblStatus;
    }
}
