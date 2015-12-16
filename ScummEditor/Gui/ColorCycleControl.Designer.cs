namespace ScummEditor.Gui
{
    partial class ColorCycleControl
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
            this.CloseBytesCount = new System.Windows.Forms.Label();
            this.ColorCycles = new System.Windows.Forms.ListView();
            this.Unknown = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Frequency = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Flags = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Start = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.End = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.ColorCycles);
            this.Contents.Controls.Add(this.CloseBytesCount);
            // 
            // CloseBytesCount
            // 
            this.CloseBytesCount.AutoSize = true;
            this.CloseBytesCount.Location = new System.Drawing.Point(3, 9);
            this.CloseBytesCount.Name = "CloseBytesCount";
            this.CloseBytesCount.Size = new System.Drawing.Size(127, 13);
            this.CloseBytesCount.TabIndex = 1;
            this.CloseBytesCount.Text = "Number of close bytes: -1";
            // 
            // ColorCycles
            // 
            this.ColorCycles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.ColorCycles.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Unknown,
            this.Frequency,
            this.Flags,
            this.Start,
            this.End});
            this.ColorCycles.Location = new System.Drawing.Point(6, 34);
            this.ColorCycles.Name = "ColorCycles";
            this.ColorCycles.Size = new System.Drawing.Size(472, 250);
            this.ColorCycles.TabIndex = 2;
            this.ColorCycles.UseCompatibleStateImageBehavior = false;
            this.ColorCycles.View = System.Windows.Forms.View.Details;
            // 
            // Unknown
            // 
            this.Unknown.Text = "Unknow";
            this.Unknown.Width = 80;
            // 
            // Frequency
            // 
            this.Frequency.Text = "Frequency";
            this.Frequency.Width = 100;
            // 
            // Flags
            // 
            this.Flags.Text = "Flags";
            this.Flags.Width = 80;
            // 
            // Start
            // 
            this.Start.Text = "Start";
            this.Start.Width = 80;
            // 
            // End
            // 
            this.End.Text = "End";
            this.End.Width = 80;
            // 
            // ColorCycleControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ColorCycleControl";
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label CloseBytesCount;
        private System.Windows.Forms.ListView ColorCycles;
        private System.Windows.Forms.ColumnHeader Unknown;
        private System.Windows.Forms.ColumnHeader Frequency;
        private System.Windows.Forms.ColumnHeader Flags;
        private System.Windows.Forms.ColumnHeader Start;
        private System.Windows.Forms.ColumnHeader End;
    }
}
