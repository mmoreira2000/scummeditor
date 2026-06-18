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
            if (room == null) return null;
            int obim = room.ObjectImageOffset(objectIndex);
            int width = room.ObjectWidth(objectIndex);
            int height = room.ObjectHeight(objectIndex);
            if (obim <= 0 || obim >= room.Data.Length || width <= 0 || height <= 0) return null;
            byte[,] matrix = DecodeRle(room.Data, obim, width, height);
            return matrix == null ? null : ToBitmap(matrix);
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
