namespace ScummEditor.Gui
{
    partial class CharsetControl
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
            this.scroll = new System.Windows.Forms.Panel();
            this.atlas = new System.Windows.Forms.PictureBox();
            this.buttons = new System.Windows.Forms.Panel();
            this.exportPngButton = new System.Windows.Forms.Button();
            this.importPngButton = new System.Windows.Forms.Button();
            this.header = new System.Windows.Forms.Label();
            this.Contents.SuspendLayout();
            this.scroll.SuspendLayout();
            this.buttons.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.atlas)).BeginInit();
            this.SuspendLayout();
            //
            // Contents
            //
            this.Contents.Controls.Add(this.scroll);
            this.Contents.Controls.Add(this.buttons);
            this.Contents.Controls.Add(this.header);
            //
            // header
            //
            this.header.Dock = System.Windows.Forms.DockStyle.Top;
            this.header.AutoSize = false;
            this.header.Height = 22;
            this.header.Name = "header";
            this.header.TabIndex = 0;
            //
            // buttons
            //
            this.buttons.Controls.Add(this.exportPngButton);
            this.buttons.Controls.Add(this.importPngButton);
            this.buttons.Dock = System.Windows.Forms.DockStyle.Top;
            this.buttons.Height = 31;
            this.buttons.Name = "buttons";
            this.buttons.TabIndex = 1;
            //
            // exportPngButton
            //
            this.exportPngButton.Location = new System.Drawing.Point(3, 3);
            this.exportPngButton.Name = "exportPngButton";
            this.exportPngButton.Size = new System.Drawing.Size(120, 25);
            this.exportPngButton.TabIndex = 0;
            this.exportPngButton.Text = "Export PNG...";
            this.exportPngButton.UseVisualStyleBackColor = true;
            this.exportPngButton.Click += new System.EventHandler(this.exportPngButton_Click);
            //
            // importPngButton
            //
            this.importPngButton.Location = new System.Drawing.Point(129, 3);
            this.importPngButton.Name = "importPngButton";
            this.importPngButton.Size = new System.Drawing.Size(120, 25);
            this.importPngButton.TabIndex = 1;
            this.importPngButton.Text = "Import PNG...";
            this.importPngButton.UseVisualStyleBackColor = true;
            this.importPngButton.Click += new System.EventHandler(this.importPngButton_Click);
            //
            // scroll
            //
            this.scroll.AutoScroll = true;
            this.scroll.BackColor = System.Drawing.Color.White;
            this.scroll.Controls.Add(this.atlas);
            this.scroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.scroll.Name = "scroll";
            this.scroll.TabIndex = 2;
            //
            // atlas
            //
            this.atlas.Location = new System.Drawing.Point(0, 0);
            this.atlas.Name = "atlas";
            this.atlas.SizeMode = System.Windows.Forms.PictureBoxSizeMode.AutoSize;
            this.atlas.TabIndex = 0;
            this.atlas.TabStop = false;
            //
            // CharsetControl
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "CharsetControl";
            this.Contents.ResumeLayout(false);
            this.scroll.ResumeLayout(false);
            this.scroll.PerformLayout();
            this.buttons.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.atlas)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel scroll;
        private System.Windows.Forms.PictureBox atlas;
        private System.Windows.Forms.Panel buttons;
        private System.Windows.Forms.Button exportPngButton;
        private System.Windows.Forms.Button importPngButton;
        private System.Windows.Forms.Label header;
    }
}
