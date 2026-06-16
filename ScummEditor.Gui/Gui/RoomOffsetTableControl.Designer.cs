namespace ScummEditor.Gui
{
    partial class RoomOffsetTableControl
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
            this.label4 = new System.Windows.Forms.Label();
            this.NumberOfItems = new System.Windows.Forms.TextBox();
            this.RoomsInfo = new System.Windows.Forms.ListView();
            this.RoomNumber = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.RoomOffSet = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.label4);
            this.Contents.Controls.Add(this.NumberOfItems);
            this.Contents.Controls.Add(this.RoomsInfo);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(-2, 6);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(87, 13);
            this.label4.TabIndex = 12;
            this.label4.Text = "Number of Items:";
            // 
            // NumberOfItems
            // 
            this.NumberOfItems.Location = new System.Drawing.Point(83, 0);
            this.NumberOfItems.Name = "NumberOfItems";
            this.NumberOfItems.ReadOnly = true;
            this.NumberOfItems.Size = new System.Drawing.Size(135, 20);
            this.NumberOfItems.TabIndex = 11;
            // 
            // RoomsInfo
            // 
            this.RoomsInfo.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.RoomsInfo.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.RoomNumber,
            this.RoomOffSet});
            this.RoomsInfo.FullRowSelect = true;
            this.RoomsInfo.Location = new System.Drawing.Point(1, 29);
            this.RoomsInfo.Name = "RoomsInfo";
            this.RoomsInfo.Size = new System.Drawing.Size(481, 257);
            this.RoomsInfo.TabIndex = 10;
            this.RoomsInfo.UseCompatibleStateImageBehavior = false;
            this.RoomsInfo.View = System.Windows.Forms.View.Details;
            // 
            // RoomNumber
            // 
            this.RoomNumber.Text = "Room Number";
            this.RoomNumber.Width = 100;
            // 
            // RoomOffSet
            // 
            this.RoomOffSet.Text = "Offset";
            this.RoomOffSet.Width = 100;
            // 
            // RoomOffsetTableControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "RoomOffsetTableControl";
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox NumberOfItems;
        private System.Windows.Forms.ListView RoomsInfo;
        private System.Windows.Forms.ColumnHeader RoomNumber;
        private System.Windows.Forms.ColumnHeader RoomOffSet;
    }
}
