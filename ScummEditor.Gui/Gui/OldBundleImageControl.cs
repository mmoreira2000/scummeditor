using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for a v2 / v3-old room background, object image or walk-behind z-plane (an OldBundleBlock of
    /// Kind=Image). Decodes the selected image with the existing v2/v3old decoders and shows it; the buttons
    /// export the shown image to PNG and import an edited PNG back into the game (OldBundleImageImporter,
    /// write-back applied in memory until "Save changes"). The batch "Import Game Graphics" menu is the
    /// alternative bulk route.
    /// </summary>
    public class OldBundleImageControl : UserControl
    {
        private readonly Label _header;
        private readonly Button _exportButton;
        private readonly Button _importButton;
        private readonly PictureBox _picture;
        private OldBundleBlock _block;

        public OldBundleImageControl()
        {
            _header = new Label { Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft };

            var topBar = new Panel { Dock = DockStyle.Top, Height = 30 };
            _exportButton = new Button { Text = "Export PNG", Width = 90, Left = 3, Top = 3, Enabled = false };
            _exportButton.Click += ExportClick;
            _importButton = new Button { Text = "Import PNG", Width = 90, Left = 99, Top = 3, Enabled = false };
            _importButton.Click += ImportClick;
            topBar.Controls.Add(_exportButton);
            topBar.Controls.Add(_importButton);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.DimGray };
            _picture = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
            scroll.Controls.Add(_picture);

            Controls.Add(scroll);
            Controls.Add(topBar);
            Controls.Add(_header);
        }

        public void SetData(OldBundleBlock block)
        {
            _block = block;
            if (_picture.Image != null) { _picture.Image.Dispose(); _picture.Image = null; }
            _exportButton.Enabled = false;
            _importButton.Enabled = false;

            if (block == null) { _header.Text = "(no image)"; return; }
            string what = KindLabel(block);

            Bitmap image;
            try { image = Decode(block); }
            catch (Exception ex) { _header.Text = what + " - decode failed: " + ex.Message; return; }

            _picture.Image = image;
            _exportButton.Enabled = image != null;
            // v1 (GdiV1 tilemap) now re-encodes all four kinds (background, object image, and the background /
            // object walk-behind masks), each preserving the shared charMap / maskChar so other images keep
            // decoding. Whatever decodes can be imported back.
            _importButton.Enabled = image != null;
            _header.Text = image == null
                ? what + " - could not decode"
                : string.Format("{0}   ·   {1} x {2}", what, image.Width, image.Height);
        }

        private static string KindLabel(OldBundleBlock b)
        {
            switch (b.ImageKind)
            {
                case OldBundleImageKind.Background: return "Room " + b.RoomNo + " background";
                case OldBundleImageKind.BackgroundZPlane: return "Room " + b.RoomNo + " walk-behind (z-plane)";
                case OldBundleImageKind.Object: return "Room " + b.RoomNo + " object " + b.ObjectIndex;
                case OldBundleImageKind.ObjectZPlane: return "Room " + b.RoomNo + " object " + b.ObjectIndex + " z-plane";
                default: return "Room " + b.RoomNo;
            }
        }

        private static Bitmap Decode(OldBundleBlock b)
        {
            byte[] raw = b.DataFile.RawContent;
            if (b.GameInfo != null && b.GameInfo.ScummVersion == 1)
            {
                // v1 (Maniac/Zak classic) uses the GdiV1 tilemap codec, not the v2 vertical RLE.
                var room = new ScummV1Room(raw);
                var dec = new ScummV1ImageDecoder(b.GameInfo.LoadedGame == ScummGame.ManiacMansion);
                switch (b.ImageKind)
                {
                    case OldBundleImageKind.Background: return dec.DecodeBackground(room);
                    case OldBundleImageKind.Object: return dec.DecodeObject(room, b.ObjectIndex);
                    case OldBundleImageKind.BackgroundZPlane: return dec.DecodeBackgroundZPlane(room);
                    case OldBundleImageKind.ObjectZPlane: return dec.DecodeObjectZPlane(room, b.ObjectIndex);
                    default: return null;
                }
            }
            if (b.IsV2)
            {
                var room = new ScummV2Room(raw);
                var dec = new ScummV2ImageDecoder();
                switch (b.ImageKind)
                {
                    case OldBundleImageKind.Background: return dec.DecodeBackground(room);
                    case OldBundleImageKind.Object: return dec.DecodeObject(room, b.ObjectIndex);
                    case OldBundleImageKind.BackgroundZPlane: return dec.DecodeBackgroundZPlane(room);
                    case OldBundleImageKind.ObjectZPlane: return dec.DecodeObjectZPlane(room, b.ObjectIndex);
                    default: return null;
                }
            }
            else
            {
                var room = new ScummV3OldRoom(raw);
                var dec = new ScummV3OldImageDecoder();
                switch (b.ImageKind)
                {
                    case OldBundleImageKind.Background: return dec.DecodeBackground(room);
                    case OldBundleImageKind.Object: return dec.DecodeObject(room, b.ObjectIndex);
                    case OldBundleImageKind.BackgroundZPlane: return dec.DecodeBackgroundZPlane(room);
                    case OldBundleImageKind.ObjectZPlane: return dec.DecodeObjectZPlane(room, b.ObjectIndex);
                    default: return null;
                }
            }
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_picture.Image == null || _block == null) return;
            using (var dlg = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = DefaultFileName(_block) })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { _picture.Image.Save(dlg.FileName, ImageFormat.Png); }
                catch (Exception ex) { MessageBox.Show(this, "Export failed: " + ex.Message, "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void ImportClick(object sender, EventArgs e)
        {
            if (_block == null) return;
            using (var dlg = new OpenFileDialog { Filter = "PNG image (*.png)|*.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string error;
                bool ok;
                try
                {
                    using (var png = (Bitmap)Image.FromFile(dlg.FileName))
                    {
                        ok = OldBundleImageImporter.Import(_block.DataFile, _block.Index, _block.RoomNo,
                            _block.IsV2, _block.ImageKind, _block.ObjectIndex, png, out error);
                    }
                }
                catch (Exception ex) { ok = false; error = ex.Message; }

                if (!ok)
                {
                    MessageBox.Show(this, error, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SetData(_block); // re-decode so the preview shows the imported image
                MessageBox.Show(this, "Image imported. Use \"Save changes\" to write it back to the game files.",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static string DefaultFileName(OldBundleBlock b)
        {
            switch (b.ImageKind)
            {
                case OldBundleImageKind.Background: return string.Format("Room#{0}.png", b.RoomNo);
                case OldBundleImageKind.BackgroundZPlane: return string.Format("Room#{0} ZP.png", b.RoomNo);
                case OldBundleImageKind.Object: return string.Format("Room#{0} Obj#{1}.png", b.RoomNo, b.ObjectIndex);
                case OldBundleImageKind.ObjectZPlane: return string.Format("Room#{0} Obj#{1} ZP.png", b.RoomNo, b.ObjectIndex);
                default: return "image.png";
            }
        }
    }
}
