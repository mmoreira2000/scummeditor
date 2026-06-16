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
