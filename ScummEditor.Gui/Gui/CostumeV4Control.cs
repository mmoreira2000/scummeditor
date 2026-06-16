using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Exceptions;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer/editor for a SCUMM v4 costume ("CO" block): a list of the costume's frames (CELs) on the
    /// left, the decoded frame on the right, with PNG export and import. It renders via
    /// CostumeImageDecoderV4 using the palette of the costume's room (PA for VGA, the EGA table for
    /// EGA). v4 costumes have a flat layout decoded by CostumeV4, not the v5/v6 Costume path.
    /// </summary>
    public class CostumeV4Control : BlockBaseControl
    {
        private readonly SplitContainer _split;
        private readonly TreeView _tree;
        private readonly Panel _scroll;
        private readonly PictureBox _picture;
        private readonly Button _exportButton;
        private readonly Button _importButton;
        private readonly CostumeImageDecoderV4 _decoder = new CostumeImageDecoderV4();
        private readonly CostumeImageEncoderV4 _encoder = new CostumeImageEncoderV4();

        private CostumeV4 _costume;
        private Color[] _palette;
        private int _currentFrameIndex = -1;
        private bool _splitterApplied;

        public CostumeV4Control()
        {
            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            _tree.AfterSelect += TreeAfterSelect;
            _split.Panel1.Controls.Add(_tree);

            // The picture fills the panel; a thin top bar holds the export/import buttons. The fill
            // control is added first so the top-docked bar takes its edge and the picture keeps the rest.
            _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.LightGray };
            _picture = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
            _scroll.Controls.Add(_picture);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 30 };
            _exportButton = new Button { Text = "Export PNG", Width = 90, Left = 3, Top = 3, Enabled = false };
            _exportButton.Click += ExportClick;
            _importButton = new Button { Text = "Import PNG", Width = 90, Left = 99, Top = 3, Enabled = false };
            _importButton.Click += ImportClick;
            topBar.Controls.Add(_exportButton);
            topBar.Controls.Add(_importButton);

            _split.Panel2.Controls.Add(_scroll);
            _split.Panel2.Controls.Add(topBar);

            Controls.Add(_split);
            _split.BringToFront();
        }

        // The base BlockBaseControl constructor sets the Size (raising OnSizeChanged) before this
        // constructor assigns _split, so guard against _split still being null on the first call.
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (_split != null && !_splitterApplied && _split.Width > 200)
            {
                _split.SplitterDistance = 150;
                _splitterApplied = true;
            }
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _costume = blockBase as CostumeV4;
            _tree.Nodes.Clear();
            _picture.Image = null;
            _currentFrameIndex = -1;
            _exportButton.Enabled = false;
            _importButton.Enabled = false;
            if (_costume == null) return;

            _palette = CostumeV4PaletteResolver.Resolve(_costume);

            for (int i = 0; i < _costume.Frames.Count; i++)
            {
                CostumeImageData frame = _costume.Frames[i];
                var node = _tree.Nodes.Add(string.Format("Frame {0} ({1}x{2})", i, frame.Width, frame.Height));
                node.Tag = i;
            }

            if (_tree.Nodes.Count > 0)
            {
                _tree.SelectedNode = _tree.Nodes[0];
            }
        }

        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_costume == null || e.Node == null || !(e.Node.Tag is int))
            {
                _picture.Image = null;
                _currentFrameIndex = -1;
                _exportButton.Enabled = false;
                _importButton.Enabled = false;
                return;
            }

            _currentFrameIndex = (int)e.Node.Tag;
            // Transparent background (costume colour 0) so the sprite stands out against the panel.
            _picture.Image = _decoder.Decode(_costume.Frames[_currentFrameIndex], _costume.PaletteSize, _palette, true);
            _exportButton.Enabled = true;
            _importButton.Enabled = true;
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_costume == null || _currentFrameIndex < 0 || _currentFrameIndex >= _costume.Frames.Count)
            {
                return;
            }

            using (var dialog = new SaveFileDialog
            {
                Filter = "PNG Files|*.png",
                FileName = string.Format("Costume FrameIndex#{0}.png", _currentFrameIndex)
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                // Export NON-transparent so colour 0 stays a real indexed entry: the pixel indexes ARE
                // the costume-local palette indexes, which keeps the frame round-trippable on import.
                using (Bitmap export = _decoder.Decode(_costume.Frames[_currentFrameIndex], _costume.PaletteSize, _palette, false))
                {
                    if (export != null) export.Save(dialog.FileName, ImageFormat.Png);
                }
            }
        }

        private void ImportClick(object sender, EventArgs e)
        {
            if (_costume == null || _currentFrameIndex < 0 || _currentFrameIndex >= _costume.Frames.Count)
            {
                return;
            }

            using (var dialog = new OpenFileDialog { Filter = "PNG Files|*.png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                int frameToKeep = _currentFrameIndex;
                try
                {
                    using (var imported = (Bitmap)Image.FromFile(dialog.FileName))
                    {
                        CostumeImageData frame = _costume.Frames[_currentFrameIndex];
                        byte[] rle = _encoder.Encode(imported, _costume.PaletteSize, frame.Width, frame.Height);
                        _costume.ReplaceFrameImage(_currentFrameIndex, rle);
                    }
                }
                catch (ImageEncodeException ex)
                {
                    MessageBox.Show(ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // ReplaceFrameImage re-parsed the costume; rebuild the frame list and re-render.
                SetAndRefreshData(_costume);
                if (frameToKeep < _tree.Nodes.Count)
                {
                    _tree.SelectedNode = _tree.Nodes[frameToKeep];
                }

                MessageBox.Show("Costume frame imported. Use \"Save changes\" to write it back to the game files.",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
