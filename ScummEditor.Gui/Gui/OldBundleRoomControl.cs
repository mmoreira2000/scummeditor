using System.Windows.Forms;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Read-only viewer for a v2 / v3-old room header (an OldBundleBlock of Kind=Header), mirroring the v4
    /// RoomHeaderControl: a field/value grid of dimensions, sub-resource offsets and counts, read from the
    /// typed room view (ScummV2Room / ScummV3OldRoom).
    /// </summary>
    public class OldBundleRoomControl : UserControl
    {
        private readonly DataGridView _grid;

        public OldBundleRoomControl()
        {
            _grid = OldBundleControlHelpers.CreateFieldValueGrid();
            Controls.Add(_grid);
        }

        public void SetData(OldBundleBlock block)
        {
            _grid.Rows.Clear();
            if (block == null || block.DataFile == null || block.DataFile.RawContent == null) return;

            byte[] data = block.DataFile.RawContent;
            _grid.Rows.Add("Room", block.RoomNo);
            _grid.Rows.Add("Container", block.IsV2 ? "v2 (Maniac / Zak)" : "v3 old-bundle (Loom / Indy3 EGA)");
            _grid.Rows.Add("File size", data.Length + " bytes");

            if (block.IsV2)
            {
                var room = new ScummV2Room(data);
                AddRoomRows(room.Width, room.Height, room.NumObjects, room.NumSounds, room.NumScripts,
                    room.ImageOffset, room.BoxOffset, room.EntryScriptOffset, room.ExitScriptOffset);
            }
            else
            {
                var room = new ScummV3OldRoom(data);
                AddRoomRows(room.Width, room.Height, room.NumObjects, room.NumSounds, room.NumScripts,
                    room.ImageOffset, room.BoxOffset, room.EntryScriptOffset, room.ExitScriptOffset);
            }
        }

        private void AddRoomRows(int width, int height, int numObjects, int numSounds, int numScripts,
            int imageOffset, int boxOffset, int entryScript, int exitScript)
        {
            _grid.Rows.Add("Width", width);
            _grid.Rows.Add("Height", height);
            _grid.Rows.Add("Objects", numObjects);
            _grid.Rows.Add("Sounds", numSounds);
            _grid.Rows.Add("Scripts", numScripts);
            _grid.Rows.Add("Image offset", "0x" + imageOffset.ToString("X4"));
            _grid.Rows.Add("Box offset", "0x" + boxOffset.ToString("X4"));
            _grid.Rows.Add("Entry script", "0x" + entryScript.ToString("X4"));
            _grid.Rows.Add("Exit script", "0x" + exitScript.ToString("X4"));
        }
    }
}
