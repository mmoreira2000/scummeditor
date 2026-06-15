using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Exceptions;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class ImportResources : Form
    {
        private bool _cancelImport;
        private bool _importing;
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

            _cancelImport = false;
            _importing = true;
            foreach (Control control in Controls)
            {
                if (control.Name != "Cancel" && control.GetType() != typeof(Label) && control.GetType() != typeof(ProgressBar))
                {
                    control.Enabled = false;
                }
            }


            Application.DoEvents();

            // SCUMM v4 has no LFLF blocks (its rooms span several LE/FO/LF DISKnn.LEC containers), so
            // it uses a dedicated batch path instead of the v5/v6 walk below.
            if (_scummFile.LoadedGameInfo != null && _scummFile.LoadedGameInfo.ScummVersion == 4)
            {
                ImportV4(location);
                return;
            }

            List<ImageInfo> files = Directory.GetFiles(location, "*.png").Select(f => new ImageInfo(f)).ToList();

            List<DiskBlock> diskBlocks = _scummFile.DataFile.GetLFLFs();

            Progress.Maximum = files.Count - 1;
            Progress.Value = 0;
            Progress.Visible = true;

            FilesFound.Text = files.Count.ToString();

            var encoder = new ImageEncoder();
            var bompEncoder = new BompImageEncoder();
            var costumeEncoder = new CostumeImageEncoder();
            var zplaneEncoder = new ZPlaneEncoder();


            for (int i = 0; i < files.Count; i++)
            {
                ImageInfo currentFile = files[i];
                RoomBlock currentRoomBlock = diskBlocks[currentFile.RoomIndex].GetROOM();
                Bitmap bitmapToEncode = (Bitmap)Bitmap.FromFile(currentFile.Filename);

                try
                {
                    switch (currentFile.ImageType)
                    {
                        case ImageType.Background:
                            {
                                encoder.Encode(currentRoomBlock, bitmapToEncode);
                            }
                            break;
                        case ImageType.ZPlane:
                            {
                                zplaneEncoder.Encode(currentRoomBlock, bitmapToEncode, currentFile.ZPlaneIndex);
                            }
                            break;
                        case ImageType.Object:
                            if (currentRoomBlock.GetOBIMs()[currentFile.ObjectIndex].GetIMxx()[currentFile.ImageIndex].GetSMAP() == null)
                            {
                                bompEncoder.Encode(currentRoomBlock, currentFile.ObjectIndex, currentFile.ImageIndex, bitmapToEncode);
                            }
                            else
                            {
                                encoder.Encode(currentRoomBlock, currentFile.ObjectIndex, currentFile.ImageIndex, bitmapToEncode);
                            }
                            break;
                        case ImageType.ObjectsZPlane:
                            {
                                zplaneEncoder.Encode(currentRoomBlock, currentFile.ObjectIndex, currentFile.ImageIndex, bitmapToEncode, currentFile.ZPlaneIndex);
                            }
                            break;
                        case ImageType.Costume:
                            {
                                Costume costume = diskBlocks[currentFile.RoomIndex].GetCostumes()[currentFile.CostumeIndex];
                                costumeEncoder.Encode(currentRoomBlock, costume, currentFile.FrameIndex, bitmapToEncode);
                            }
                            break;
                    }

                }
                catch (ImageEncodeException ex)
                {
                    MessageBox.Show(ex.Message, "Error importing", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                FilesImported.Text = i.ToString();
                Progress.Value = i;
                Application.DoEvents();
            }

            MessageBox.Show("All images imported");

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>Batch-imports every PNG back into a v4 game (backgrounds, objects, z-planes, costume frames).</summary>
        private void ImportV4(string location)
        {
            int pngCount = Directory.GetFiles(location, "*.png").Length;
            Progress.Maximum = Math.Max(1, pngCount);
            Progress.Value = 0;
            Progress.Visible = true;
            FilesFound.Text = pngCount.ToString();

            ScummV4GraphicsBatch.ImportReport report = ScummV4GraphicsBatch.Import(_scummFile, location, (done, total) =>
            {
                Progress.Value = Math.Min(done, Progress.Maximum);
                FilesImported.Text = done.ToString();
                Application.DoEvents();
            });

            string message = string.Format("{0} of {1} images imported.", report.Imported, report.Found);
            if (report.Errors.Count > 0)
            {
                message += Environment.NewLine + Environment.NewLine + "Issues:" + Environment.NewLine
                    + string.Join(Environment.NewLine, report.Errors.Take(15));
                if (report.Errors.Count > 15)
                {
                    message += Environment.NewLine + string.Format("... and {0} more.", report.Errors.Count - 15);
                }
            }
            MessageBox.Show(message, "Import",
                MessageBoxButtons.OK, report.Errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

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
