using System;
using System.Collections.Generic;
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
        /// Number of walk-behind z-planes in the room background. v3 old-bundle (GF_OLD_BUNDLE) rooms
        /// reserve exactly one (ScummVM fixes _numZBuffer at 2 for all v3, gfx.cpp:1039), so this is 1
        /// when a readable z-plane region sits after the background strips, else 0.
        /// </summary>
        public int CountBackgroundZPlanes(ScummV3OldRoom room)
        {
            if (room == null || room.Width == 0 || room.Height == 0 || room.ImageOffset == 0)
            {
                return 0;
            }
            int regionEnd = room.NextStructuralOffsetAbove(room.ImageOffset);
            return GetZPlaneStrips(room.Data, room.ImageOffset, room.Width, regionEnd) != null ? 1 : 0;
        }

        /// <summary>Number of walk-behind z-planes in object image <paramref name="objectIndex"/> (0 or 1).</summary>
        public int CountObjectZPlanes(ScummV3OldRoom room, int objectIndex)
        {
            if (room == null)
            {
                return 0;
            }
            int obim = room.ObjectImageOffset(objectIndex);
            int width = room.ObjectWidth(objectIndex);
            int height = room.ObjectHeight(objectIndex);
            if (obim == 0 || width == 0 || height == 0 || !HasConsistentEgaStripTable(room.Data, obim, width))
            {
                return 0;
            }
            int regionEnd = room.NextStructuralOffsetAbove(obim);
            return GetZPlaneStrips(room.Data, obim, width, regionEnd) != null ? 1 : 0;
        }

        /// <summary>
        /// Decodes the room's single walk-behind (z-plane) mask to a black/white bitmap (black = masked),
        /// or null when the room has none. The plane sits at zbase = ImageOffset + smapLen, immediately
        /// after the background strips; its per-strip offset table is at zbase (GF_OLD_BUNDLE base +0,
        /// ScummVM gfx.cpp:2612-2613), one LE16 offset per strip relative to zbase, 0 = empty strip.
        /// </summary>
        public Bitmap DecodeBackgroundZPlane(ScummV3OldRoom room)
        {
            if (room == null || room.Width == 0 || room.Height == 0 || room.ImageOffset == 0)
            {
                return null;
            }
            int regionEnd = room.NextStructuralOffsetAbove(room.ImageOffset);
            List<ZPlaneStripData> strips = GetZPlaneStrips(room.Data, room.ImageOffset, room.Width, regionEnd);
            if (strips == null)
            {
                return null;
            }
            return new ZPlaneDecoder().Decode(strips, room.Width, room.Height);
        }

        /// <summary>Decodes object <paramref name="objectIndex"/>'s walk-behind z-plane mask, or null when it has none.</summary>
        public Bitmap DecodeObjectZPlane(ScummV3OldRoom room, int objectIndex)
        {
            if (room == null)
            {
                return null;
            }
            int obim = room.ObjectImageOffset(objectIndex);
            int width = room.ObjectWidth(objectIndex);
            int height = room.ObjectHeight(objectIndex);
            if (obim == 0 || width == 0 || height == 0 || !HasConsistentEgaStripTable(room.Data, obim, width))
            {
                return null;
            }
            int regionEnd = room.NextStructuralOffsetAbove(obim);
            List<ZPlaneStripData> strips = GetZPlaneStrips(room.Data, obim, width, regionEnd);
            if (strips == null)
            {
                return null;
            }
            return new ZPlaneDecoder().Decode(strips, width, height);
        }

        /// <summary>
        /// Reads the per-strip z-plane offset table that follows the EGA strips at <paramref name="imageOffset"/>.
        /// zbase = imageOffset + smapLen; the table is numStrips x LE16 offsets relative to zbase (base +0,
        /// no leading header word - the GF_OLD_BUNDLE layout). A 0 offset marks an empty strip. Each strip's
        /// bytes run from its offset to the region end (the mask RLE self-terminates at the strip height).
        /// Returns null when there is no room for a full offset table before <paramref name="regionEnd"/>,
        /// i.e. the image has no z-plane region.
        /// </summary>
        private static List<ZPlaneStripData> GetZPlaneStrips(byte[] data, int imageOffset, int width, int regionEnd)
        {
            int numStrips = width / 8;
            if (numStrips <= 0 || imageOffset < 0 || imageOffset + 2 > data.Length)
            {
                return null;
            }
            int smapLen = data[imageOffset] | (data[imageOffset + 1] << 8);
            int zbase = imageOffset + smapLen;
            if (smapLen < 2 || zbase + numStrips * 2 > data.Length || zbase + numStrips * 2 > regionEnd)
            {
                return null; // no z-plane region fits between the strips and the next sub-resource
            }

            var strips = new List<ZPlaneStripData>(numStrips);
            for (int n = 0; n < numStrips; n++)
            {
                int start = data[zbase + n * 2] | (data[zbase + n * 2 + 1] << 8);
                if (start == 0)
                {
                    strips.Add(new ZPlaneStripData { OffSet = 0, ImageData = new byte[0] });
                    continue;
                }

                int dataStart = zbase + start;
                int length = regionEnd - dataStart;
                if (dataStart < numStrips * 2 + zbase || length < 0 || dataStart + length > data.Length)
                {
                    length = 0; // offset points into the table or past the buffer - treat as empty
                }
                var bytes = new byte[length];
                if (length > 0)
                {
                    Array.Copy(data, dataStart, bytes, 0, length);
                }
                strips.Add(new ZPlaneStripData { OffSet = (ushort)start, ImageData = bytes });
            }
            return strips;
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
