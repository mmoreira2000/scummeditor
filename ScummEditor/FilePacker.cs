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
            var dialog = new FolderBrowserDialog
            {
                Description = "Select the game folder (the one with the game data files, e.g. TENTACLE.000/.001)."
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            // The detection looks only at the content of the selected folder.
            GameInfo gameInfo = Functions.FindScummGameInFolder(dialog.SelectedPath);
            if (gameInfo.LoadedGame == ScummGame.None)
            {
                MessageBox.Show(this,
                    "No SCUMM game was found in the selected folder.",
                    "Open game folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            scummFile = new ScummV6GameData();
            scummFile.LoadFromGameInfo(gameInfo);

            string language = DetectLanguageSafe();
            LoadedGame.Text = BuildLoadedGameStatus(scummFile.LoadedGameInfo, language);

            treeNavigatorManager.ScummV6GameData = scummFile;
            treeNavigatorManager.LoadTree();
        }

        /// <summary>Language detection is optional - it must never break the game loading.</summary>
        private string DetectLanguageSafe()
        {
            try
            {
                return GameLanguageDetector.Detect(scummFile.DataFile);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Status bar text: game name, its edition (+ language when known) and the SCUMM version.</summary>
        private string BuildLoadedGameStatus(GameInfo gameInfo, string language)
        {
            string gameName = GetGameName(gameInfo.LoadedGame);

            string edition;
            if (gameInfo.IsTalkie)
            {
                edition = "Talkie";
            }
            else
            {
                edition = "Floppy";
            }

            string details = edition;
            if (language != null)
            {
                details = edition + ", " + language;
            }

            return gameName + " (" + details + ")  -  SCUMM v" + gameInfo.ScummVersion;
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
            // The edition (Talkie/Floppy) is detected separately - see BuildLoadedGameStatus.
            switch (game)
            {
                case ScummGame.DayOfTheTentacle:
                    return "Day of the Tentacle";
                case ScummGame.SamAndMax:
                    return "Sam & Max Hit the Road";
                case ScummGame.FateOfAtlantis:
                    return "Indiana Jones and the Fate of Atlantis";
                case ScummGame.MonkeyIsland1VGA:
                case ScummGame.MonkeyIsland1VGASpeech:
                    return "The Secret of Monkey Island (CD)";
                case ScummGame.MonkeyIsland2:
                    return "Monkey Island 2: LeChuck's Revenge";
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

        private void exportGameTextsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (scummFile == null || scummFile.DataFile == null)
            {
                MessageBox.Show(this, "Open a game first.", "Export game texts");
                return;
            }

            var dlg = new SaveFileDialog
            {
                Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = Path.GetFileNameWithoutExtension(scummFile.LoadedGameInfo.DataFile) + "-texts.txt"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            GameTextCodec codec;
            if (!TryPromptCharmap(out codec)) return;

            try
            {
                int count = GameTextManager.ExportToFile(scummFile.DataFile, dlg.FileName, codec,
                    Path.GetFileName(scummFile.LoadedGameInfo.DataFile));
                MessageBox.Show(this, count + " textos exportados para:\n" + dlg.FileName,
                    "Export game texts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Falha ao exportar: " + ex.Message, "Export game texts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Optional step of the text export: the translator may paste the "; charmap:" line of an
        /// in-progress translation so the new export keeps its custom characters. Empty input
        /// falls back to the default charmap; Cancel aborts the export (returns false).
        /// </summary>
        private bool TryPromptCharmap(out GameTextCodec codec)
        {
            codec = null;
            string lastInput = string.Empty;

            while (true)
            {
                using (var form = new Form())
                {
                    var label = new Label
                    {
                        Text = "Optional: paste the \"; charmap:\" line of an existing translation file so this " +
                               "export keeps its custom characters. Leave empty to use the default charmap."
                    };
                    label.SetBounds(12, 9, 596, 42);

                    var input = new TextBox { Text = lastInput };
                    input.SetBounds(12, 54, 596, 20);

                    var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
                    ok.SetBounds(452, 84, 75, 26);
                    var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
                    cancel.SetBounds(533, 84, 75, 26);

                    form.Text = "Export game texts - charmap";
                    form.FormBorderStyle = FormBorderStyle.FixedDialog;
                    form.MinimizeBox = false;
                    form.MaximizeBox = false;
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.ClientSize = new Size(620, 120);
                    form.Controls.AddRange(new Control[] { label, input, ok, cancel });
                    form.AcceptButton = ok;
                    form.CancelButton = cancel;

                    if (form.ShowDialog(this) != DialogResult.OK) return false;
                    lastInput = input.Text;
                }

                try
                {
                    codec = GameTextCodec.ParsePastedCharmap(lastInput);
                    return true;
                }
                catch (FormatException ex)
                {
                    MessageBox.Show(this, "Charmap inválido: " + ex.Message, "Export game texts",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void importGameTextsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (scummFile == null || scummFile.DataFile == null)
            {
                MessageBox.Show(this, "Open a game first.", "Import game texts");
                return;
            }

            var dlg = new OpenFileDialog { Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                GameTextImportReport report = GameTextManager.ImportFromFile(scummFile.DataFile, dlg.FileName);

                string message = report.Summary();
                if (report.HasChanges)
                    message += Environment.NewLine + "Use 'Save Changes' para gravar as alterações nos arquivos do jogo.";

                MessageBox.Show(this, message, "Import game texts", MessageBoxButtons.OK,
                    report.Errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Falha ao importar: " + ex.Message, "Import game texts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void exportGameFontsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (scummFile == null || scummFile.DataFile == null)
            {
                MessageBox.Show(this, "Open a game first.", "Export game fonts");
                return;
            }

            var dlg = new FolderBrowserDialog
            {
                Description = "Pasta para salvar as fontes do jogo (charset_N.png + charset_N.guide.png)"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string report = CharsetPngCodec.ExportAll(scummFile.DataFile, dlg.SelectedPath);
                MessageBox.Show(this, report, "Export game fonts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Falha ao exportar: " + ex.Message, "Export game fonts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void importGameFontsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (scummFile == null || scummFile.DataFile == null)
            {
                MessageBox.Show(this, "Open a game first.", "Import game fonts");
                return;
            }

            var dlg = new FolderBrowserDialog
            {
                Description = "Pasta com os arquivos charset_N.png a importar"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string report = CharsetPngCodec.ImportAll(scummFile.DataFile, dlg.SelectedPath);
                MessageBox.Show(this,
                    report + Environment.NewLine + "Use 'Save Changes' para gravar as alterações nos arquivos do jogo.",
                    "Import game fonts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Falha ao importar: " + ex.Message, "Import game fonts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
