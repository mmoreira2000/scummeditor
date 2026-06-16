namespace ScummEditor.Gui
{
    partial class CdAudioSouControl
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
            this.info = new System.Windows.Forms.Label();
            this.buttons = new System.Windows.Forms.Panel();
            this.playButton = new System.Windows.Forms.Button();
            this.exportButton = new System.Windows.Forms.Button();
            this.exportAllButton = new System.Windows.Forms.Button();
            this.grid = new System.Windows.Forms.DataGridView();
            this.buttons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            this.SuspendLayout();
            //
            // info
            //
            this.info.Dock = System.Windows.Forms.DockStyle.Top;
            this.info.AutoSize = false;
            this.info.Height = 22;
            this.info.Padding = new System.Windows.Forms.Padding(3, 4, 3, 0);
            this.info.Name = "info";
            this.info.TabIndex = 0;
            //
            // buttons
            //
            this.buttons.Controls.Add(this.playButton);
            this.buttons.Controls.Add(this.exportButton);
            this.buttons.Controls.Add(this.exportAllButton);
            this.buttons.Dock = System.Windows.Forms.DockStyle.Top;
            this.buttons.Height = 31;
            this.buttons.Name = "buttons";
            this.buttons.TabIndex = 1;
            //
            // playButton
            //
            this.playButton.Location = new System.Drawing.Point(3, 3);
            this.playButton.Name = "playButton";
            this.playButton.Size = new System.Drawing.Size(75, 25);
            this.playButton.TabIndex = 0;
            this.playButton.Text = "Play";
            this.playButton.UseVisualStyleBackColor = true;
            this.playButton.Click += new System.EventHandler(this.playButton_Click);
            //
            // exportButton
            //
            this.exportButton.Location = new System.Drawing.Point(84, 3);
            this.exportButton.Name = "exportButton";
            this.exportButton.Size = new System.Drawing.Size(110, 25);
            this.exportButton.TabIndex = 1;
            this.exportButton.Text = "Export WAV...";
            this.exportButton.UseVisualStyleBackColor = true;
            this.exportButton.Click += new System.EventHandler(this.exportButton_Click);
            //
            // exportAllButton
            //
            this.exportAllButton.Location = new System.Drawing.Point(200, 3);
            this.exportAllButton.Name = "exportAllButton";
            this.exportAllButton.Size = new System.Drawing.Size(130, 25);
            this.exportAllButton.TabIndex = 2;
            this.exportAllButton.Text = "Export all tracks...";
            this.exportAllButton.UseVisualStyleBackColor = true;
            this.exportAllButton.Click += new System.EventHandler(this.exportAllButton_Click);
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
            this.grid.MultiSelect = false;
            this.grid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.grid.TabIndex = 2;
            //
            // CdAudioSouControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.grid);
            this.Controls.Add(this.buttons);
            this.Controls.Add(this.info);
            this.Name = "CdAudioSouControl";
            this.Size = new System.Drawing.Size(640, 420);
            this.buttons.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label info;
        private System.Windows.Forms.Panel buttons;
        private System.Windows.Forms.Button playButton;
        private System.Windows.Forms.Button exportButton;
        private System.Windows.Forms.Button exportAllButton;
        private System.Windows.Forms.DataGridView grid;
    }
}
