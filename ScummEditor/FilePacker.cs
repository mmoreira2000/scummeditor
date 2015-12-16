using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ScummEditor;
using ScummEditor.Encoders;
using ScummEditor.Gui;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor
{
    public partial class FilePacker : Form
    {
        private TreeNavigatorManager treeNavigatorManager;
        private ScummV6GameData scummFile;

        public FilePacker()
        {
            InitializeComponent();

            treeNavigatorManager = new TreeNavigatorManager(ResourceNavigator, ContentContainer.Panel2);

            Text = string.Format(Text, Application.ProductVersion);
        }

        private void UnXorFile(int xorKey, string sourceFile)
        {
            var x = new XoredFileStream(xorKey, sourceFile, FileMode.Open, FileAccess.Read);

            var x2 = new FileStream(sourceFile + ".unxor", FileMode.Create, FileAccess.Write);


            int length = (int)x.Length;  // get file length
            var buffer = new byte[length];            // create buffer
            int count;                            // actual number of bytes read
            int sum = 0;                          // total number of bytes read

            // read until Read method returns 0 (end of the stream has been reached)
            while ((count = x.Read(buffer, 0, length - sum)) > 0)
            {
                sum += count;  // sum is a buffer offset for next reading
                x2.Write(buffer, 0, count);
            }

            x.Flush();
            x.Close();
            x2.Flush();
            x2.Close();

        }

        private void LoadGame()
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == DialogResult.Cancel) return;

            scummFile = new ScummV6GameData();
            scummFile.LoadDataFromDisc(dlg.FileName);

            LoadedGame.Text = GetGameName(scummFile.LoadedGameInfo.LoadedGame);

            treeNavigatorManager.ScummV6GameData = scummFile;
            treeNavigatorManager.LoadTree();
        }

        private void SaveGame()
        {
            scummFile.SaveDataToDisk();
        }

        private void exportAllRoomBackgroundImagesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var exportResources = new ExportResources();

            exportResources.ShowDialog(scummFile, this);
        }

        private void OpenFileButton_Click(object sender, EventArgs e)
        {
            LoadGame();
        }

        private void TestCalculateBlockSize_Click(object sender, EventArgs e)
        {
            scummFile.PostProcessChanges();
        }

        private void SaveDataFile_Click(object sender, EventArgs e)
        {
            SaveGame();
        }

        private string GetGameName(ScummGame game)
        {
            switch (game)
            {
                case ScummGame.DayOfTheTentacle:
                    return "Day of Tentacle (Talkie)";
                case ScummGame.SamAndMax:
                    return "Sam & Max Hit The Road (Talkie)";
                case ScummGame.FateOfAtlantis:
                    return "Indiana Jones And The Fate of Atlantis (Talkie)";
                case ScummGame.MonkeyIsland1VGA:
                    return "The Secret Of Monkey Island (CD)";
                case ScummGame.MonkeyIsland1VGASpeech:
                    return "The Secret Of Monkey Island (CD) (Talkie)";
                case ScummGame.MonkeyIsland2:
                    return "Monkey Island 2: LeChuck's Revenge (CD)";
                case ScummGame.None:
                default:
                    return "None";
            }
        }

        private void convertFile_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == DialogResult.Cancel) return;

            var scummGame = Functions.FindScummGame(dlg.FileName);
            if (scummGame.LoadedGame == ScummGame.None)
            {
                MessageBox.Show("No Know game found.");
                return;
            }

            string gameName = GetGameName(scummGame.LoadedGame);
            MessageBox.Show(string.Format("Found game '{0}'. The file will be decrypted with the .unxor extension", gameName));

            UnXorFile(scummGame.XorKey, scummGame.IndexFile);
            UnXorFile(scummGame.XorKey, scummGame.DataFile);
        }

        private void openFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            LoadGame();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void HelpAbout_Click(object sender, EventArgs e)
        {
            var about = new About();
            about.ShowDialog(this);
        }

        private void ExportGameGraphicsButton_Click(object sender, EventArgs e)
        {
            var exportResources = new ExportResources();

            exportResources.ShowDialog(scummFile, this);
        }

        private void ImportGameGraphics_Click(object sender, EventArgs e)
        {
            var exportResources = new ImportResources();

            exportResources.ShowDialog(scummFile, this);
        }

        private void AboutToolbar_Click(object sender, EventArgs e)
        {
            HelpAbout_Click(this, e);
        }

        private void ImportGameGraphicsButton_Click(object sender, EventArgs e)
        {
            ImportGameGraphics_Click(sender, e);
        }

        private void saveChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveGame();
        }
    }
}
