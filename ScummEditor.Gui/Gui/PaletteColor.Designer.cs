namespace ScummEditor.Gui
{
    partial class PaletteColor
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
            this.indexText = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // indexText
            // 
            this.indexText.AutoSize = true;
            this.indexText.BackColor = System.Drawing.Color.White;
            this.indexText.Font = new System.Drawing.Font("Tahoma", 6F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.indexText.Location = new System.Drawing.Point(-1, -1);
            this.indexText.Name = "indexText";
            this.indexText.Size = new System.Drawing.Size(17, 10);
            this.indexText.TabIndex = 1;
            this.indexText.Text = "255";
            this.indexText.Click += new System.EventHandler(this.TextClick);
            // 
            // PaletteColor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.indexText);
            this.Name = "PaletteColor";
            this.Size = new System.Drawing.Size(24, 24);
            this.Click += new System.EventHandler(this.ColorClick);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.Label indexText;



    }
}
