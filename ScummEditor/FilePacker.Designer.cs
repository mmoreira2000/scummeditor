namespace ScummEditor
{
    partial class FilePacker
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FilePacker));
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveChangesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.exportAllRoomBackgroundImagesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ImportGameGraphics = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.HelpAbout = new System.Windows.Forms.ToolStripMenuItem();
            this.label2 = new System.Windows.Forms.Label();
            this.ResourceNavigator = new System.Windows.Forms.TreeView();
            this.Navigator = new System.Windows.Forms.GroupBox();
            this.ContentContainer = new System.Windows.Forms.SplitContainer();
            this.toolStrip1 = new System.Windows.Forms.ToolStrip();
            this.OpenFileButton = new System.Windows.Forms.ToolStripButton();
            this.SaveDataFile = new System.Windows.Forms.ToolStripButton();
            this.convertFile = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.ExportGameGraphicsButton = new System.Windows.Forms.ToolStripButton();
            this.ImportGameGraphicsButton = new System.Windows.Forms.ToolStripButton();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.TestCalculateBlockSize = new System.Windows.Forms.ToolStripButton();
            this.AboutToolbar = new System.Windows.Forms.ToolStripButton();
            this.StatusBar = new System.Windows.Forms.StatusStrip();
            this.Progress = new System.Windows.Forms.ToolStripProgressBar();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.LoadedGame = new System.Windows.Forms.ToolStripStatusLabel();
            this.menuStrip1.SuspendLayout();
            this.Navigator.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ContentContainer)).BeginInit();
            this.ContentContainer.Panel1.SuspendLayout();
            this.ContentContainer.SuspendLayout();
            this.toolStrip1.SuspendLayout();
            this.StatusBar.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(732, 24);
            this.menuStrip1.TabIndex = 8;
            this.menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openFileToolStripMenuItem,
            this.saveChangesToolStripMenuItem,
            this.toolStripSeparator4,
            this.exportAllRoomBackgroundImagesToolStripMenuItem,
            this.ImportGameGraphics,
            this.toolStripSeparator3,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openFileToolStripMenuItem
            // 
            this.openFileToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("openFileToolStripMenuItem.Image")));
            this.openFileToolStripMenuItem.Name = "openFileToolStripMenuItem";
            this.openFileToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.openFileToolStripMenuItem.Text = "Open File";
            this.openFileToolStripMenuItem.Click += new System.EventHandler(this.openFileToolStripMenuItem_Click);
            // 
            // saveChangesToolStripMenuItem
            // 
            this.saveChangesToolStripMenuItem.Image = global::ScummEditor.Properties.Resources.Disk_blue;
            this.saveChangesToolStripMenuItem.Name = "saveChangesToolStripMenuItem";
            this.saveChangesToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.saveChangesToolStripMenuItem.Text = "Save Changes";
            this.saveChangesToolStripMenuItem.Click += new System.EventHandler(this.saveChangesToolStripMenuItem_Click);
            // 
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(188, 6);
            // 
            // exportAllRoomBackgroundImagesToolStripMenuItem
            // 
            this.exportAllRoomBackgroundImagesToolStripMenuItem.Image = ((System.Drawing.Image)(resources.GetObject("exportAllRoomBackgroundImagesToolStripMenuItem.Image")));
            this.exportAllRoomBackgroundImagesToolStripMenuItem.Name = "exportAllRoomBackgroundImagesToolStripMenuItem";
            this.exportAllRoomBackgroundImagesToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.exportAllRoomBackgroundImagesToolStripMenuItem.Text = "Export game graphics";
            this.exportAllRoomBackgroundImagesToolStripMenuItem.Click += new System.EventHandler(this.exportAllRoomBackgroundImagesToolStripMenuItem_Click);
            // 
            // ImportGameGraphics
            // 
            this.ImportGameGraphics.Image = ((System.Drawing.Image)(resources.GetObject("ImportGameGraphics.Image")));
            this.ImportGameGraphics.Name = "ImportGameGraphics";
            this.ImportGameGraphics.Size = new System.Drawing.Size(191, 22);
            this.ImportGameGraphics.Text = "Import game graphics";
            this.ImportGameGraphics.Click += new System.EventHandler(this.ImportGameGraphics_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(188, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(191, 22);
            this.exitToolStripMenuItem.Text = "&Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.HelpAbout});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // HelpAbout
            // 
            this.HelpAbout.Image = global::ScummEditor.Properties.Resources.sbr_ico_16x16;
            this.HelpAbout.Name = "HelpAbout";
            this.HelpAbout.Size = new System.Drawing.Size(107, 22);
            this.HelpAbout.Text = "&About";
            this.HelpAbout.Click += new System.EventHandler(this.HelpAbout_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(143, 30);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(65, 13);
            this.label2.TabIndex = 10;
            this.label2.Text = "Loaded File:";
            // 
            // ResourceNavigator
            // 
            this.ResourceNavigator.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ResourceNavigator.FullRowSelect = true;
            this.ResourceNavigator.HideSelection = false;
            this.ResourceNavigator.Location = new System.Drawing.Point(0, 0);
            this.ResourceNavigator.Name = "ResourceNavigator";
            this.ResourceNavigator.Size = new System.Drawing.Size(236, 589);
            this.ResourceNavigator.TabIndex = 13;
            // 
            // Navigator
            // 
            this.Navigator.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Navigator.Controls.Add(this.ContentContainer);
            this.Navigator.Location = new System.Drawing.Point(0, 52);
            this.Navigator.Name = "Navigator";
            this.Navigator.Size = new System.Drawing.Size(732, 610);
            this.Navigator.TabIndex = 14;
            this.Navigator.TabStop = false;
            this.Navigator.Text = "Content Navigator";
            // 
            // ContentContainer
            // 
            this.ContentContainer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ContentContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ContentContainer.Location = new System.Drawing.Point(3, 16);
            this.ContentContainer.Name = "ContentContainer";
            // 
            // ContentContainer.Panel1
            // 
            this.ContentContainer.Panel1.Controls.Add(this.ResourceNavigator);
            this.ContentContainer.Size = new System.Drawing.Size(726, 591);
            this.ContentContainer.SplitterDistance = 238;
            this.ContentContainer.TabIndex = 14;
            // 
            // toolStrip1
            // 
            this.toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.OpenFileButton,
            this.SaveDataFile,
            this.convertFile,
            this.toolStripSeparator2,
            this.ExportGameGraphicsButton,
            this.ImportGameGraphicsButton,
            this.toolStripSeparator1,
            this.TestCalculateBlockSize,
            this.AboutToolbar});
            this.toolStrip1.Location = new System.Drawing.Point(0, 24);
            this.toolStrip1.Name = "toolStrip1";
            this.toolStrip1.Size = new System.Drawing.Size(732, 25);
            this.toolStrip1.TabIndex = 18;
            this.toolStrip1.Text = "toolStrip1";
            // 
            // OpenFileButton
            // 
            this.OpenFileButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.OpenFileButton.Image = ((System.Drawing.Image)(resources.GetObject("OpenFileButton.Image")));
            this.OpenFileButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.OpenFileButton.Name = "OpenFileButton";
            this.OpenFileButton.Size = new System.Drawing.Size(23, 22);
            this.OpenFileButton.Text = "Open game data file";
            this.OpenFileButton.Click += new System.EventHandler(this.OpenFileButton_Click);
            // 
            // SaveDataFile
            // 
            this.SaveDataFile.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.SaveDataFile.Image = ((System.Drawing.Image)(resources.GetObject("SaveDataFile.Image")));
            this.SaveDataFile.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.SaveDataFile.Name = "SaveDataFile";
            this.SaveDataFile.Size = new System.Drawing.Size(23, 22);
            this.SaveDataFile.Text = "Save game data file";
            this.SaveDataFile.Click += new System.EventHandler(this.SaveDataFile_Click);
            // 
            // convertFile
            // 
            this.convertFile.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.convertFile.Image = ((System.Drawing.Image)(resources.GetObject("convertFile.Image")));
            this.convertFile.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.convertFile.Name = "convertFile";
            this.convertFile.Size = new System.Drawing.Size(23, 22);
            this.convertFile.Text = "Unxor game files";
            this.convertFile.Click += new System.EventHandler(this.convertFile_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
            // 
            // ExportGameGraphicsButton
            // 
            this.ExportGameGraphicsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.ExportGameGraphicsButton.Image = ((System.Drawing.Image)(resources.GetObject("ExportGameGraphicsButton.Image")));
            this.ExportGameGraphicsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ExportGameGraphicsButton.Name = "ExportGameGraphicsButton";
            this.ExportGameGraphicsButton.Size = new System.Drawing.Size(23, 22);
            this.ExportGameGraphicsButton.Text = "Export Game Graphics";
            this.ExportGameGraphicsButton.Click += new System.EventHandler(this.ExportGameGraphicsButton_Click);
            // 
            // ImportGameGraphicsButton
            // 
            this.ImportGameGraphicsButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.ImportGameGraphicsButton.Image = ((System.Drawing.Image)(resources.GetObject("ImportGameGraphicsButton.Image")));
            this.ImportGameGraphicsButton.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.ImportGameGraphicsButton.Name = "ImportGameGraphicsButton";
            this.ImportGameGraphicsButton.Size = new System.Drawing.Size(23, 22);
            this.ImportGameGraphicsButton.Text = "Import Game Graphics";
            this.ImportGameGraphicsButton.Click += new System.EventHandler(this.ImportGameGraphicsButton_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
            // 
            // TestCalculateBlockSize
            // 
            this.TestCalculateBlockSize.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.TestCalculateBlockSize.Image = ((System.Drawing.Image)(resources.GetObject("TestCalculateBlockSize.Image")));
            this.TestCalculateBlockSize.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.TestCalculateBlockSize.Name = "TestCalculateBlockSize";
            this.TestCalculateBlockSize.Size = new System.Drawing.Size(23, 22);
            this.TestCalculateBlockSize.Text = "Test blocksize and blockoffset calculations (internal debug only)";
            this.TestCalculateBlockSize.Visible = false;
            this.TestCalculateBlockSize.Click += new System.EventHandler(this.TestCalculateBlockSize_Click);
            // 
            // AboutToolbar
            // 
            this.AboutToolbar.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.AboutToolbar.Image = ((System.Drawing.Image)(resources.GetObject("AboutToolbar.Image")));
            this.AboutToolbar.ImageTransparentColor = System.Drawing.Color.Magenta;
            this.AboutToolbar.Name = "AboutToolbar";
            this.AboutToolbar.Size = new System.Drawing.Size(23, 22);
            this.AboutToolbar.Text = "About";
            this.AboutToolbar.Click += new System.EventHandler(this.AboutToolbar_Click);
            // 
            // StatusBar
            // 
            this.StatusBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.Progress,
            this.toolStripStatusLabel1,
            this.LoadedGame});
            this.StatusBar.Location = new System.Drawing.Point(0, 665);
            this.StatusBar.Name = "StatusBar";
            this.StatusBar.Size = new System.Drawing.Size(732, 22);
            this.StatusBar.TabIndex = 19;
            this.StatusBar.Text = "statusStrip1";
            // 
            // Progress
            // 
            this.Progress.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.Progress.Name = "Progress";
            this.Progress.Size = new System.Drawing.Size(150, 16);
            this.Progress.Visible = false;
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            this.toolStripStatusLabel1.Size = new System.Drawing.Size(83, 17);
            this.toolStripStatusLabel1.Text = "Loaded Game:";
            // 
            // LoadedGame
            // 
            this.LoadedGame.Name = "LoadedGame";
            this.LoadedGame.Size = new System.Drawing.Size(36, 17);
            this.LoadedGame.Text = "None";
            // 
            // FilePacker
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(732, 687);
            this.Controls.Add(this.StatusBar);
            this.Controls.Add(this.toolStrip1);
            this.Controls.Add(this.Navigator);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.menuStrip1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip1;
            this.Name = "FilePacker";
            this.Text = "SCUMMeditor {0}";
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.Navigator.ResumeLayout(false);
            this.ContentContainer.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.ContentContainer)).EndInit();
            this.ContentContainer.ResumeLayout(false);
            this.toolStrip1.ResumeLayout(false);
            this.toolStrip1.PerformLayout();
            this.StatusBar.ResumeLayout(false);
            this.StatusBar.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openFileToolStripMenuItem;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TreeView ResourceNavigator;
        private System.Windows.Forms.GroupBox Navigator;
        private System.Windows.Forms.SplitContainer ContentContainer;
        private System.Windows.Forms.ToolStripMenuItem exportAllRoomBackgroundImagesToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripButton OpenFileButton;
        private System.Windows.Forms.ToolStripButton SaveDataFile;
        private System.Windows.Forms.ToolStripButton convertFile;
        private System.Windows.Forms.ToolStripButton TestCalculateBlockSize;
        private System.Windows.Forms.StatusStrip StatusBar;
        private System.Windows.Forms.ToolStripStatusLabel LoadedGame;
        private System.Windows.Forms.ToolStripProgressBar Progress;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton ExportGameGraphicsButton;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem HelpAbout;
        private System.Windows.Forms.ToolStripMenuItem ImportGameGraphics;
        private System.Windows.Forms.ToolStripButton ImportGameGraphicsButton;
        private System.Windows.Forms.ToolStripButton AboutToolbar;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem saveChangesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
    }
}

