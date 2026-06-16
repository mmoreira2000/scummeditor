using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes a SCUMM v4 costume frame (CEL) to a bitmap. The RLE is the same column-major scheme the
    /// v5/v6 CostumeImageDecoder uses (color in the high bits, run in the low bits; run 0 => the next
    /// byte is the run; 16 colours = 4/4 bits, 32 colours = 5/3 bits), but the palette is resolved the
    /// v4 way and passed in as an already-mapped Color[] (room PA for VGA, the 16-colour EGA table for
    /// EGA), so this does not depend on the v5/v6 RoomBlock palette navigation.
    /// </summary>
    public class CostumeImageDecoderV4
    {
        /// <param name="localPalette">PaletteSize colours: localPalette[i] is the real colour for costume-local index i.</param>
        public Bitmap Decode(CostumeImageData picture, int paletteSize, Color[] localPalette, bool useTransparentColor)
        {
            if (picture.Width == 0 || picture.Height == 0)
            {
                return null;
            }

            int colorSize = paletteSize == 16 ? 4 : 5;
            int repetitionCountSize = paletteSize == 16 ? 4 : 3;
            int transparentIndex = useTransparentColor ? 0 : -1;

            var indexMatrix = new byte[picture.Width, picture.Height];
            if (picture.ImageData == null || picture.ImageData.Length == 0)
            {
                return IndexedImageHelper.FromIndexMatrix(indexMatrix, localPalette, transparentIndex);
            }

            var bitStreamManager = new BitStreamManager(picture.ImageData);
            int currentLine = 0;
            int currentColumn = 0;
            bool finishDecode = false;
            while (!finishDecode)
            {
                int repetitionCount = bitStreamManager.ReadValue(repetitionCountSize);
                byte paletteIndex = bitStreamManager.ReadValue(colorSize);
                if (repetitionCount == 0 && bitStreamManager.Position != bitStreamManager.Lenght)
                {
                    repetitionCount = bitStreamManager.ReadByte();
                }

                for (int i = 0; i < repetitionCount; i++)
                {
                    indexMatrix[currentColumn, currentLine] = paletteIndex;
                    currentLine++;
                    if (currentLine == picture.Height)
                    {
                        currentLine = 0;
                        currentColumn++;
                    }
                    if (currentColumn == picture.Width) { finishDecode = true; break; } // filled every column
                }

                if ((currentColumn == picture.Width && currentLine == 0) || bitStreamManager.EndOfStream)
                {
                    finishDecode = true;
                }
            }

            return IndexedImageHelper.FromIndexMatrix(indexMatrix, localPalette, transparentIndex);
        }
    }
}
