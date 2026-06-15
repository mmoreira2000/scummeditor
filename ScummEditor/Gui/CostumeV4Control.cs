using System;
using System.Drawing;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for a SCUMM v4 costume ("CO" block): a list of the costume's frames (CELs) on the left,
    /// the decoded frame on the right. Read-only - it renders via CostumeImageDecoderV4 using the
    /// palette of the costume's room (PA for VGA, the EGA table for EGA). Mirrors the v4 room image
    /// viewer; v4 costumes have a flat layout decoded by CostumeV4, not the v5/v6 Costume path.
    /// </summary>
    public class CostumeV4Control : BlockBaseControl
    {
        private readonly SplitContainer _split;
        private readonly TreeView _tree;
        private readonly Panel _scroll;
        private readonly PictureBox _picture;
        private readonly CostumeImageDecoderV4 _decoder = new CostumeImageDecoderV4();

        private CostumeV4 _costume;
        private Color[] _palette;
        private bool _splitterApplied;

        public CostumeV4Control()
        {
            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            _tree.AfterSelect += TreeAfterSelect;
            _split.Panel1.Controls.Add(_tree);

            _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.LightGray };
            _picture = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
            _scroll.Controls.Add(_picture);
            _split.Panel2.Controls.Add(_scroll);

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
            if (_costume == null) return;

            _palette = ResolvePalette(_costume);

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
                return;
            }

            int index = (int)e.Node.Tag;
            // Transparent background (costume color 0) so the sprite stands out against the panel.
            _picture.Image = _decoder.Decode(_costume.Frames[index], _costume.PaletteSize, _palette, true);
        }

        /// <summary>Builds the costume's frame palette from its room: VGA = PA colors, EGA = the EGA table.</summary>
        private static Color[] ResolvePalette(CostumeV4 costume)
        {
            Color[] roomColors = null;
            var disk = costume.Parent as ScummV4DiskBlock;
            ScummV4RoomBlock room = disk != null ? disk.GetRoom() : null;
            if (room != null)
            {
                PaletteData pa = room.GetPA();
                roomColors = room.IsEga ? EgaColorTable.Colors256 : (pa != null ? pa.Colors : null);
            }

            var palette = new Color[costume.PaletteSize];
            for (int i = 0; i < costume.PaletteSize; i++)
            {
                int index = i < costume.Palette.Count ? costume.Palette[i] : 0;
                palette[i] = (roomColors != null && index < roomColors.Length) ? roomColors[index] : Color.Black;
            }
            return palette;
        }
    }
}
