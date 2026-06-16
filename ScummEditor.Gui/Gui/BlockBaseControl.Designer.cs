namespace ScummEditor.Gui
{
    partial class BlockBaseControl
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
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.blockSize = new System.Windows.Forms.TextBox();
            this.blockType = new System.Windows.Forms.TextBox();
            this.blockOffset = new System.Windows.Forms.TextBox();
            this.Contents = new System.Windows.Forms.Panel();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(3, 10);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(61, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "BlockType:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(228, 10);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(57, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "BlockSize:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(3, 36);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(68, 13);
            this.label3.TabIndex = 2;
            this.label3.Text = "Block Offset:";
            // 
            // blockSize
            // 
            this.blockSize.Location = new System.Drawing.Point(291, 7);
            this.blockSize.Name = "blockSize";
            this.blockSize.ReadOnly = true;
            this.blockSize.Size = new System.Drawing.Size(135, 20);
            this.blockSize.TabIndex = 3;
            // 
            // blockType
            // 
            this.blockType.Location = new System.Drawing.Point(87, 7);
            this.blockType.Name = "blockType";
            this.blockType.ReadOnly = true;
            this.blockType.Size = new System.Drawing.Size(135, 20);
            this.blockType.TabIndex = 4;
            // 
            // blockOffset
            // 
            this.blockOffset.Location = new System.Drawing.Point(87, 33);
            this.blockOffset.Name = "blockOffset";
            this.blockOffset.ReadOnly = true;
            this.blockOffset.Size = new System.Drawing.Size(135, 20);
            this.blockOffset.TabIndex = 5;
            // 
            // Contents
            // 
            this.Contents.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Contents.Location = new System.Drawing.Point(6, 59);
            this.Contents.Name = "Contents";
            this.Contents.Size = new System.Drawing.Size(481, 287);
            this.Contents.TabIndex = 6;
            // 
            // BlockBaseControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.Contents);
            this.Controls.Add(this.blockOffset);
            this.Controls.Add(this.blockType);
            this.Controls.Add(this.blockSize);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Name = "BlockBaseControl";
            this.Size = new System.Drawing.Size(490, 349);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox blockSize;
        private System.Windows.Forms.TextBox blockType;
        private System.Windows.Forms.TextBox blockOffset;
        protected internal System.Windows.Forms.Panel Contents;
    }
}
