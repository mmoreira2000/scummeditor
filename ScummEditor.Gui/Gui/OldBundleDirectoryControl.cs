using System.Drawing;
using System.Windows.Forms;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Read-only viewer for a v2 / v3-old index resource directory (an OldBundleBlock of Kind=Directory),
    /// mirroring the v4 DirectoryOfItemsControl: an entry count plus a list of (id, room, offset) rows.
    /// </summary>
    public class OldBundleDirectoryControl : UserControl
    {
        private readonly Label _header;
        private readonly ListView _list;

        public OldBundleDirectoryControl()
        {
            _header = new Label { Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                HideSelection = false
            };
            _list.Columns.Add("Id", 70);
            _list.Columns.Add("Room", 70);
            _list.Columns.Add("Offset", 100);
            Controls.Add(_list);
            Controls.Add(_header);
        }

        public void SetData(OldBundleBlock block)
        {
            _list.Items.Clear();
            if (block == null) { _header.Text = "(no directory)"; return; }

            V3OldResourceDirectory dir = block.Directory;
            if (dir == null) { _header.Text = block.Title + " - (not parsed)"; return; }

            _header.Text = string.Format("{0}   ·   {1} entries", block.Title, dir.Count);
            _list.BeginUpdate();
            for (int i = 0; i < dir.Count; i++)
            {
                int off = dir.Offsets[i];
                var item = new ListViewItem(i.ToString());
                item.SubItems.Add(dir.RoomNumbers[i].ToString());
                item.SubItems.Add(off == 0xFFFF || off == 0 ? "(absent)" : "0x" + off.ToString("X4"));
                _list.Items.Add(item);
            }
            _list.EndUpdate();
        }
    }
}
