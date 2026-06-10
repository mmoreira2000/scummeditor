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
            this.header = new System.Windows.Forms.Label();
            this.Contents.SuspendLayout();
            this.scroll.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.atlas)).BeginInit();
            this.SuspendLayout();
            //
            // Contents
            //
            this.Contents.Controls.Add(this.scroll);
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
            // scroll
            //
            this.scroll.AutoScroll = true;
            this.scroll.BackColor = System.Drawing.Color.White;
            this.scroll.Controls.Add(this.atlas);
            this.scroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.scroll.Name = "scroll";
            this.scroll.TabIndex = 1;
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
            ((System.ComponentModel.ISupportInitialize)(this.atlas)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel scroll;
        private System.Windows.Forms.PictureBox atlas;
        private System.Windows.Forms.Label header;
    }
}
