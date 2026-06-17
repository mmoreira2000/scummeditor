using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes the EGA images of a SCUMM v3 "old bundle" room (Loom EGA, Indy3 EGA). The room is a raw
    /// chunk, not a block tree, so the background/object strip tables are reached by their offsets in
    /// the room bytes; the actual EGA strip RLE is identical to v4, so the decode is delegated to the
    /// existing ScummV4ImageDecoder.DecodeEgaImage. The palette is the fixed 16-colour EGA hardware
    /// table (v3 EGA rooms carry no per-room palette).
    /// </summary>
    public class ScummV3OldImageDecoder
    {
        private readonly ScummV4ImageDecoder _decoder = new ScummV4ImageDecoder();

        /// <summary>Decodes the room background, or null when the room declares no image.</summary>
        public Bitmap DecodeBackground(ScummV3OldRoom room)
        {
            if (room == null || room.Width == 0 || room.Height == 0 || room.ImageOffset == 0)
            {
                return null;
            }

            return _decoder.DecodeEgaImage(room.Data, room.ImageOffset, room.Width, room.Height, EgaColorTable.Colors256);
        }

        /// <summary>
        /// Decodes object image index i, or null when the object has no real image. Many v3 objects
        /// are hotspot-only and leave a junk/shared OBIM slot, so we decode only when the strip table
        /// at the object's OBIM offset is self-consistent with the object's own width (the strips
        /// start right after the offset table) - which is exactly the object that owns that image.
        /// </summary>
        public Bitmap DecodeObject(ScummV3OldRoom room, int objectIndex)
        {
            if (room == null)
            {
                return null;
            }

            int obim = room.ObjectImageOffset(objectIndex);
            int width = room.ObjectWidth(objectIndex);
            int height = room.ObjectHeight(objectIndex);
            if (obim == 0 || width == 0 || height == 0)
            {
                return null;
            }

            if (!HasConsistentEgaStripTable(room.Data, obim, width))
            {
                return null;
            }

            return _decoder.DecodeEgaImage(room.Data, obim, width, height, EgaColorTable.Colors256);
        }

        /// <summary>
        /// True when the bytes at baseIndex form an EGA strip table for an image of the given width:
        /// smapLen fits the buffer and the first strip offset equals the offset-table size
        /// (2 + numStrips*2), i.e. the strips begin immediately after the table. This identifies the
        /// object that actually owns the image vs. hotspot-only objects pointing at a shared slot.
        /// </summary>
        private static bool HasConsistentEgaStripTable(byte[] data, int baseIndex, int width)
        {
            int numStrips = width / 8;
            if (numStrips <= 0 || baseIndex < 0 || baseIndex + 2 + numStrips * 2 > data.Length)
            {
                return false;
            }

            int smapLen = data[baseIndex] | (data[baseIndex + 1] << 8);
            if (smapLen < 2 + numStrips * 2 || baseIndex + smapLen > data.Length)
            {
                return false;
            }

            int firstOffset = data[baseIndex + 2] | (data[baseIndex + 3] << 8);
            return firstOffset == 2 + numStrips * 2;
        }
    }
}
