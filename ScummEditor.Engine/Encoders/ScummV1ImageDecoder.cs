using System;
using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes SCUMM v1 (Maniac Mansion / Zak McKracken classic DOS) room backgrounds, object images and
    /// walk-behind masks. v1 does NOT use the v2 vertical-RLE; it is a C64-style TILEMAP: a shared 256-tile
    /// charMap plus per-cell picMap (tile index), colorMap (low 3 bits = the 4th colour) and maskMap
    /// (mask-tile index), each compressed with the simple <see cref="DecodeV1Gfx"/> RLE. A tile is 8 bytes
    /// (one per pixel-row), 2 bits per pixel (4 colours), each 2-bit pixel doubled to 2 screen pixels (an
    /// 8px strip). Backgrounds are COLUMN-major (cell = row + strip*rows); object images are one combined
    /// 3-plane (tile/colour/mask) ROW-major stream at the OBIM. Colours pass through the per-game v1ColorMap
    /// render remap, then the 16-colour EGA palette. Mirrors ScummVM GdiV1 (gfx.cpp). Decode/preview only;
    /// re-encode (tile quantization) is a separate, lossy concern.
    /// </summary>
    public class ScummV1ImageDecoder
    {
        // v1ColorMaps (ScummVM palette.cpp): the DOS-EGA render remap, per game. Maps a 0..15 colour index
        // to the EGA palette slot actually drawn (Maniac and Zak differ only at indices 10 and 15).
        private static readonly byte[] ZakEgaColorMap =
            { 0x00, 0x0F, 0x04, 0x03, 0x05, 0x02, 0x01, 0x0E, 0x0C, 0x06, 0x0D, 0x08, 0x07, 0x0A, 0x09, 0x07 };
        private static readonly byte[] ManiacEgaColorMap =
            { 0x00, 0x0F, 0x04, 0x03, 0x05, 0x02, 0x01, 0x0E, 0x0C, 0x06, 0x0C, 0x08, 0x07, 0x0A, 0x09, 0x08 };

        private readonly byte[] _renderMap;

        public ScummV1ImageDecoder(bool isManiac)
        {
            _renderMap = isManiac ? ManiacEgaColorMap : ZakEgaColorMap;
        }

        /// <summary>Decodes the room background tilemap to a 16-colour EGA bitmap, or null if it cannot be read.</summary>
        public Bitmap DecodeBackground(ScummV1Room room)
        {
            byte[,] matrix = BackgroundMatrix(room);
            return matrix == null ? null : ToBitmap(matrix);
        }

        /// <summary>Decodes the room background to a [width,height] matrix of (render-remapped) EGA indices, or null.</summary>
        public byte[,] BackgroundMatrix(ScummV1Room room)
        {
            if (room == null) return null;
            int w = room.WidthInChars, h = room.HeightInChars;
            if (w <= 0 || h <= 0) return null;

            byte[] charMap = DecodeV1Gfx(room.Data, room.CharMapOffset, 2048);
            byte[] picMap = DecodeV1Gfx(room.Data, room.PicMapOffset, w * h);
            byte[] colorMap = DecodeV1Gfx(room.Data, room.ColorMapOffset, w * h);
            if (charMap == null || picMap == null || colorMap == null) return null;

            var matrix = new byte[w * 8, h * 8];
            var colors = new int[4];
            colors[0] = room.Color(0); colors[1] = room.Color(1); colors[2] = room.Color(2);

            for (int strip = 0; strip < w; strip++)
            {
                for (int y = 0; y < h; y++)
                {
                    int cell = y + strip * h; // column-major
                    colors[3] = colorMap[cell] & 7;
                    DrawTile(matrix, charMap, picMap[cell], colors, strip * 8, y * 8);
                }
            }
            return matrix;
        }

        /// <summary>Decodes object image <paramref name="objectIndex"/> (a 3-plane combined stream at its OBIM), or null.</summary>
        public Bitmap DecodeObject(ScummV1Room room, int objectIndex)
        {
            if (room == null) return null;
            int obim = room.ObjectImageOffset(objectIndex);
            int wpx = room.ObjectWidth(objectIndex), hpx = room.ObjectHeight(objectIndex);
            if (obim <= 0 || obim >= room.Data.Length || wpx <= 0 || hpx <= 0) return null;

            // An imageless object leaves its OBIM pointing at a code (OBCD) block; do not decode that.
            for (int k = 0; k < room.NumObjects; k++)
                if (room.ObjectCodeOffset(k) == obim) return null;

            int w = wpx / 8, h = hpx / 8;
            if (w <= 0 || h <= 0) return null;

            byte[] charMap = DecodeV1Gfx(room.Data, room.CharMapOffset, 2048);
            byte[] objectMap = DecodeV1Gfx(room.Data, obim, w * h * 3);
            if (charMap == null || objectMap == null) return null;

            var matrix = new byte[wpx, hpx];
            var colors = new int[4];
            colors[0] = room.Color(0); colors[1] = room.Color(1); colors[2] = room.Color(2);

            for (int strip = 0; strip < w; strip++)
            {
                for (int y = 0; y < h; y++)
                {
                    int tile = objectMap[y * w + strip];          // plane 0 (tile), row-major
                    colors[3] = objectMap[(y + h) * w + strip] & 7; // plane 1 (colour)
                    DrawTile(matrix, charMap, tile, colors, strip * 8, y * 8);
                }
            }
            return ToBitmap(matrix);
        }

        /// <summary>
        /// Decodes the room's walk-behind (z-plane) mask (maskMap cells -> maskChar tiles) to a black/white
        /// bitmap (white = masked / walk-behind). v1 mask bytes are stored inverted (^0xFF). Null if absent.
        /// </summary>
        public Bitmap DecodeBackgroundZPlane(ScummV1Room room)
        {
            if (room == null) return null;
            int w = room.WidthInChars, h = room.HeightInChars;
            if (w <= 0 || h <= 0) return null;

            byte[] maskMap = DecodeV1Gfx(room.Data, room.MaskMapOffset, w * h);
            int maskPtr = room.MaskDataOffset;
            if (maskMap == null || maskPtr <= 0 || maskPtr + 2 > room.Data.Length) return null;

            int storedLen = room.Data[maskPtr] | (room.Data[maskPtr + 1] << 8);
            int maskCharLen = storedLen - 8; // the stored length word is always 8 too big (ScummVM bug #3458)
            if (maskCharLen <= 0) return null;
            byte[] maskChar = DecodeV1Gfx(room.Data, maskPtr + 2, maskCharLen);
            if (maskChar == null) return null;

            var matrix = new byte[w * 8, h * 8];
            for (int strip = 0; strip < w; strip++)
            {
                for (int y = 0; y < h; y++)
                {
                    int midx = maskMap[y + strip * h] * 8;
                    for (int i = 0; i < 8; i++)
                    {
                        if (midx + i >= maskChar.Length) break;
                        int c = maskChar[midx + i] ^ 0xFF;
                        int py = y * 8 + i;
                        for (int b = 0; b < 8; b++)
                            matrix[strip * 8 + b, py] = (byte)((c >> (7 - b)) & 1);
                    }
                }
            }
            return IndexedImageHelper.FromIndexMatrix(matrix, new[] { Color.Black, Color.White }, -1);
        }

        /// <summary>
        /// The v1 graphics-map RLE (ScummVM decodeV1Gfx). A 4-byte "common colour" header, then runs:
        /// bit 0x80 = a common-colour run (colour = common[(b&gt;&gt;5)&amp;3], length (b&amp;0x1F)+1); bit 0x40 = a
        /// literal-colour run (length (b&amp;0x3F)+1, one following colour byte); otherwise a raw copy of
        /// (b&amp;0x3F)+1 bytes. Decodes exactly <paramref name="dstSize"/> bytes. Null on a malformed stream.
        /// </summary>
        public static byte[] DecodeV1Gfx(byte[] src, int offset, int dstSize)
        {
            if (src == null || dstSize <= 0 || offset < 0 || offset + 4 > src.Length) return null;
            var dst = new byte[dstSize];
            try
            {
                var common = new byte[4];
                for (int k = 0; k < 4; k++) common[k] = src[offset + k];
                int p = offset + 4;
                int x = 0;
                while (x < dstSize)
                {
                    byte run = src[p++];
                    if ((run & 0x80) != 0)
                    {
                        byte color = common[(run >> 5) & 3];
                        int count = (run & 0x1F) + 1;
                        for (int k = 0; k < count && x < dstSize; k++) dst[x++] = color;
                    }
                    else if ((run & 0x40) != 0)
                    {
                        int count = (run & 0x3F) + 1;
                        byte color = src[p++];
                        for (int k = 0; k < count && x < dstSize; k++) dst[x++] = color;
                    }
                    else
                    {
                        int count = (run & 0x3F) + 1;
                        for (int k = 0; k < count && x < dstSize; k++) dst[x++] = src[p++];
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            return dst;
        }

        /// <summary>Draws one 8x8 tile (8 bytes, 2bpp, each pixel doubled horizontally) at (px,py) into the index matrix.</summary>
        private void DrawTile(byte[,] matrix, byte[] charMap, int tile, int[] colors, int px, int py)
        {
            int charIdx = tile * 8;
            if (charIdx + 8 > charMap.Length) return;
            for (int i = 0; i < 8; i++)
            {
                int c = charMap[charIdx + i];
                SetPair(matrix, px + 0, py + i, colors[(c >> 6) & 3]);
                SetPair(matrix, px + 2, py + i, colors[(c >> 4) & 3]);
                SetPair(matrix, px + 4, py + i, colors[(c >> 2) & 3]);
                SetPair(matrix, px + 6, py + i, colors[(c >> 0) & 3]);
            }
        }

        /// <summary>Writes one 2-bit pixel to two adjacent columns, remapped through the render colour map.</summary>
        private void SetPair(byte[,] matrix, int px, int py, int colorIndex)
        {
            byte v = _renderMap[colorIndex & 0x0F];
            int w = matrix.GetLength(0), h = matrix.GetLength(1);
            if (py >= h) return;
            if (px < w) matrix[px, py] = v;
            if (px + 1 < w) matrix[px + 1, py] = v;
        }

        private static Bitmap ToBitmap(byte[,] matrix)
        {
            var ega = new Color[16];
            Array.Copy(EgaColorTable.Colors256, ega, 16);
            return IndexedImageHelper.FromIndexMatrix(matrix, ega, -1);
        }
    }
}
