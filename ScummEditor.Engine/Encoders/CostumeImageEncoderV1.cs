using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Engine.Exceptions;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes a SCUMM v1 (format 0x57) costume frame from an indexed bitmap back to the C64 2-bit RLE -
    /// the inverse of <see cref="CostumeImageDecoderV1"/>. The bitmap must be an indexed PNG whose pixel
    /// indexes are the 0..3 colour values (as exported), so the encoding is lossless. Each 8-pixel strip
    /// stores 4 two-bit samples (the left pixel of each doubled pair); samples are packed column-strip-major
    /// and RLE-compressed (a run byte with bit 0x80 set repeats one following colour byte, otherwise it is a
    /// literal run of that many colour bytes).
    /// </summary>
    public class CostumeImageEncoderV1
    {
        /// <summary>Encodes an indexed bitmap that must match the original frame's size; throws otherwise.</summary>
        public byte[] Encode(Bitmap bitmap, int expectedWidth, int expectedHeight)
        {
            if (!IndexedImageHelper.IsIndexed(bitmap))
            {
                throw new ImageEncodeException("The image must be an indexed (palette-based) PNG so the 4-colour costume indexes are preserved. Re-export it from ScummEditor and edit it without converting it to RGB/truecolor.");
            }
            if (bitmap.Width != expectedWidth || bitmap.Height != expectedHeight)
            {
                throw new ImageEncodeException(string.Format(
                    "The frame must be {0}x{1} (the original size), but it is {2}x{3}.",
                    expectedWidth, expectedHeight, bitmap.Width, bitmap.Height));
            }

            return Encode(IndexedImageHelper.GetIndexMatrix(bitmap));
        }

        /// <summary>Encodes a width x height index matrix (values 0..3) to the C64 costume RLE bytes.</summary>
        public byte[] Encode(byte[,] matrix)
        {
            int width = matrix.GetLength(0), height = matrix.GetLength(1);
            int widthBytes = width / 8;
            var samples = new byte[widthBytes * height];
            for (int strip = 0; strip < widthBytes; strip++)
            {
                for (int y = 0; y < height; y++)
                {
                    int b = 0;
                    for (int pair = 0; pair < 4; pair++)
                    {
                        int v = matrix[strip * 8 + pair * 2, y] & 3; // left pixel of each doubled pair
                        b |= v << (pair * 2);
                    }
                    samples[strip * height + y] = (byte)b;
                }
            }
            return RleEncode(samples);
        }

        private static byte[] RleEncode(byte[] samples)
        {
            var output = new List<byte>();
            int i = 0, n = samples.Length;
            while (i < n)
            {
                int run = 1;
                while (i + run < n && samples[i + run] == samples[i] && run < 127) run++;
                if (run >= 2)
                {
                    output.Add((byte)(0x80 | run)); // repeat run
                    output.Add(samples[i]);
                    i += run;
                }
                else
                {
                    int litStart = i, lit = 0;
                    while (i < n && lit < 127 && !(i + 1 < n && samples[i + 1] == samples[i]))
                    {
                        i++; lit++;
                    }
                    output.Add((byte)lit); // literal run
                    for (int k = 0; k < lit; k++) output.Add(samples[litStart + k]);
                }
            }
            return output.ToArray();
        }
    }
}
