using System;
using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes SCUMM v2 (Maniac Mansion / Zak McKracken) room backgrounds and object images. Unlike the
    /// v3+ per-strip EGA codec, a v2 image is ONE column-major vertical-RLE stream covering the whole
    /// bitmap (ScummVM GdiV2::prepareDrawBitmap / generateStripTable). Each data byte gives a run and a
    /// 4-bit colour (low nibble): if bit 0x80 is set it is a "dither" run that REUSES the colour the
    /// previous column left at the same row (a per-column dither table indexed by row); otherwise it is a
    /// solid run that also records its colour in that table. A run length of 0 is followed by an extended
    /// length byte. The 16 colours are the fixed EGA palette (the room palette is identity in v2 EGA).
    /// </summary>
    public class ScummV2ImageDecoder
    {
        /// <summary>Decodes the room background to a 16-colour EGA bitmap, or null if it cannot be read.</summary>
        public Bitmap DecodeBackground(ScummV2Room room)
        {
            if (room == null || room.Width <= 0 || room.Height <= 0 || room.ImageOffset <= 0) return null;
            byte[,] matrix = DecodeRle(room.Data, room.ImageOffset, room.Width, room.Height);
            return matrix == null ? null : ToBitmap(matrix);
        }

        /// <summary>Decodes object image <paramref name="objectIndex"/> (its OBIM is the raw RLE stream), or null.</summary>
        public Bitmap DecodeObject(ScummV2Room room, int objectIndex)
        {
            if (!ObjectOwnsImage(room, objectIndex)) return null;
            int obim = room.ObjectImageOffset(objectIndex);
            int width = room.ObjectWidth(objectIndex);
            int height = room.ObjectHeight(objectIndex);
            byte[,] matrix = DecodeRle(room.Data, obim, width, height);
            return matrix == null ? null : ToBitmap(matrix);
        }

        /// <summary>
        /// True when object <paramref name="objectIndex"/> genuinely OWNS the image at its OBIM offset.
        /// v2 (like v0) leaves an imageless object's OBIM pointing at a code (OBCD) block, and several
        /// multi-state objects can share one OBIM while each declares a different size; decoding such an
        /// object against its own width/height yields garbage, and re-encoding it would splice over an
        /// unrelated resource (another object's code, or the primary state's real image). An object owns
        /// its image only when its OBIM is not an OBCD-table entry AND its declared graphics fit within
        /// the object's region. Mirrors ScummVM resetRoomObjects' defaultPtr handling (object.cpp).
        /// </summary>
        public static bool ObjectOwnsImage(ScummV2Room room, int objectIndex)
        {
            if (room == null) return false;
            int obim = room.ObjectImageOffset(objectIndex);
            int w = room.ObjectWidth(objectIndex), h = room.ObjectHeight(objectIndex);
            if (obim <= 0 || obim >= room.Data.Length || w <= 0 || h <= 0) return false;

            for (int k = 0; k < room.NumObjects; k++)
            {
                if (room.ObjectCodeOffset(k) == obim) return false; // OBIM points at a code block: imageless
            }

            int regionEnd = room.NextStructuralOffsetAbove(obim);
            int gfxLen = GraphicsRleLength(room.Data, obim, w, h);
            return obim + gfxLen <= regionEnd; // declared size must fit (rejects non-primary multi-state)
        }

        /// <summary>
        /// The GdiV2 column-major vertical RLE. Produces a [width,height] index matrix of 4-bit colours.
        /// Returns null on a malformed stream (so a junk OBIM is skipped rather than throwing).
        /// </summary>
        public static byte[,] DecodeRle(byte[] code, int offset, int width, int height)
        {
            if (code == null || width <= 0 || height <= 0 || height > 128) return null;
            var matrix = new byte[width, height];
            var ditherTable = new byte[height];
            int src = offset;
            int run = 1, color = 0;
            bool dither = false;
            try
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (--run == 0)
                        {
                            byte data = code[src++];
                            if ((data & 0x80) != 0) { run = data & 0x7F; dither = true; }
                            else { run = data >> 4; dither = false; }
                            color = data & 0x0F;
                            if (run == 0) run = code[src++];
                        }
                        if (!dither) ditherTable[y] = (byte)color;
                        matrix[x, y] = ditherTable[y];
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            return matrix;
        }

        /// <summary>
        /// Decodes the room's single walk-behind (z-plane) mask, which follows the background graphics in
        /// the IM00 region, to a black/white bitmap (white = masked / walk-behind). Returns null if the
        /// room has no decodable image. The mask is an RLE of per-8-column strips: a run byte (bit 0x80 =
        /// repeat one mask byte, else one literal byte per row), each byte holding 8 horizontal mask bits
        /// (bit 7 = leftmost). Matches ScummVM GdiV2 mask decode.
        /// </summary>
        public Bitmap DecodeBackgroundZPlane(ScummV2Room room)
        {
            if (room == null || room.Width <= 0 || room.Height <= 0 || room.ImageOffset <= 0) return null;
            int gfxLen = GraphicsRleLength(room.Data, room.ImageOffset, room.Width, room.Height);
            int maskStart = room.ImageOffset + gfxLen;
            int imageEnd = room.NextStructuralOffsetAbove(room.ImageOffset);
            if (maskStart >= imageEnd || maskStart >= room.Data.Length) return null;
            byte[,] mask = DecodeMaskRle(room.Data, maskStart, room.Width, room.Height);
            if (mask == null) return null;
            return IndexedImageHelper.FromIndexMatrix(mask, new[] { Color.Black, Color.White }, -1);
        }

        /// <summary>The GdiV2 mask RLE -> a [width,height] matrix of 0/1 (1 = mask bit set). Null on a malformed stream.</summary>
        public static byte[,] DecodeMaskRle(byte[] code, int offset, int width, int height)
        {
            if (code == null || width <= 0 || height <= 0) return null;
            var mask = new byte[width, height];
            int src = offset;
            int theX = 0, theY = 0;
            try
            {
                int run = code[src++];
                byte data = 0;
                while (theX < width)
                {
                    bool runFlag = (run & 0x80) != 0;
                    if (runFlag) { run &= 0x7F; data = code[src++]; }
                    do
                    {
                        if (!runFlag) data = code[src++];
                        for (int b = 0; b < 8; b++)
                        {
                            int px = theX + b;
                            if (px < width) mask[px, theY] = (byte)((data >> (7 - b)) & 1);
                        }
                        theY++;
                        if (theY >= height) { theY = 0; theX += 8; if (theX >= width) break; }
                    } while (--run != 0);
                    if (theX >= width) break;
                    run = code[src++];
                }
            }
            catch (IndexOutOfRangeException)
            {
                return mask; // partial mask is still useful for a preview
            }
            return mask;
        }

        /// <summary>
        /// Number of bytes the graphics RLE at <paramref name="offset"/> consumes (so the z-plane mask
        /// that follows it can be located). Walks the same stream as DecodeRle without building pixels.
        /// </summary>
        public static int GraphicsRleLength(byte[] code, int offset, int width, int height)
        {
            if (code == null || width <= 0 || height <= 0) return 0;
            int src = offset, run = 1;
            try
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (--run == 0)
                        {
                            byte data = code[src++];
                            run = (data & 0x80) != 0 ? (data & 0x7F) : (data >> 4);
                            if (run == 0) run = code[src++];
                        }
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                return src - offset;
            }
            return src - offset;
        }

        private static Bitmap ToBitmap(byte[,] matrix)
        {
            var ega = new Color[16];
            Array.Copy(EgaColorTable.Colors256, ega, 16);
            return IndexedImageHelper.FromIndexMatrix(matrix, ega, -1);
        }
    }
}
