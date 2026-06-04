using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class ExportResources : Form
    {
        private ScummV6GameData _scummFile;

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
            int fileCount = 0;

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

            var diskBlocks = _scummFile.DataFile.GetLFLFs();

            Progress.Maximum = diskBlocks.Count - 1;
            Progress.Value = 0;
            Progress.Visible = true;

            FilesExported.Visible = true;
            FilesExportedLabel.Visible = true;

            // Backgrounds, objects and costumes are always exported as true 8bpp indexed images
            // whose pixel bytes ARE the original palette indexes. This is the only supported format
            // because it round-trips losslessly: the index is preserved even when several palette
            // entries share the same color, so the image still renders correctly under any
            // alternate palette. (Editing must keep the image indexed; do not convert it to RGB.)
            var decoder = new ImageDecoder();
            decoder.UseTransparentColor = ExportWithTransparency.Checked;

            var bompDecoder = new BompImageDecoder();
            bompDecoder.UseTransparentColor = ExportWithTransparency.Checked;

            var costumeDecoder = new CostumeImageDecoder();
            costumeDecoder.UseTransparentColor = ExportWithTransparency.Checked;

            var zplaneDecoder = new ZPlaneDecoder();

            // Z-planes are binary masks (no palette), exported as 1bpp black/white images.
            var convert = new ImageDepthConversor();


            for (int i = 0; i < diskBlocks.Count; i++)
            {
                if (_cancelExport) break;

                var currentRoom = (RoomBlock)diskBlocks[i].Childrens.Single(r => r.GetType() == typeof(RoomBlock));

                if (ExportBackgrounds.Checked)
                {
                    Bitmap background = decoder.Decode(currentRoom);
                    if (background != null)
                    {
                        string backgroundName = Path.Combine(location, string.Format("Room#{0}.png", i));
                        background.Save(backgroundName, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    FilesExported.Text = (++fileCount).ToString();
                }

                if (ExportBackgroundZPlanes.Checked)
                {
                    List<ZPlane> zPlanes = currentRoom.GetRMIM().GetIM00().GetZPlanes();

                    for (int j = 0; j < zPlanes.Count; j++)
                    {
                        if (_cancelExport) break;

                        Bitmap zplane = zplaneDecoder.Decode(currentRoom, j);
                        if (zplane != null)
                        {
                            zplane = convert.CopyToBpp(zplane, 1, new Color[2] { Color.Black, Color.White });
                            zplane.Save(Path.Combine(location, string.Format("Room#{0} ZP#{1}.png", i, j)), System.Drawing.Imaging.ImageFormat.Png);
                            FilesExported.Text = (++fileCount).ToString();
                        }
                    }
                }


                if (ExportObjects.Checked)
                {
                    var OBIMs = currentRoom.GetOBIMs();
                    for (int j = 0; j < OBIMs.Count; j++)
                    {
                        ObjectImage objectImage = OBIMs[j];
                        List<ImageData> IMxx = objectImage.GetIMxx();

                        for (int k = 0; k < IMxx.Count; k++)
                        {
                            if (_cancelExport) break;

                            Bitmap image;

                            if (IMxx[k].GetSMAP() == null)
                            {
                                image = bompDecoder.Decode(currentRoom, j, k);
                            }
                            else
                            {
                                image = decoder.Decode(currentRoom, j, k);
                            }

                            string objectFilename = Path.Combine(location, string.Format("Room#{0} Obj#{1} Img#{2}.png", i, j, k));
                            image.Save(objectFilename, System.Drawing.Imaging.ImageFormat.Png);

                            FilesExported.Text = (++fileCount).ToString();
                        }
                    }
                }

                if (ExportObjectsZPlanes.Checked)
                {
                    var OBIMs = currentRoom.GetOBIMs();
                    for (int j = 0; j < OBIMs.Count; j++)
                    {
                        ObjectImage objectImage = OBIMs[j];
                        List<ImageData> IMxx = objectImage.GetIMxx();

                        for (int k = 0; k < IMxx.Count; k++)
                        {
                            List<ZPlane> zplanes = IMxx[k].GetZPlanes();
                            for (int l = 0; l < zplanes.Count; l++)
                            {
                                if (_cancelExport) break;

                                Bitmap zplane = zplaneDecoder.Decode(currentRoom, j, k, l);

                                zplane = convert.CopyToBpp(zplane, 1, new Color[2] { Color.Black, Color.White });
                                zplane.Save(Path.Combine(location, string.Format("Room#{0} Obj#{1} Img#{2} ZP#{3}.png", i, j, k, l)), System.Drawing.Imaging.ImageFormat.Png);

                                FilesExported.Text = (++fileCount).ToString();
                            }
                        }
                    }
                }

                if (ExportCostumes.Checked)
                {
                    DiskBlock currentDisk = diskBlocks[i];

                    List<Costume> costumesList = currentDisk.Childrens.OfType<Costume>().ToList();
                    for (int j = 0; j < costumesList.Count; j++)
                    {
                        Costume costume = costumesList[j];
                        for (int k = 0; k < costume.Pictures.Count; k++)
                        {
                            if (_cancelExport) break;

                            if (costume.Pictures[k].ImageData.Length == 0
                                || costume.Pictures[k].ImageData.Length == 1 && costume.Pictures[k].ImageData[0] == 0) continue;

                            Bitmap image = costumeDecoder.Decode(currentRoom, costume, k);

                            image.Save(Path.Combine(location, string.Format("Room#{0} Costume#{1} FrameIndex#{2}.png", i, j, k)), System.Drawing.Imaging.ImageFormat.Png);
                            FilesExported.Text = (++fileCount).ToString();
                        }
                    }
                }

                Progress.Value = i;
                Application.DoEvents();
            }

            Progress.Visible = false;
            _exporting = false;
            Cursor = Cursors.Default;

            if (_cancelExport)
            {
                MessageBox.Show("Export cancelled.");

            }
            else
            {
                MessageBox.Show("All images sucefully exported");
            }
            Close();
        }

        public void ShowDialog(ScummV6GameData scummFile, Form form)
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
