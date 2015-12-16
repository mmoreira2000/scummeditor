namespace ScummEditor.Gui
{
    partial class ObjectImageHeaderControl
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
            this.Id = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.NumImages = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.NumZPlanes = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.Unknown = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.LocationX = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.LocationY = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.LocationWidth = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.LocationHeigth = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.NumHotspots = new System.Windows.Forms.TextBox();
            this.Hotspots = new System.Windows.Forms.ListView();
            this.X = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Y = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.Hotspots);
            this.Contents.Controls.Add(this.label12);
            this.Contents.Controls.Add(this.NumHotspots);
            this.Contents.Controls.Add(this.label11);
            this.Contents.Controls.Add(this.LocationHeigth);
            this.Contents.Controls.Add(this.label10);
            this.Contents.Controls.Add(this.LocationWidth);
            this.Contents.Controls.Add(this.label9);
            this.Contents.Controls.Add(this.LocationY);
            this.Contents.Controls.Add(this.label8);
            this.Contents.Controls.Add(this.LocationX);
            this.Contents.Controls.Add(this.label7);
            this.Contents.Controls.Add(this.Unknown);
            this.Contents.Controls.Add(this.label6);
            this.Contents.Controls.Add(this.NumZPlanes);
            this.Contents.Controls.Add(this.label5);
            this.Contents.Controls.Add(this.NumImages);
            this.Contents.Controls.Add(this.label4);
            this.Contents.Controls.Add(this.Id);
            // 
            // Id
            // 
            this.Id.Location = new System.Drawing.Point(68, 9);
            this.Id.Name = "Id";
            this.Id.ReadOnly = true;
            this.Id.Size = new System.Drawing.Size(100, 20);
            this.Id.TabIndex = 0;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(3, 12);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(21, 13);
            this.label4.TabIndex = 1;
            this.label4.Text = "ID:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(3, 97);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(96, 13);
            this.label5.TabIndex = 3;
            this.label5.Text = "Number of Images:";
            // 
            // NumImages
            // 
            this.NumImages.Location = new System.Drawing.Point(113, 94);
            this.NumImages.Name = "NumImages";
            this.NumImages.ReadOnly = true;
            this.NumImages.Size = new System.Drawing.Size(100, 20);
            this.NumImages.TabIndex = 2;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 123);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(104, 13);
            this.label6.TabIndex = 5;
            this.label6.Text = "Number of Z-Planes:";
            // 
            // NumZPlanes
            // 
            this.NumZPlanes.Location = new System.Drawing.Point(113, 120);
            this.NumZPlanes.Name = "NumZPlanes";
            this.NumZPlanes.ReadOnly = true;
            this.NumZPlanes.Size = new System.Drawing.Size(100, 20);
            this.NumZPlanes.TabIndex = 4;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(178, 12);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(56, 13);
            this.label7.TabIndex = 7;
            this.label7.Text = "Unknown:";
            // 
            // Unknown
            // 
            this.Unknown.Location = new System.Drawing.Point(243, 9);
            this.Unknown.Name = "Unknown";
            this.Unknown.ReadOnly = true;
            this.Unknown.Size = new System.Drawing.Size(100, 20);
            this.Unknown.TabIndex = 6;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(3, 38);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(17, 13);
            this.label8.TabIndex = 9;
            this.label8.Text = "X:";
            // 
            // LocationX
            // 
            this.LocationX.Location = new System.Drawing.Point(68, 35);
            this.LocationX.Name = "LocationX";
            this.LocationX.ReadOnly = true;
            this.LocationX.Size = new System.Drawing.Size(100, 20);
            this.LocationX.TabIndex = 8;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(3, 64);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(17, 13);
            this.label9.TabIndex = 11;
            this.label9.Text = "Y:";
            // 
            // LocationY
            // 
            this.LocationY.Location = new System.Drawing.Point(68, 61);
            this.LocationY.Name = "LocationY";
            this.LocationY.ReadOnly = true;
            this.LocationY.Size = new System.Drawing.Size(100, 20);
            this.LocationY.TabIndex = 10;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(178, 38);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(38, 13);
            this.label10.TabIndex = 13;
            this.label10.Text = "Width:";
            // 
            // LocationWidth
            // 
            this.LocationWidth.Location = new System.Drawing.Point(243, 35);
            this.LocationWidth.Name = "LocationWidth";
            this.LocationWidth.ReadOnly = true;
            this.LocationWidth.Size = new System.Drawing.Size(100, 20);
            this.LocationWidth.TabIndex = 12;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(178, 64);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(41, 13);
            this.label11.TabIndex = 15;
            this.label11.Text = "Heigth:";
            // 
            // LocationHeigth
            // 
            this.LocationHeigth.Location = new System.Drawing.Point(243, 61);
            this.LocationHeigth.Name = "LocationHeigth";
            this.LocationHeigth.ReadOnly = true;
            this.LocationHeigth.Size = new System.Drawing.Size(100, 20);
            this.LocationHeigth.TabIndex = 14;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(3, 154);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(104, 13);
            this.label12.TabIndex = 17;
            this.label12.Text = "Number of Hotspots:";
            // 
            // NumHotspots
            // 
            this.NumHotspots.Location = new System.Drawing.Point(113, 151);
            this.NumHotspots.Name = "NumHotspots";
            this.NumHotspots.ReadOnly = true;
            this.NumHotspots.Size = new System.Drawing.Size(100, 20);
            this.NumHotspots.TabIndex = 16;
            // 
            // Hotspots
            // 
            this.Hotspots.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.Hotspots.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.X,
            this.Y});
            this.Hotspots.FullRowSelect = true;
            this.Hotspots.Location = new System.Drawing.Point(0, 177);
            this.Hotspots.Name = "Hotspots";
            this.Hotspots.Size = new System.Drawing.Size(481, 110);
            this.Hotspots.TabIndex = 18;
            this.Hotspots.UseCompatibleStateImageBehavior = false;
            this.Hotspots.View = System.Windows.Forms.View.Details;
            // 
            // X
            // 
            this.X.Text = "X";
            this.X.Width = 100;
            // 
            // Y
            // 
            this.Y.Text = "Y";
            this.Y.Width = 100;
            // 
            // ObjectImageHeader
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "ObjectImageHeader";
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox Id;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.TextBox NumHotspots;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.TextBox LocationHeigth;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox LocationWidth;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox LocationY;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox LocationX;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox Unknown;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox NumZPlanes;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox NumImages;
        private System.Windows.Forms.ListView Hotspots;
        private System.Windows.Forms.ColumnHeader X;
        private System.Windows.Forms.ColumnHeader Y;
    }
}
