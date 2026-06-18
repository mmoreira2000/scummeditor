using System;
using System.Collections.Generic;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited SCUMM v2 room background / object image into the GdiV2 column-major
    /// vertical-RLE stream (the inverse of ScummV2ImageDecoder). It emits only SOLID runs - no dither - so
    /// the decode reproduces the edited pixels exactly (lossless). A run of length R of colour C is one
    /// data byte (R &lt;&lt; 4) | C when R &lt;= 7 (a longer inline run would set the 0x80 dither bit), and
    /// otherwise a (0 &lt;&lt; 4) | C byte followed by an extended length byte R. Runs are reset at each
    /// column boundary (each column's runs sum to the height), which the decoder handles because its run
    /// counter reaches 0 exactly at the boundary.
    ///
    /// The walk-behind (z-plane) mask that follows the graphics in the IM00 region is NOT re-encoded; the
    /// caller appends the original mask bytes verbatim (an image edit changes pixels, not the mask).
    /// </summary>
    public static class ScummV2ImageEncoder
    {
        /// <summary>Encodes a [width,height] 4-bit index matrix to the GdiV2 graphics stream (no z-plane).</summary>
        public static byte[] EncodeGraphics(byte[,] matrix, int width, int height)
        {
            var output = new List<byte>(width * height / 2 + 16);
            for (int x = 0; x < width; x++)
            {
                int y = 0;
                while (y < height)
                {
                    int color = matrix[x, y] & 0x0F;
                    int run = 1;
                    while (y + run < height && (matrix[x, y + run] & 0x0F) == color) run++;
                    y += run;

                    while (run > 0)
                    {
                        if (run <= 7)
                        {
                            output.Add((byte)((run << 4) | color)); // inline solid run (high nibble 1-7)
                            run = 0;
                        }
                        else
                        {
                            int ext = Math.Min(run, 255);           // extended: 0-nibble colour byte + length byte
                            output.Add((byte)color);
                            output.Add((byte)ext);
                            run -= ext;
                        }
                    }
                }
            }
            return output.ToArray();
        }

        /// <summary>
        /// Builds the full replacement IM00 content for an edited image: the re-encoded graphics followed
        /// by the original z-plane mask bytes (everything from the graphics end of the original stream to
        /// <paramref name="imageEnd"/>), so the walk-behind mask is preserved byte-for-byte.
        /// </summary>
        public static byte[] EncodeImage(byte[] originalData, int im00Offset, int imageEnd, int width, int height, byte[,] matrix)
        {
            byte[] graphics = EncodeGraphics(matrix, width, height);
            int origGraphicsLen = ScummV2ImageDecoder.GraphicsRleLength(originalData, im00Offset, width, height);
            int zStart = im00Offset + origGraphicsLen;
            int zLen = (imageEnd > zStart && imageEnd <= originalData.Length) ? imageEnd - zStart : 0;

            var result = new byte[graphics.Length + zLen];
            Array.Copy(graphics, 0, result, 0, graphics.Length);
            if (zLen > 0) Array.Copy(originalData, zStart, result, graphics.Length, zLen);
            return result;
        }

        /// <summary>
        /// Builds the replacement IM00 content for an edited WALK-BEHIND MASK: the original graphics bytes
        /// kept verbatim, followed by the re-encoded mask. (An edit to the mask changes the walk-behind,
        /// not the pixels.)
        /// </summary>
        public static byte[] EncodeImageWithMask(byte[] originalData, int im00Offset, int width, int height, byte[,] maskMatrix)
        {
            int origGraphicsLen = ScummV2ImageDecoder.GraphicsRleLength(originalData, im00Offset, width, height);
            byte[] mask = EncodeMask(maskMatrix, width, height);

            var result = new byte[origGraphicsLen + mask.Length];
            Array.Copy(originalData, im00Offset, result, 0, origGraphicsLen);
            Array.Copy(mask, 0, result, origGraphicsLen, mask.Length);
            return result;
        }

        /// <summary>
        /// Re-encodes a 0/1 walk-behind mask matrix into the GdiV2 mask RLE: the strip-major sequence of
        /// per-8-column mask bytes (bit 7 = leftmost), PackBits-style (a repeat run is 0x80|count + the
        /// byte; a literal run is count + that many bytes). The inverse of ScummV2ImageDecoder.DecodeMaskRle.
        /// </summary>
        public static byte[] EncodeMask(byte[,] maskMatrix, int width, int height)
        {
            int numStrips = (width + 7) / 8;
            var seq = new List<byte>(numStrips * height);
            for (int strip = 0; strip < numStrips; strip++)
            {
                for (int row = 0; row < height; row++)
                {
                    int b = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int px = strip * 8 + bit;
                        if (px < width && (maskMatrix[px, row] & 1) != 0) b |= 1 << (7 - bit);
                    }
                    seq.Add((byte)b);
                }
            }

            var output = new List<byte>(seq.Count);
            int i = 0;
            while (i < seq.Count)
            {
                int repeat = 1;
                while (i + repeat < seq.Count && seq[i + repeat] == seq[i] && repeat < 127) repeat++;
                if (repeat >= 2)
                {
                    output.Add((byte)(0x80 | repeat));
                    output.Add(seq[i]);
                    i += repeat;
                }
                else
                {
                    int start = i;
                    var lit = new List<byte>();
                    while (i < seq.Count && lit.Count < 255)
                    {
                        if (i + 1 < seq.Count && seq[i + 1] == seq[i]) break; // a repeat run starts next
                        lit.Add(seq[i]);
                        i++;
                    }
                    if (lit.Count == 0) { lit.Add(seq[start]); i = start + 1; }
                    output.Add((byte)lit.Count);
                    output.AddRange(lit);
                }
            }
            return output.ToArray();
        }
    }
}
