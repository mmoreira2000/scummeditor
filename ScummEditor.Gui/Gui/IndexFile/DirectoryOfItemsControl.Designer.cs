namespace ScummEditor.Gui.IndexFile
{
    partial class DirectoryOfItemsControl
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
            this.RoomsInfo = new System.Windows.Forms.ListView();
            this.RoomNumber = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.RoomOffSet = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.NumberOfItems = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.Contents.SuspendLayout();
            this.SuspendLayout();
            // 
            // Contents
            // 
            this.Contents.Controls.Add(this.label4);
            this.Contents.Controls.Add(this.NumberOfItems);
            this.Contents.Controls.Add(this.RoomsInfo);
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
            this.RoomsInfo.Location = new System.Drawing.Point(0, 30);
            this.RoomsInfo.Name = "RoomsInfo";
            this.RoomsInfo.Size = new System.Drawing.Size(481, 257);
            this.RoomsInfo.TabIndex = 4;
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
            // NumberOfItems
            // 
            this.NumberOfItems.Location = new System.Drawing.Point(81, 3);
            this.NumberOfItems.Name = "NumberOfItems";
            this.NumberOfItems.Size = new System.Drawing.Size(135, 20);
            this.NumberOfItems.TabIndex = 5;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(-3, 7);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(87, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Number of Items:";
            // 
            // DirectoryOfItemsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Name = "DirectoryOfItemsControl";
            this.Contents.ResumeLayout(false);
            this.Contents.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView RoomsInfo;
        private System.Windows.Forms.ColumnHeader RoomNumber;
        private System.Windows.Forms.ColumnHeader RoomOffSet;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox NumberOfItems;
    }
}
