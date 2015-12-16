namespace ScummEditor.Gui
{
    partial class CostumeControl
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
            this.components = new System.ComponentModel.Container();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.label8 = new System.Windows.Forms.Label();
            this.CloseByte = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.Format = new System.Windows.Forms.TextBox();
            this.Size = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.Header = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.NumAnimations = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.label9 = new System.Windows.Forms.Label();
            this.AnimOffsets = new System.Windows.Forms.ListView();
            this.k = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label10 = new System.Windows.Forms.Label();
            this.Animations = new System.Windows.Forms.TreeView();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.Commands = new System.Windows.Forms.ListView();
            this.Command = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Action = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.AnimationCommandsOffset = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.LimbsOffsets = new System.Windows.Forms.ListView();
            this.LimbOffset = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.label12 = new System.Windows.Forms.Label();
            this.Limbs = new System.Windows.Forms.TreeView();
            this.label13 = new System.Windows.Forms.Label();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.Pictures = new System.Windows.Forms.ListView();
            this.PictureId = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Width = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Height = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.RelX = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.RelY = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.MoveX = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.MoveY = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.RedirLimb = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.RedirPict = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.ImageSize = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.PaletteTable = new System.Windows.Forms.FlowLayoutPanel();
            this.Offset = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.Contents.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.tabPage6.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.tabControl1);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Controls.Add(this.tabPage6);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(481, 287);
            this.tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.label8);
            this.tabPage1.Controls.Add(this.CloseByte);
            this.tabPage1.Controls.Add(this.label7);
            this.tabPage1.Controls.Add(this.Format);
            this.tabPage1.Controls.Add(this.Size);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.Header);
            this.tabPage1.Controls.Add(this.label4);
            this.tabPage1.Location = new System.Drawing.Point(4, 22);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(473, 261);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "General";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(6, 61);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(30, 13);
            this.label8.TabIndex = 8;
            this.label8.Text = "Size:";
            // 
            // CloseByte
            // 
            this.CloseByte.Location = new System.Drawing.Point(95, 132);
            this.CloseByte.Name = "CloseByte";
            this.CloseByte.ReadOnly = true;
            this.CloseByte.Size = new System.Drawing.Size(100, 20);
            this.CloseByte.TabIndex = 7;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(6, 25);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(45, 13);
            this.label7.TabIndex = 6;
            this.label7.Text = "Header:";
            // 
            // Format
            // 
            this.Format.Location = new System.Drawing.Point(95, 96);
            this.Format.Name = "Format";
            this.Format.ReadOnly = true;
            this.Format.Size = new System.Drawing.Size(100, 20);
            this.Format.TabIndex = 5;
            // 
            // Size
            // 
            this.Size.Location = new System.Drawing.Point(95, 59);
            this.Size.Name = "Size";
            this.Size.ReadOnly = true;
            this.Size.Size = new System.Drawing.Size(100, 20);
            this.Size.TabIndex = 3;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(6, 133);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(57, 13);
            this.label5.TabIndex = 2;
            this.label5.Text = "CloseByte:";
            // 
            // Header
            // 
            this.Header.Location = new System.Drawing.Point(95, 22);
            this.Header.Name = "Header";
            this.Header.ReadOnly = true;
            this.Header.Size = new System.Drawing.Size(100, 20);
            this.Header.TabIndex = 1;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 96);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(42, 13);
            this.label4.TabIndex = 0;
            this.label4.Text = "Format:";
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.NumAnimations);
            this.tabPage2.Controls.Add(this.label6);
            this.tabPage2.Controls.Add(this.splitContainer1);
            this.tabPage2.Location = new System.Drawing.Point(4, 22);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(473, 261);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Animations";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // NumAnimations
            // 
            this.NumAnimations.Location = new System.Drawing.Point(125, 7);
            this.NumAnimations.Name = "NumAnimations";
            this.NumAnimations.ReadOnly = true;
            this.NumAnimations.Size = new System.Drawing.Size(100, 20);
            this.NumAnimations.TabIndex = 2;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(6, 10);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(113, 13);
            this.label6.TabIndex = 1;
            this.label6.Text = "Number of Animations:";
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(0, 33);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.label9);
            this.splitContainer1.Panel1.Controls.Add(this.AnimOffsets);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.label10);
            this.splitContainer1.Panel2.Controls.Add(this.Animations);
            this.splitContainer1.Size = new System.Drawing.Size(473, 228);
            this.splitContainer1.SplitterDistance = 157;
            this.splitContainer1.TabIndex = 0;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(-1, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(144, 13);
            this.label9.TabIndex = 3;
            this.label9.Text = "Animations Definition Offsets:";
            // 
            // AnimOffsets
            // 
            this.AnimOffsets.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.AnimOffsets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.k});
            this.AnimOffsets.FullRowSelect = true;
            this.AnimOffsets.Location = new System.Drawing.Point(0, 16);
            this.AnimOffsets.Name = "AnimOffsets";
            this.AnimOffsets.Size = new System.Drawing.Size(155, 212);
            this.AnimOffsets.TabIndex = 0;
            this.AnimOffsets.UseCompatibleStateImageBehavior = false;
            this.AnimOffsets.View = System.Windows.Forms.View.Details;
            // 
            // k
            // 
            this.k.Text = "Offset";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(-3, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(108, 13);
            this.label10.TabIndex = 4;
            this.label10.Text = "Animation Definitions:";
            // 
            // Animations
            // 
            this.Animations.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Animations.FullRowSelect = true;
            this.Animations.Location = new System.Drawing.Point(0, 16);
            this.Animations.Name = "Animations";
            this.Animations.Size = new System.Drawing.Size(312, 213);
            this.Animations.TabIndex = 0;
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.Commands);
            this.tabPage3.Controls.Add(this.AnimationCommandsOffset);
            this.tabPage3.Controls.Add(this.label11);
            this.tabPage3.Location = new System.Drawing.Point(4, 22);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(473, 261);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Commands";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // Commands
            // 
            this.Commands.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Commands.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Command,
            this.Action});
            this.Commands.FullRowSelect = true;
            this.Commands.Location = new System.Drawing.Point(0, 32);
            this.Commands.Name = "Commands";
            this.Commands.Size = new System.Drawing.Size(473, 229);
            this.Commands.TabIndex = 2;
            this.Commands.UseCompatibleStateImageBehavior = false;
            this.Commands.View = System.Windows.Forms.View.Details;
            // 
            // Command
            // 
            this.Command.Text = "Command";
            // 
            // Action
            // 
            this.Action.Text = "Action";
            // 
            // AnimationCommandsOffset
            // 
            this.AnimationCommandsOffset.Location = new System.Drawing.Point(103, 6);
            this.AnimationCommandsOffset.Name = "AnimationCommandsOffset";
            this.AnimationCommandsOffset.ReadOnly = true;
            this.AnimationCommandsOffset.Size = new System.Drawing.Size(100, 20);
            this.AnimationCommandsOffset.TabIndex = 1;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(6, 9);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(91, 13);
            this.label11.TabIndex = 0;
            this.label11.Text = "Commands offset:";
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.splitContainer2);
            this.tabPage4.Location = new System.Drawing.Point(4, 22);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(473, 261);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Limbs";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(3, 3);
            this.splitContainer2.Name = "splitContainer2";
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.LimbsOffsets);
            this.splitContainer2.Panel1.Controls.Add(this.label12);
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.Limbs);
            this.splitContainer2.Panel2.Controls.Add(this.label13);
            this.splitContainer2.Size = new System.Drawing.Size(467, 255);
            this.splitContainer2.SplitterDistance = 155;
            this.splitContainer2.TabIndex = 0;
            // 
            // LimbsOffsets
            // 
            this.LimbsOffsets.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.LimbsOffsets.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.LimbOffset});
            this.LimbsOffsets.FullRowSelect = true;
            this.LimbsOffsets.Location = new System.Drawing.Point(0, 21);
            this.LimbsOffsets.Name = "LimbsOffsets";
            this.LimbsOffsets.Size = new System.Drawing.Size(155, 234);
            this.LimbsOffsets.TabIndex = 1;
            this.LimbsOffsets.UseCompatibleStateImageBehavior = false;
            this.LimbsOffsets.View = System.Windows.Forms.View.Details;
            // 
            // LimbOffset
            // 
            this.LimbOffset.Text = "Offset";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(0, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(70, 13);
            this.label12.TabIndex = 0;
            this.label12.Text = "Limbs Offsets";
            // 
            // Limbs
            // 
            this.Limbs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Limbs.FullRowSelect = true;
            this.Limbs.Location = new System.Drawing.Point(2, 21);
            this.Limbs.Name = "Limbs";
            this.Limbs.Size = new System.Drawing.Size(306, 234);
            this.Limbs.TabIndex = 1;
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(3, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(34, 13);
            this.label13.TabIndex = 0;
            this.label13.Text = "Limbs";
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.Pictures);
            this.tabPage5.Location = new System.Drawing.Point(4, 22);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage5.Size = new System.Drawing.Size(473, 261);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Pictures";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // Pictures
            // 
            this.Pictures.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.PictureId,
            this.Width,
            this.Height,
            this.RelX,
            this.RelY,
            this.MoveX,
            this.MoveY,
            this.RedirLimb,
            this.RedirPict,
            this.ImageSize});
            this.Pictures.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Pictures.FullRowSelect = true;
            this.Pictures.Location = new System.Drawing.Point(3, 3);
            this.Pictures.Name = "Pictures";
            this.Pictures.Size = new System.Drawing.Size(467, 255);
            this.Pictures.TabIndex = 3;
            this.Pictures.UseCompatibleStateImageBehavior = false;
            this.Pictures.View = System.Windows.Forms.View.Details;
            // 
            // PictureId
            // 
            this.PictureId.Text = "ID";
            // 
            // Width
            // 
            this.Width.Text = "Width";
            // 
            // Height
            // 
            this.Height.Text = "Height";
            // 
            // RelX
            // 
            this.RelX.Text = "Rel X";
            // 
            // RelY
            // 
            this.RelY.Text = "Rel Y";
            // 
            // MoveX
            // 
            this.MoveX.Text = "Move X";
            // 
            // MoveY
            // 
            this.MoveY.Text = "Move Y";
            // 
            // RedirLimb
            // 
            this.RedirLimb.Text = "Redir_Limb";
            // 
            // RedirPict
            // 
            this.RedirPict.Text = "Redir Pict";
            // 
            // ImageSize
            // 
            this.ImageSize.Text = "Image Size";
            // 
            // tabPage6
            // 
            this.tabPage6.Controls.Add(this.PaletteTable);
            this.tabPage6.Location = new System.Drawing.Point(4, 22);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage6.Size = new System.Drawing.Size(473, 261);
            this.tabPage6.TabIndex = 5;
            this.tabPage6.Text = "Palette";
            this.tabPage6.UseVisualStyleBackColor = true;
            // 
            // PaletteTable
            // 
            this.PaletteTable.Dock = System.Windows.Forms.DockStyle.Fill;
            this.PaletteTable.Location = new System.Drawing.Point(3, 3);
            this.PaletteTable.Name = "PaletteTable";
            this.PaletteTable.Size = new System.Drawing.Size(467, 255);
            this.PaletteTable.TabIndex = 7;
            // 
            // Offset
            // 
            this.Offset.Text = "Offset";
            // 
            // CostumeControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "CostumeControl";
            this.Contents.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel1.PerformLayout();
            this.splitContainer2.Panel2.ResumeLayout(false);
            this.splitContainer2.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            this.tabPage5.ResumeLayout(false);
            this.tabPage6.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox CloseByte;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox Format;
        private System.Windows.Forms.TextBox Size;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox Header;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox NumAnimations;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.ListView AnimOffsets;
        private System.Windows.Forms.ColumnHeader k;
        private System.Windows.Forms.ColumnHeader Offset;
        private System.Windows.Forms.TreeView Animations;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.ListView Commands;
        private System.Windows.Forms.TextBox AnimationCommandsOffset;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.ColumnHeader Command;
        private System.Windows.Forms.ColumnHeader Action;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.ListView LimbsOffsets;
        private System.Windows.Forms.ColumnHeader LimbOffset;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TreeView Limbs;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.ListView Pictures;
        private System.Windows.Forms.ColumnHeader Width;
        private System.Windows.Forms.ColumnHeader Height;
        private System.Windows.Forms.ColumnHeader RelX;
        private System.Windows.Forms.ColumnHeader RelY;
        private System.Windows.Forms.ColumnHeader MoveX;
        private System.Windows.Forms.ColumnHeader MoveY;
        private System.Windows.Forms.ColumnHeader RedirLimb;
        private System.Windows.Forms.ColumnHeader RedirPict;
        private System.Windows.Forms.ColumnHeader ImageSize;
        private System.Windows.Forms.TabPage tabPage6;
        private System.Windows.Forms.ColumnHeader PictureId;
        private System.Windows.Forms.FlowLayoutPanel PaletteTable;
        private System.Windows.Forms.ToolTip toolTip1;
    }
}
