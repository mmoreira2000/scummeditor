using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;

namespace ScummEditor.Gui
{
    public partial class ImportResources : Form
    {
        private ScummGameData _scummFile;

        public void ShowDialog(ScummGameData scummFile, Form form)
        {
            _scummFile = scummFile;
            ShowDialog(form);
        }

        public ImportResources()
        {
            InitializeComponent();
        }

        private void SelectFolder_Click(object sender, EventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            dlg.SelectedPath = ImportLocation.Text;

            DialogResult resp = dlg.ShowDialog();
            if (resp == DialogResult.Cancel) return;

            ImportLocation.Text = dlg.SelectedPath;
        }

        private void OK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ImportLocation.Text)) return;
            if (!Directory.Exists(ImportLocation.Text)) return;

            string location = ImportLocation.Text;

            Cursor = Cursors.WaitCursor;
            Cancel.Cursor = Cursors.Default;

            foreach (Control control in Controls)
            {
                if (control.Name != "Cancel" && control.GetType() != typeof(Label) && control.GetType() != typeof(ProgressBar))
                {
                    control.Enabled = false;
                }
            }

            Application.DoEvents();

            // The actual import loops live in the engine (ScummV4GraphicsBatch / ScummV5V6GraphicsBatch);
            // this dialog only picks the folder and drives the progress bar. v3 "GF_OLD256" rooms reuse
            // the v4 block layout, so they take the v4 path.
            int version = _scummFile.LoadedGameInfo != null ? _scummFile.LoadedGameInfo.ScummVersion : 0;
            bool oldBundle = _scummFile.LoadedGameInfo != null && _scummFile.LoadedGameInfo.UsesOldBundle;
            if (version == 1)
            {
                ImportV1(location); // Maniac Mansion / Zak McKracken classic (GdiV1 tilemap - NOT the v2 codec)
            }
            else if (version == 2)
            {
                ImportV2(location); // Maniac Mansion, Zak McKracken (GdiV2 raw-room games)
            }
            else if (version == 3 && oldBundle)
            {
                ImportV3Old(location); // raw-room EGA games (Loom EGA, Indy3 EGA)
            }
            else if (version == 3 || version == 4)
            {
                ImportV4(location);
            }
            else if (version == 8)
            {
                ImportV8(location); // The Curse of Monkey Island (IMAG/WRAP/OFFS images, two data files)
            }
            else
            {
                ImportV5V6(location);
            }
        }

        /// <summary>Batch-imports edited backgrounds + object images into a v8 game (both COMI data files).</summary>
        private void ImportV8(string location)
        {
            ShowProgress(location);
            ScummV4GraphicsBatch.ImportReport report = ScummV8GraphicsBatch.Import(_scummFile, location, OnImportProgress);
            ShowImportResult(report.Imported, report.Found, report.Errors);
        }

        /// <summary>Batch-imports edited GdiV1 tilemap images, z-planes and 0x57 costume frames into a v1 game.</summary>
        private void ImportV1(string location)
        {
            ShowProgress(location);
            ScummV4GraphicsBatch.ImportReport report = ScummV1Graphics.Import(_scummFile, location, OnImportProgress);
            ShowImportResult(report.Imported, report.Found, report.Errors);
        }

        /// <summary>Batch-imports edited EGA backgrounds, object images, z-planes and costume frames into a v2 game.</summary>
        private void ImportV2(string location)
        {
            ShowProgress(location);
            ScummV4GraphicsBatch.ImportReport report = ScummV2Graphics.Import(_scummFile, location, OnImportProgress);
            ShowImportResult(report.Imported, report.Found, report.Errors);
        }

        /// <summary>Batch-imports edited EGA backgrounds + object images into a v3 old-bundle game.</summary>
        private void ImportV3Old(string location)
        {
            ShowProgress(location);
            ScummV4GraphicsBatch.ImportReport report = ScummV3OldGraphics.Import(_scummFile, location, OnImportProgress);
            ShowImportResult(report.Imported, report.Found, report.Errors);
        }

        /// <summary>Batch-imports every PNG back into a v4 game (backgrounds, objects, z-planes, costume frames).</summary>
        private void ImportV4(string location)
        {
            ShowProgress(location);
            ScummV4GraphicsBatch.ImportReport report = ScummV4GraphicsBatch.Import(_scummFile, location, OnImportProgress);
            ShowImportResult(report.Imported, report.Found, report.Errors);
        }

        /// <summary>Batch-imports every PNG back into a v5/v6 game (backgrounds, objects, z-planes, costume frames).</summary>
        private void ImportV5V6(string location)
        {
            ShowProgress(location);
            ScummV5V6GraphicsBatch.ImportReport report = ScummV5V6GraphicsBatch.Import(_scummFile.DataFile, location, OnImportProgress);
            ShowImportResult(report.Imported, report.Found, report.Errors);
        }

        private void ShowProgress(string location)
        {
            int pngCount = Directory.GetFiles(location, "*.png").Length;
            Progress.Maximum = Math.Max(1, pngCount);
            Progress.Value = 0;
            Progress.Visible = true;
            FilesFound.Text = pngCount.ToString();
        }

        private void OnImportProgress(int done, int total)
        {
            Progress.Value = Math.Min(done, Progress.Maximum);
            FilesImported.Text = done.ToString();
            Application.DoEvents();
        }

        private void ShowImportResult(int imported, int found, List<string> errors)
        {
            string message = string.Format("{0} of {1} images imported.", imported, found);
            if (errors.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine + "Issues:" + Environment.NewLine
                    + string.Join(Environment.NewLine, errors.Take(15));
                if (errors.Count > 15)
                {
                    message += Environment.NewLine + string.Format("... and {0} more.", errors.Count - 15);
                }
            }
            MessageBox.Show(message, "Import",
                MessageBoxButtons.OK, errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
