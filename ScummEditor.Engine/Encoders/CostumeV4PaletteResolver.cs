using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Resolves the real (room) colours for a SCUMM v4 costume's local palette: a VGA room uses its
    /// PA palette, an EGA room the fixed 16-colour EGA table. Shared by the costume viewer and the
    /// batch graphics export so both render costumes identically.
    /// </summary>
    public static class CostumeV4PaletteResolver
    {
        public static Color[] Resolve(CostumeV4 costume)
        {
            Color[] roomColors = null;

            // v4 packs the room and its costumes in an LF disk block; v3 (GF_OLD256) keeps one room
            // per NN.LFL where the costume's RO is a sibling under the data-file container.
            ScummV4RoomBlock room = null;
            var disk = costume.Parent as ScummV4DiskBlock;
            if (disk != null)
            {
                room = disk.GetRoom();
            }
            else
            {
                var v3File = costume.Parent as ScummV3Small256DataFile;
                if (v3File != null)
                {
                    room = v3File.GetRoom();
                }
            }

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
