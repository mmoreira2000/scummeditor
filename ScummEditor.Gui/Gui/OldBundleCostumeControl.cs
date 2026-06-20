using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for a v2 / v3-old costume (an OldBundleBlock of Kind=Costume): lists the costume's frames and
    /// shows the selected one, decoded with the classic CEL codec (format 0x58, shared by v2 and v3old) and
    /// the fixed 16-colour EGA palette. The PNG button exports the current frame (per-frame import is on the
    /// batch graphics menu).
    /// </summary>
    public class OldBundleCostumeControl : UserControl
    {
        private readonly Label _header;
        private readonly Button _exportButton;
        private readonly Button _importButton;
        private readonly ListBox _frameList;
        private readonly PictureBox _picture;
        private readonly SplitContainer _split;
        private readonly Color[] _ega;
        private bool _splitterApplied;

        private OldBundleBlock _block;
        private CostumeV3Old _costume;

        public OldBundleCostumeControl()
        {
            _ega = new Color[16];
            Array.Copy(EgaColorTable.Colors256, _ega, 16);

            _header = new Label { Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft };

            var topBar = new Panel { Dock = DockStyle.Top, Height = 30 };
            _exportButton = new Button { Text = "Export PNG", Width = 90, Left = 3, Top = 3, Enabled = false };
            _exportButton.Click += ExportClick;
            _importButton = new Button { Text = "Import PNG", Width = 90, Left = 99, Top = 3, Enabled = false };
            _importButton.Click += ImportClick;
            topBar.Controls.Add(_exportButton);
            topBar.Controls.Add(_importButton);

            // SplitterDistance is applied later in OnSizeChanged (not here): setting it at construction, when
            // the control still has its tiny default width, can clamp to the wrong position.
            _split = new SplitContainer { Dock = DockStyle.Fill };
            _frameList = new ListBox { Dock = DockStyle.Fill };
            _frameList.SelectedIndexChanged += (s, e) => ShowSelectedFrame();
            _split.Panel1.Controls.Add(_frameList);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.DimGray };
            _picture = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
            scroll.Controls.Add(_picture);
            _split.Panel2.Controls.Add(scroll);

            Controls.Add(_split);
            Controls.Add(topBar);
            Controls.Add(_header);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (!_splitterApplied && _split != null && _split.Width > 200)
            {
                _split.SplitterDistance = 90; // frame list on the left, preview on the right
                _splitterApplied = true;
            }
        }

        public void SetData(OldBundleBlock block)
        {
            _block = block;
            _frameList.Items.Clear();
            if (_picture.Image != null) { _picture.Image.Dispose(); _picture.Image = null; }
            _exportButton.Enabled = false;
            _importButton.Enabled = false;
            _costume = null;

            if (block == null) { _header.Text = "(no costume)"; return; }

            try { _costume = new CostumeV3Old(block.DataFile.RawContent, block.Offset); }
            catch (Exception ex) { _header.Text = "Costume " + block.ResourceIndex + " - parse failed: " + ex.Message; return; }

            _header.Text = string.Format("Costume {0} (room {1})   ·   {2} frame(s)", block.ResourceIndex, block.RoomNo, _costume.Frames.Count);
            for (int k = 0; k < _costume.Frames.Count; k++) _frameList.Items.Add("Frame " + k);
            if (_frameList.Items.Count > 0) _frameList.SelectedIndex = 0;
        }

        private void ShowSelectedFrame()
        {
            if (_picture.Image != null) { _picture.Image.Dispose(); _picture.Image = null; }
            _exportButton.Enabled = false;
            _importButton.Enabled = false;
            if (_costume == null || _frameList.SelectedIndex < 0 || _frameList.SelectedIndex >= _costume.Frames.Count) return;

            try
            {
                Bitmap frame = new CostumeImageDecoderV4().Decode(_costume.Frames[_frameList.SelectedIndex], 16, _ega, false);
                _picture.Image = frame;
                _exportButton.Enabled = frame != null;
                _importButton.Enabled = frame != null;
                _header.Text = frame == null
                    ? string.Format("Costume {0} (room {1})   ·   frame {2} - could not decode", _block.ResourceIndex, _block.RoomNo, _frameList.SelectedIndex)
                    : string.Format("Costume {0} (room {1})   ·   frame {2} of {3}   ·   {4} x {5}",
                        _block.ResourceIndex, _block.RoomNo, _frameList.SelectedIndex, _costume.Frames.Count, frame.Width, frame.Height);
            }
            catch (Exception ex)
            {
                _header.Text = "Frame decode failed: " + ex.Message;
            }
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_picture.Image == null || _block == null) return;
            using (var dlg = new SaveFileDialog
            {
                Filter = "PNG image (*.png)|*.png",
                FileName = string.Format("Costume#{0} FrameIndex#{1}.png", _block.ResourceIndex, _frameList.SelectedIndex)
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { _picture.Image.Save(dlg.FileName, ImageFormat.Png); }
                catch (Exception ex) { MessageBox.Show(this, "Export failed: " + ex.Message, "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void ImportClick(object sender, EventArgs e)
        {
            if (_block == null || _costume == null || _frameList.SelectedIndex < 0) return;
            int frameIndex = _frameList.SelectedIndex;
            using (var dlg = new OpenFileDialog { Filter = "PNG image (*.png)|*.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string error;
                bool ok;
                try
                {
                    using (var png = (Bitmap)Image.FromFile(dlg.FileName))
                    {
                        ok = OldBundleCostumeImporter.ImportFrame(_block.DataFile, _block.Index, _block.RoomNo,
                            _block.IsV2, _block.Offset, frameIndex, png, out error);
                    }
                }
                catch (Exception ex) { ok = false; error = ex.Message; }

                if (!ok)
                {
                    MessageBox.Show(this, error, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                SetData(_block); // re-parse the costume from the updated bytes
                if (frameIndex < _frameList.Items.Count) _frameList.SelectedIndex = frameIndex; // keep the frame selected
                MessageBox.Show(this, "Frame imported. Use \"Save changes\" to write it back to the game files.",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
