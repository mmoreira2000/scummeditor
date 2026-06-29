using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Image viewer for a SCUMM v8 room (ROOM block): a tree of the room background, its object images and
    /// every z-plane (mask) on the left, the decoded picture on the right, with per-image PNG export/import.
    /// v8 images use the IMAG/WRAP/OFFS/SMAP|BOMP/ZPLN nesting, so this uses ScummV8ImageDecoder/Encoder
    /// rather than the v5/v6 DiskBlockControl path. Objects can be SMAP- or BOMP-coded; both are shown.
    /// </summary>
    public class ScummV8RoomImageControl : BlockBaseControl
    {
        private readonly SplitContainer _split;
        private readonly TreeView _tree;
        private readonly Panel _scroll;
        private readonly PictureBox _picture;
        private readonly Button _exportButton;
        private readonly Button _importButton;
        private readonly ScummV8ImageDecoder _decoder = new ScummV8ImageDecoder();
        private readonly ScummV8ImageEncoder _encoder = new ScummV8ImageEncoder();

        private RoomBlock _room;
        private ImageTarget _currentTarget;
        private Bitmap _currentImage;
        private bool _splitterApplied;

        /// <summary>What a tree node renders: the background, one object image (by OBIM index), or a z-plane
        /// (mask) of either, identified by its z-plane index within the parent image.</summary>
        private class ImageTarget
        {
            public bool IsBackground { get; set; }
            public int ObjectIndex { get; set; }   // -1 for the background
            public bool IsZPlane { get; set; }
            public int ZPlaneIndex { get; set; }
        }

        public ScummV8RoomImageControl()
        {
            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            _tree.AfterSelect += TreeAfterSelect;
            _split.Panel1.Controls.Add(_tree);

            _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
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

        // The splitter distance can only be set once the control is wide enough; the base constructor sets
        // Size (raising OnSizeChanged) before _split is assigned, so guard against null.
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (_split != null && !_splitterApplied && _split.Width > 200)
            {
                _split.SplitterDistance = 180;
                _splitterApplied = true;
            }
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _room = blockBase as RoomBlock;
            _tree.Nodes.Clear();
            _picture.Image = null;
            _currentTarget = null;
            _exportButton.Enabled = false;
            _importButton.Enabled = false;
            if (_room == null) return;

            // Background, with one child node per z-plane (mask).
            if (HasBackground(_room))
            {
                var node = _tree.Nodes.Add("Room Background");
                node.Tag = new ImageTarget { IsBackground = true, ObjectIndex = -1 };
                AddZPlaneNodes(node, true, -1, _decoder.CountBackgroundZPlanes(_room));
                node.Expand();
            }

            // Objects: list every OBIM that carries an image (SMAP or BOMP); a hotspot-only object has no
            // IMAG and is skipped. Decoding is deferred to selection so a room with many objects opens fast.
            var obims = _room.Childrens.Where(c => c.BlockType == "OBIM").ToList();
            for (int i = 0; i < obims.Count; i++)
            {
                if (!obims[i].Childrens.Any(c => c.BlockType == "IMAG")) continue; // hotspot-only
                var node = _tree.Nodes.Add("Object " + i);
                node.Tag = new ImageTarget { ObjectIndex = i };
                AddZPlaneNodes(node, false, i, _decoder.CountObjectZPlanes(_room, i));
            }

            if (_tree.Nodes.Count > 0) _tree.SelectedNode = _tree.Nodes[0];
        }

        private void AddZPlaneNodes(TreeNode parent, bool isBackground, int objectIndex, int zCount)
        {
            for (int z = 0; z < zCount; z++)
            {
                var zNode = parent.Nodes.Add("Z-Plane " + z);
                zNode.Tag = new ImageTarget { IsBackground = isBackground, ObjectIndex = objectIndex, IsZPlane = true, ZPlaneIndex = z };
            }
        }

        private static bool HasBackground(RoomBlock room)
        {
            return room.Childrens.Any(c => c.BlockType == "IMAG");
        }

        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            _currentTarget = e.Node.Tag as ImageTarget;
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (_currentTarget == null)
            {
                _picture.Image = null;
                _exportButton.Enabled = false;
                _importButton.Enabled = false;
                return;
            }

            if (_currentTarget.IsZPlane)
            {
                _currentImage = _currentTarget.IsBackground
                    ? _decoder.DecodeBackgroundZPlane(_room, _currentTarget.ZPlaneIndex)
                    : _decoder.DecodeObjectZPlane(_room, _currentTarget.ObjectIndex, _currentTarget.ZPlaneIndex);
            }
            else
            {
                _currentImage = _currentTarget.IsBackground
                    ? _decoder.DecodeBackground(_room)
                    : _decoder.DecodeObject(_room, _currentTarget.ObjectIndex);
            }

            _picture.Image = _currentImage;
            _exportButton.Enabled = _currentImage != null;
            _importButton.Enabled = _currentImage != null;
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_currentImage == null) return;
            using (var dialog = new SaveFileDialog { Filter = "PNG Files|*.png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                _currentImage.Save(dialog.FileName, ImageFormat.Png); // already 8bpp-indexed -> palette preserved
            }
        }

        private void ImportClick(object sender, EventArgs e)
        {
            if (_currentTarget == null) return;
            using (var dialog = new OpenFileDialog { Filter = "PNG Files|*.png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    using (var imported = (Bitmap)Image.FromFile(dialog.FileName))
                    {
                        if (_currentTarget.IsZPlane)
                        {
                            if (_currentTarget.IsBackground) _encoder.EncodeBackgroundZPlane(_room, _currentTarget.ZPlaneIndex, imported);
                            else _encoder.EncodeObjectZPlane(_room, _currentTarget.ObjectIndex, _currentTarget.ZPlaneIndex, imported);
                        }
                        else if (_currentTarget.IsBackground)
                        {
                            _encoder.EncodeBackground(_room, imported);
                        }
                        else
                        {
                            _encoder.EncodeObject(_room, _currentTarget.ObjectIndex, imported);
                        }
                    }
                }
                catch (ImageEncodeException ex)
                {
                    MessageBox.Show(ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                RefreshPreview();
                MessageBox.Show("Image imported. Use \"Save changes\" to write it back to the game files.",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
