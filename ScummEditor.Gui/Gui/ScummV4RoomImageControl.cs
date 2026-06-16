using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Image viewer for a SCUMM v4 room (RO block): a tree of the room background and its object
    /// images on the left, the decoded picture on the right, with PNG export. v4 rooms have a flat
    /// image layout (BM/OI) with no TRNS/PALS, so this uses ScummV4ImageDecoder rather than the
    /// v5/v6 DiskBlockControl/RoomBlockImageControl path.
    /// </summary>
    public class ScummV4RoomImageControl : BlockBaseControl
    {
        private readonly SplitContainer _split;
        private readonly TreeView _tree;
        private readonly Panel _scroll;
        private readonly PictureBox _picture;
        private readonly Button _exportButton;
        private readonly Button _importButton;
        private readonly ScummV4ImageDecoder _decoder = new ScummV4ImageDecoder();
        private readonly ScummV4ImageEncoder _encoder = new ScummV4ImageEncoder();

        private ScummV4RoomBlock _room;
        private ImageTarget _currentTarget;
        private Bitmap _currentImage;
        private bool _splitterApplied;

        /// <summary>
        /// What a tree node renders: the room background, one object image, or a z-plane (mask) of
        /// either. A z-plane node keeps its parent's IsBackground / ObjectImage / ObjectCode so the
        /// decoder knows which block to read, plus which z-plane index within it.
        /// </summary>
        private class ImageTarget
        {
            public bool IsBackground { get; set; }
            public ScummV4ImageBlock ObjectImage { get; set; }
            public ObjectCode ObjectCode { get; set; }
            public bool IsZPlane { get; set; }
            public int ZPlaneIndex { get; set; }
        }

        public ScummV4RoomImageControl()
        {
            _split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };

            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            _tree.AfterSelect += TreeAfterSelect;
            _split.Panel1.Controls.Add(_tree);

            // The picture fills the panel; a thin top bar holds the export button. The fill control
            // is added first so the top-docked bar takes its edge and the picture keeps the rest.
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

        // The splitter distance can only be set once the control is wide enough; setting it in the
        // constructor (when the control still has its small default size) would throw.
        // NOTE: the base BlockBaseControl constructor sets the control's Size (which raises
        // OnSizeChanged) BEFORE this class's constructor assigns _split, so _split can still be null
        // here on the very first call - guard against it.
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);

            if (_split != null && !_splitterApplied && _split.Width > 200)
            {
                _split.SplitterDistance = 170;
                _splitterApplied = true;
            }
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _room = blockBase as ScummV4RoomBlock;
            _tree.Nodes.Clear();
            _picture.Image = null;
            _currentTarget = null;
            _exportButton.Enabled = false;
            _importButton.Enabled = false;

            if (_room == null)
            {
                return;
            }

            // Background, with one child node per z-plane (mask) embedded in it.
            if (_room.GetBM() != null)
            {
                var node = _tree.Nodes.Add("Room Background");
                node.Tag = new ImageTarget { IsBackground = true };

                int backgroundZPlanes = _decoder.CountBackgroundZPlanes(_room);
                for (int z = 0; z < backgroundZPlanes; z++)
                {
                    var zNode = node.Nodes.Add("Z-Plane " + z);
                    zNode.Tag = new ImageTarget { IsBackground = true, IsZPlane = true, ZPlaneIndex = z };
                }
                node.Expand();
            }

            // Objects: pair each OI with its OC (by object id). Only objects that actually decode to
            // an image are listed - many objects carry a hotspot size in their OC but no pixels in
            // their OI (those would just render blank), so they are skipped (as the v5/v6 viewer does).
            List<ObjectCode> codes = _room.GetObjectCodes();
            foreach (ScummV4ImageBlock objectImage in _room.GetObjectImages())
            {
                ObjectCode code = codes.Find(c => c.ObjectId == objectImage.ObjectId);
                if (code == null || code.Width == 0 || code.Height == 0)
                {
                    continue;
                }
                if (_decoder.DecodeObject(_room, objectImage, code) == null)
                {
                    continue; // hotspot-only object: declared size but no image pixels
                }

                var node = _tree.Nodes.Add("Object " + objectImage.ObjectId);
                node.Tag = new ImageTarget { ObjectImage = objectImage, ObjectCode = code };

                int objectZPlanes = _decoder.CountObjectZPlanes(_room, objectImage, code);
                for (int z = 0; z < objectZPlanes; z++)
                {
                    var zNode = node.Nodes.Add("Z-Plane " + z);
                    zNode.Tag = new ImageTarget { ObjectImage = objectImage, ObjectCode = code, IsZPlane = true, ZPlaneIndex = z };
                }
            }

            if (_tree.Nodes.Count > 0)
            {
                _tree.SelectedNode = _tree.Nodes[0];
            }
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
                    : _decoder.DecodeObjectZPlane(_room, _currentTarget.ObjectImage, _currentTarget.ObjectCode, _currentTarget.ZPlaneIndex);
            }
            else
            {
                _currentImage = _currentTarget.IsBackground
                    ? _decoder.DecodeBackground(_room)
                    : _decoder.DecodeObject(_room, _currentTarget.ObjectImage, _currentTarget.ObjectCode);
            }

            _picture.Image = _currentImage;
            _exportButton.Enabled = _currentImage != null;
            _importButton.Enabled = _currentImage != null;
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_currentImage == null)
            {
                return;
            }

            using (var dialog = new SaveFileDialog { Filter = "PNG Files|*.png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                // The decoded bitmap is already 8bpp-indexed, so the PNG keeps the palette indexes.
                _currentImage.Save(dialog.FileName, ImageFormat.Png);
            }
        }

        private void ImportClick(object sender, EventArgs e)
        {
            if (_currentTarget == null)
            {
                return;
            }

            using (var dialog = new OpenFileDialog { Filter = "PNG Files|*.png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                try
                {
                    using (var imported = (Bitmap)Image.FromFile(dialog.FileName))
                    {
                        if (_currentTarget.IsZPlane)
                        {
                            if (_currentTarget.IsBackground)
                            {
                                _encoder.EncodeBackgroundZPlane(_room, _currentTarget.ZPlaneIndex, imported);
                            }
                            else
                            {
                                _encoder.EncodeObjectZPlane(_room, _currentTarget.ObjectImage, _currentTarget.ObjectCode, _currentTarget.ZPlaneIndex, imported);
                            }
                        }
                        else if (_currentTarget.IsBackground)
                        {
                            _encoder.EncodeBackground(_room, imported);
                        }
                        else
                        {
                            _encoder.EncodeObject(_room, _currentTarget.ObjectImage, _currentTarget.ObjectCode, imported);
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
