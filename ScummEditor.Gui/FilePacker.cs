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
using ScummEditor.Engine;
using ScummEditor.Engine.Encoders;
using ScummEditor.Gui;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class FilePacker : Form
    {
        private TreeNavigatorManager treeNavigatorManager;
        private ScummGameData scummFile;

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

            scummFile = ScummGameData.LoadFromGameInfo(gameInfo);

            // The MD5-table language was set at detection; refine it with the content heuristic now that the
            // game is loaded (fills Unknown, and corrects a fan-translation that kept the English index).
            RefineLanguageSafe();
            ScummLanguage lang = scummFile.LoadedGameInfo.Language;
            string language = lang != ScummLanguage.Unknown ? ScummLanguageNames.DisplayName(lang) : null;
            LoadedGame.Text = BuildLoadedGameStatus(scummFile.LoadedGameInfo, language);

            treeNavigatorManager.GameData = scummFile;
            treeNavigatorManager.LoadTree();
        }

        /// <summary>Language refinement is optional - it must never break the game loading.</summary>
        private void RefineLanguageSafe()
        {
            try { ScummLanguageDetector.RefineFromContent(scummFile); }
            catch (Exception) { }
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
            else if (gameInfo.ScummVersion == 4)
            {
                // v4 floppy games: the graphics edition (EGA/VGA) is detected from the data.
                edition = GetV4EditionName(gameInfo.Edition);
            }
            else if (gameInfo.HasCdAudio)
            {
                // CD edition without recorded speech (e.g. Monkey Island 1 CD: music on CD audio).
                edition = "CD";
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

        private static string GetV4EditionName(GameEdition edition)
        {
            switch (edition)
            {
                case GameEdition.FloppyEga:
                    return "Floppy EGA";
                case GameEdition.FloppyVga:
                    return "Floppy VGA";
                case GameEdition.Cd:
                    return "CD";
                default:
                    return "Floppy";
            }
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

        // The edition (Talkie/Floppy) is shown separately - see BuildLoadedGameStatus. The name mapping
        // itself lives in the engine (ScummGameNames) so it covers every ScummGame value and is testable.
        private string GetGameName(ScummGame game)
        {
            return ScummGameNames.DisplayName(game);
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

        /// <summary>
        /// Exports a small scummvm.ini "launch profile" so the edited game starts with the correct
        /// engine/variant in ScummVM (auto-detection can fail on a modified game whose index MD5 changed).
        /// </summary>
        private void exportScummVmIniToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (scummFile == null || scummFile.LoadedGameInfo == null
                || scummFile.LoadedGameInfo.LoadedGame == ScummGame.None)
            {
                MessageBox.Show(this, "Open a game first.", "Export ScummVM profile");
                return;
            }

            GameInfo info = scummFile.LoadedGameInfo;
            string gameFolder = Path.GetDirectoryName(info.DataFile);

            var dlg = new SaveFileDialog
            {
                Filter = "ScummVM config (*.ini)|*.ini|All files (*.*)|*.*",
                FileName = ScummVmConfigExporter.SafeIniFileName(info)
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                ScummVmConfigExporter.Export(info, gameFolder, dlg.FileName);
                MessageBox.Show(this,
                    "ScummVM launch profile saved to:\n" + dlg.FileName + "\n\nRun it with:\n"
                    + "    scummvm --config=\"" + dlg.FileName + "\" " + ScummVmConfigExporter.BuildTargetName(info)
                    + "\n\nor paste its target section into your existing scummvm.ini.",
                    "Export ScummVM profile", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Export ScummVM profile",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                string gameLabel = Path.GetFileName(scummFile.LoadedGameInfo.DataFile);
                // v3 old-bundle (Loom EGA, Indy3 EGA) uses its own raw-room text pipeline; v4 and v3
                // "GF_OLD256" (one NN.LFL per room) use the v4 path; v5/v6 use the LFLF data file.
                int version = scummFile.LoadedGameInfo.ScummVersion;
                int count;
                if (version <= 2)
                    count = ScummV2TextManager.ExportToFile(scummFile, dlg.FileName, gameLabel);
                else if (version == 3 && scummFile.LoadedGameInfo.UsesOldBundle)
                    count = ScummV3OldTextManager.ExportToFile(scummFile, dlg.FileName, codec, gameLabel);
                else if (version == 4 || version == 3)
                    count = GameTextManager.ExportToFileV4(scummFile, dlg.FileName, codec, gameLabel);
                else
                    count = GameTextManager.ExportToFile(scummFile.DataFile, dlg.FileName, codec, gameLabel);
                MessageBox.Show(this, count + " texts exported to:\n" + dlg.FileName,
                    "Export game texts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Export game texts",
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
                    MessageBox.Show(this, "Invalid charmap: " + ex.Message, "Export game texts",
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
                int version = scummFile.LoadedGameInfo.ScummVersion;
                GameTextImportReport report;
                if (version <= 2)
                    report = ScummV2TextManager.ImportFromFile(scummFile, dlg.FileName);
                else if (version == 3 && scummFile.LoadedGameInfo.UsesOldBundle)
                    report = ScummV3OldTextManager.ImportFromFile(scummFile, dlg.FileName);
                else if (version == 4 || version == 3)
                    report = GameTextManager.ImportFromFileV4(scummFile, dlg.FileName);
                else
                    report = GameTextManager.ImportFromFile(scummFile.DataFile, dlg.FileName);

                string message = report.Summary();
                if (report.HasChanges)
                    message += Environment.NewLine + "Use 'Save Changes' to write the changes to the game files.";

                MessageBox.Show(this, message, "Import game texts", MessageBoxButtons.OK,
                    report.Errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Import failed: " + ex.Message, "Import game texts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void exportGameFontsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (scummFile == null || scummFile.LoadedGameInfo == null
                || scummFile.LoadedGameInfo.LoadedGame == ScummGame.None)
            {
                MessageBox.Show(this, "Open a game first.", "Export game fonts");
                return;
            }

            // v2 (Maniac/Zak) has no LFL charset - its font lives in the game .EXE.
            if (scummFile.LoadedGameInfo.ScummVersion <= 2)
            {
                ExportV2ExeFont();
                return;
            }

            var dlg = new FolderBrowserDialog
            {
                Description = "Folder to save the game fonts (charset_N.png + charset_N.guide.png)"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                // v3 (Loom/Indy3 EGA old-bundle AND Indy3 VGA / FM-Towns GF_OLD256) keep their fonts as
                // standalone 9N.LFL CharsetV3 files (in V3Charsets), NOT as v4+ Charset blocks - they need
                // the dedicated v3 codec. GetAllEditableCharsets() only sees v4+ charsets, so it would be
                // empty here.
                string report = HasV3Charsets()
                    ? CharsetV3PngCodec.ExportAll(scummFile.V3Charsets, dlg.SelectedPath)
                    : CharsetPngCodec.ExportAll(scummFile.GetAllEditableCharsets(), dlg.SelectedPath);
                MessageBox.Show(this, report, "Export game fonts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Export game fonts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>True when the game's fonts are standalone v3 charsets (9N.LFL), needing CharsetV3PngCodec.</summary>
        private bool HasV3Charsets()
        {
            return scummFile != null && scummFile.V3Charsets != null && scummFile.V3Charsets.Count > 0;
        }

        private void importGameFontsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (scummFile == null || scummFile.LoadedGameInfo == null
                || scummFile.LoadedGameInfo.LoadedGame == ScummGame.None)
            {
                MessageBox.Show(this, "Open a game first.", "Import game fonts");
                return;
            }

            if (scummFile.LoadedGameInfo.ScummVersion <= 2)
            {
                ImportV2ExeFont();
                return;
            }

            var dlg = new FolderBrowserDialog
            {
                Description = "Folder with the charset_N.png files to import"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string report = HasV3Charsets()
                    ? CharsetV3PngCodec.ImportAll(scummFile.V3Charsets, dlg.SelectedPath)
                    : CharsetPngCodec.ImportAll(scummFile.GetAllEditableCharsets(), dlg.SelectedPath);
                MessageBox.Show(this,
                    report + Environment.NewLine + "Use 'Save Changes' to write the changes to the game files.",
                    "Import game fonts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Import failed: " + ex.Message, "Import game fonts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Exports the v2 EXE-embedded font (MANIAC.EXE / ZAK.EXE) as an editable PNG atlas.</summary>
        private void ExportV2ExeFont()
        {
            string exePath = ScummV2ExeFontCodec.FindGameExe(Path.GetDirectoryName(scummFile.LoadedGameInfo.DataFile));
            if (exePath == null)
            {
                MessageBox.Show(this, "Could not find the game executable (MANIAC.EXE / ZAK.EXE) next to the data files.",
                    "Export game fonts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string error;
            ScummV2ExeFont font = ScummV2ExeFont.Read(File.ReadAllBytes(exePath), out error);
            if (font == null)
            {
                MessageBox.Show(this, "Could not read the font from " + Path.GetFileName(exePath) + ":\n" + error,
                    "Export game fonts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var dlg = new FolderBrowserDialog { Description = "Folder to save the EXE font (font.png + font.guide.png)" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string png = Path.Combine(dlg.SelectedPath, "font.png");
                ScummV2ExeFontCodec.ExportPng(font, png, Path.Combine(dlg.SelectedPath, "font.guide.png"));
                MessageBox.Show(this,
                    "The font from " + Path.GetFileName(exePath) + " was exported to:\n" + png
                    + "\n\nEdit the punctuation/symbol slots (e.g. $ % ' < = > [ \\ ] _ { | } ~) to hold accented "
                    + "letters, then use Import game fonts.\n\nNote: an EXE font edit shows only under the original "
                    + "DOS engine (DOSBox); ScummVM uses its own built-in font.",
                    "Export game fonts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Export game fonts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Imports an edited PNG atlas back into a copy of the v2 game executable (same size, in place).</summary>
        private void ImportV2ExeFont()
        {
            string exePath = ScummV2ExeFontCodec.FindGameExe(Path.GetDirectoryName(scummFile.LoadedGameInfo.DataFile));
            if (exePath == null)
            {
                MessageBox.Show(this, "Could not find the game executable (MANIAC.EXE / ZAK.EXE) next to the data files.",
                    "Import game fonts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var open = new OpenFileDialog { Filter = "Font atlas (font.png)|*.png|All files (*.*)|*.*", Title = "Select the edited font PNG" };
            if (open.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string error;
                ScummV2ExeFont font = ScummV2ExeFont.Read(File.ReadAllBytes(exePath), out error);
                if (font == null)
                {
                    MessageBox.Show(this, "Could not read the font from " + Path.GetFileName(exePath) + ":\n" + error,
                        "Import game fonts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string report = ScummV2ExeFontCodec.ImportPng(font, open.FileName);

                // Write the patched executable where the user chooses (defaults to the original, but a copy
                // is offered so the original EXE need not be overwritten).
                var save = new SaveFileDialog
                {
                    Filter = "Game executable (*.exe)|*.exe|All files (*.*)|*.*",
                    FileName = Path.GetFileName(exePath),
                    InitialDirectory = Path.GetDirectoryName(exePath),
                    Title = "Save the patched executable"
                };
                if (save.ShowDialog(this) != DialogResult.OK) return;

                File.WriteAllBytes(save.FileName, font.ExeBytes);
                MessageBox.Show(this,
                    report + Environment.NewLine + Environment.NewLine + "Patched executable written to:\n" + save.FileName
                    + "\n\nThe edited glyphs render under the original DOS engine (DOSBox), not ScummVM.",
                    "Import game fonts", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Import failed: " + ex.Message, "Import game fonts",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
