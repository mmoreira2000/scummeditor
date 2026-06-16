namespace ScummEditor.Gui
{
    partial class RoomHeaderControl
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
            this.Width = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.Height = new System.Windows.Forms.TextBox();
            this.NumberObjects = new System.Windows.Forms.TextBox();
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.NumberObjects);
            this.Contents.Controls.Add(this.Height);
            this.Contents.Controls.Add(this.label6);
            this.Contents.Controls.Add(this.label5);
            this.Contents.Controls.Add(this.label4);
            this.Contents.Controls.Add(this.Width);
            // 
            // Width
            // 
            this.Width.Location = new System.Drawing.Point(47, 16);
            this.Width.Name = "Width";
            this.Width.ReadOnly = true;
            this.Width.Size = new System.Drawing.Size(100, 20);
            this.Width.TabIndex = 0;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 19);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(38, 13);
            this.label4.TabIndex = 1;
            this.label4.Text = "Width:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 54);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(41, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "Height:";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 93);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(98, 13);
            this.label6.TabIndex = 3;
            this.label6.Text = "Number of Objects:";
            // 
            // Height
            // 
            this.Height.Location = new System.Drawing.Point(47, 51);
            this.Height.Name = "Height";
            this.Height.ReadOnly = true;
            this.Height.Size = new System.Drawing.Size(100, 20);
            this.Height.TabIndex = 4;
            // 
            // NumberObjects
            // 
            this.NumberObjects.Location = new System.Drawing.Point(107, 90);
            this.NumberObjects.Name = "NumberObjects";
            this.NumberObjects.ReadOnly = true;
            this.NumberObjects.Size = new System.Drawing.Size(100, 20);
            this.NumberObjects.TabIndex = 5;
            // 
            // RoomHeaderControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "RoomHeaderControl";
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox Width;
        private System.Windows.Forms.TextBox NumberObjects;
        private System.Windows.Forms.TextBox Height;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
    }
}
