using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for a v2 / v3-old room background, object image or walk-behind z-plane (an OldBundleBlock of
    /// Kind=Image). Decodes the selected image with the existing v2/v3old decoders and shows it; the PNG
    /// button exports it (per-node import is on the "Import Game Graphics" batch menu).
    /// </summary>
    public class OldBundleImageControl : UserControl
    {
        private readonly Label _header;
        private readonly Button _exportButton;
        private readonly PictureBox _picture;
        private OldBundleBlock _block;

        public OldBundleImageControl()
        {
            _header = new Label { Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft };

            var topBar = new Panel { Dock = DockStyle.Top, Height = 30 };
            _exportButton = new Button { Text = "Export PNG", Width = 90, Left = 3, Top = 3, Enabled = false };
            _exportButton.Click += ExportClick;
            topBar.Controls.Add(_exportButton);

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

            if (block == null) { _header.Text = "(no image)"; return; }
            string what = KindLabel(block);

            Bitmap image;
            try { image = Decode(block); }
            catch (Exception ex) { _header.Text = what + " - decode failed: " + ex.Message; return; }

            _picture.Image = image;
            _exportButton.Enabled = image != null;
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
            if (b.IsV2)
            {
                var room = new ScummV2Room(raw);
                var dec = new ScummV2ImageDecoder();
                switch (b.ImageKind)
                {
                    case OldBundleImageKind.Background: return dec.DecodeBackground(room);
                    case OldBundleImageKind.Object: return dec.DecodeObject(room, b.ObjectIndex);
                    case OldBundleImageKind.BackgroundZPlane: return dec.DecodeBackgroundZPlane(room);
                    default: return null; // v2 has no per-object z-plane
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
