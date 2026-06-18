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
    }
}
