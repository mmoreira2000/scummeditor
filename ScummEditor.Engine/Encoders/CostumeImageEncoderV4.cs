using System.Drawing;
using ScummEditor.Exceptions;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Encodes a SCUMM v4 costume frame (CEL) from an indexed bitmap's pixel indexes back to the
    /// column-major RLE the game uses - the exact inverse of CostumeImageDecoderV4 (and the same
    /// scheme as the v5/v6 CostumeImageEncoder). Each token carries a run length in the low bits and
    /// a costume-local colour in the high bits (16 colours = 4/4 bits, 32 = 3/5 bits); a run too long
    /// for the low bits is written as a 0 marker followed by a whole count byte (split at 255). The
    /// pixel bytes ARE the costume-local palette indexes (the exported PNG keeps them), so the
    /// encoding is lossless regardless of duplicate colours.
    /// </summary>
    public class CostumeImageEncoderV4
    {
        /// <summary>Encodes an indexed bitmap; throws if it is not palette-based.</summary>
        public byte[] Encode(Bitmap bitmap, int paletteSize)
        {
            if (!IndexedImageHelper.IsIndexed(bitmap))
            {
                throw new ImageEncodeException("The image must be an indexed (palette-based) PNG so the original costume palette indexes are preserved. Re-export it from ScummEditor and edit it without converting it to RGB/truecolor.");
            }

            byte[,] indexMatrix = IndexedImageHelper.GetIndexMatrix(bitmap);
            return Encode(indexMatrix, bitmap.Width, bitmap.Height, paletteSize);
        }

        /// <summary>
        /// Encodes an indexed bitmap that must match the original frame's size (the v4 costume RLE
        /// re-encode keeps the frame dimensions), throwing if it does not. This mirrors the size
        /// rule the v5/v6 ImageEncoder enforces, so the dimension check lives in the engine.
        /// </summary>
        public byte[] Encode(Bitmap bitmap, int paletteSize, int expectedWidth, int expectedHeight)
        {
            if (bitmap.Width != expectedWidth || bitmap.Height != expectedHeight)
            {
                throw new ImageEncodeException(string.Format(
                    "The frame must be {0}x{1} (the original size), but it is {2}x{3}.",
                    expectedWidth, expectedHeight, bitmap.Width, bitmap.Height));
            }

            return Encode(bitmap, paletteSize);
        }

        public byte[] Encode(byte[,] indexMatrix, int width, int height, int paletteSize)
        {
            int colorSize = paletteSize == 16 ? 4 : 5;
            int repetitionCountSize = paletteSize == 16 ? 4 : 3;
            int maxRepetitionCountValue = paletteSize == 16 ? 15 : 7;

            var bitStream = new BitStreamManager();

            int currentColumn = 0;
            int currentLine = 0;
            byte currentColor = indexMatrix[0, 0];
            int repetitionCount = 0;

            // Walk the image column by column (top to bottom, then the next column), emitting a run
            // whenever the colour changes - exactly the order CostumeImageDecoderV4 reads it back.
            while (!(currentLine == 0 && currentColumn == width))
            {
                byte color = indexMatrix[currentColumn, currentLine];
                if (color == currentColor)
                {
                    repetitionCount++;
                }
                else
                {
                    WriteRun(bitStream, currentColor, repetitionCount, colorSize, repetitionCountSize, maxRepetitionCountValue);
                    currentColor = color;
                    repetitionCount = 1;
                }

                currentLine++;
                if (currentLine == height)
                {
                    currentLine = 0;
                    currentColumn++;
                }
            }

            if (repetitionCount > 0)
            {
                WriteRun(bitStream, currentColor, repetitionCount, colorSize, repetitionCountSize, maxRepetitionCountValue);
            }

            return bitStream.ToByteArray();
        }

        private static void WriteRun(BitStreamManager bitStream, byte color, int count, int colorSize, int repetitionCountSize, int maxRepetitionCountValue)
        {
            // A run longer than one count byte is split into 255-pixel chunks (the count byte is an
            // unsigned 8-bit value), each written in the "0 marker + colour + count byte" long form.
            while (count > 255)
            {
                bitStream.AddByte(0, repetitionCountSize);
                bitStream.AddByte(color, colorSize);
                bitStream.AddByte(255);
                count -= 255;
            }

            if (count > maxRepetitionCountValue)
            {
                // Long form: a 0 in the low bits says "the real count is the next whole byte".
                bitStream.AddByte(0, repetitionCountSize);
                bitStream.AddByte(color, colorSize);
                bitStream.AddByte((byte)count);
            }
            else
            {
                // Short form: the count fits in the low bits next to the colour.
                bitStream.AddByte((byte)count, repetitionCountSize);
                bitStream.AddByte(color, colorSize);
            }
        }
    }
}
