using System;
using System.IO;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;

namespace ScummEditor.Gui
{
    public partial class ExportResources : Form
    {
        private ScummGameData _scummFile;

        private bool _cancelExport;
        private bool _exporting;

        public ExportResources()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ExportLocation.Text)) return;
            if (!Directory.Exists(ExportLocation.Text)) return;

            string location = ExportLocation.Text;

            Cursor = Cursors.WaitCursor;
            Cancel.Cursor = Cursors.Default;

            _cancelExport = false;
            _exporting = true;
            foreach (Control control in Controls)
            {
                if (control.Name != "Cancel" && control.GetType() != typeof(Label) && control.GetType() != typeof(ProgressBar))
                {
                    control.Enabled = false;
                }
            }

            Application.DoEvents();

            // The actual export loops live in the engine (ScummV4GraphicsBatch / ScummV5V6GraphicsBatch).
            // This dialog only collects the options + folder and drives the progress bar. v4 spreads its
            // rooms over several DISKnn.LEC disks; v5/v6 keep everything in one LFLF data file. v3
            // "GF_OLD256" rooms reuse the v4 block layout (one NN.LFL per room), so they take the v4 path.
            int version = _scummFile.LoadedGameInfo != null ? _scummFile.LoadedGameInfo.ScummVersion : 0;
            if (version == 3 || version == 4)
            {
                ExportV4(location);
            }
            else
            {
                ExportV5V6(location);
            }
        }

        /// <summary>Batch-exports every v4 image (backgrounds, objects, z-planes, costume frames).</summary>
        private void ExportV4(string location)
        {
            int roomCount = ScummV4GraphicsBatch.EnumerateRooms(_scummFile).Count;
            Progress.Maximum = Math.Max(1, roomCount);
            ShowProgress();

            int exported = ScummV4GraphicsBatch.Export(_scummFile, location, BuildV4Options(), OnExportProgress, () => _cancelExport);
            FinishExport(exported);
        }

        /// <summary>Batch-exports every v5/v6 image (backgrounds, objects, z-planes, costume frames).</summary>
        private void ExportV5V6(string location)
        {
            Progress.Maximum = Math.Max(1, _scummFile.DataFile.GetLFLFs().Count);
            ShowProgress();

            int exported = ScummV5V6GraphicsBatch.Export(_scummFile.DataFile, location, BuildV5V6Options(), OnExportProgress, () => _cancelExport);
            FinishExport(exported);
        }

        private ScummV4GraphicsBatch.ExportOptions BuildV4Options()
        {
            return new ScummV4GraphicsBatch.ExportOptions
            {
                Backgrounds = ExportBackgrounds.Checked,
                Objects = ExportObjects.Checked,
                Costumes = ExportCostumes.Checked,
                BackgroundZPlanes = ExportBackgroundZPlanes.Checked,
                ObjectZPlanes = ExportObjectsZPlanes.Checked,
                Transparency = ExportWithTransparency.Checked
            };
        }

        private ScummV5V6GraphicsBatch.ExportOptions BuildV5V6Options()
        {
            return new ScummV5V6GraphicsBatch.ExportOptions
            {
                Backgrounds = ExportBackgrounds.Checked,
                Objects = ExportObjects.Checked,
                Costumes = ExportCostumes.Checked,
                BackgroundZPlanes = ExportBackgroundZPlanes.Checked,
                ObjectZPlanes = ExportObjectsZPlanes.Checked,
                Transparency = ExportWithTransparency.Checked
            };
        }

        private void ShowProgress()
        {
            Progress.Value = 0;
            Progress.Visible = true;
            FilesExported.Visible = true;
            FilesExportedLabel.Visible = true;
        }

        private void OnExportProgress(int done, int total)
        {
            Progress.Value = Math.Min(done, Progress.Maximum);
            Application.DoEvents();
        }

        private void FinishExport(int exported)
        {
            FilesExported.Text = exported.ToString();
            Progress.Visible = false;
            _exporting = false;
            Cursor = Cursors.Default;
            if (_cancelExport)
            {
                MessageBox.Show("Export cancelled.");
            }
            else
            {
                MessageBox.Show(string.Format("{0} images successfully exported.", exported));
            }
            Close();
        }

        public void ShowDialog(ScummGameData scummFile, Form form)
        {
            _scummFile = scummFile;
            ShowDialog(form);
        }

        private void SelectFolder_Click(object sender, EventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            dlg.SelectedPath = ExportLocation.Text;

            DialogResult resp = dlg.ShowDialog();
            if (resp == DialogResult.Cancel) return;

            ExportLocation.Text = dlg.SelectedPath;
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            if (_exporting)
            {
                _cancelExport = true;
            }
            else
            {
                Close();
            }
        }
    }
}
