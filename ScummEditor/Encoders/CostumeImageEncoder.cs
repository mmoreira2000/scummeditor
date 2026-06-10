using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using ScummEditor.Exceptions;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    public class CostumeImageEncoder
    {
        CostumeImageData _pictureData;
        private Costume _costume;
        private Bitmap _imageToEncode;

        // Raw costume-relative palette indexes read from the (indexed) source bitmap. The codec
        // stores these directly, so the index is preserved losslessly regardless of duplicate colors.
        private byte[,] _indexMatrix;

        public void Encode(RoomBlock roomBlock, Costume costume, int frameIndex, Bitmap imageToEncode)
        {
            _costume = costume;
            _pictureData = costume.Pictures[frameIndex];
            _imageToEncode = imageToEncode;

            Encode();
        }
        private void Encode()
        {
            if (_imageToEncode.Width != _pictureData.Width || _imageToEncode.Height != _pictureData.Height)
            {
                throw new ImageEncodeException("The image must have the same size as original costume frame");
            }

            if (!IndexedImageHelper.IsIndexed(_imageToEncode))
            {
                throw new ImageEncodeException("The image must be an indexed (palette-based) image so the original palette indexes are preserved. Re-export it from ScummEditor and edit it without converting it to RGB/truecolor.");
            }
            _indexMatrix = IndexedImageHelper.GetIndexMatrix(_imageToEncode);

            var bitStreamManager = new BitStreamManager();
            int colorSize = 0;
            int repetitionCountSize = 0;
            int maxRepetitionCountValue = 0;

            if (_costume.PaletteSize == 16)
            {
                colorSize = 4;
                repetitionCountSize = 4;
                maxRepetitionCountValue = 15; //1111
            }
            else
            {
                colorSize = 5;
                repetitionCountSize = 3;
                maxRepetitionCountValue = 7; //111
            }

            int currentLine = 0;
            int currentColumn = 0;

            byte currentColorPalette = GetRelativeIndexAt(0, 0);
            int repetitionCount = 0;

            /* The compression used for the costume data is a simple byte based RLE compression. 
             * However, it works by columns, not by lines. 
             * Each byte contains the color in the high bits and the repetition count in the low bits. 
             * If the repetition count is 0, then the next byte contains the actual repetition count. 
             * How many bits are used for the color depends on the palette size: for a 16 color palette, 4 bits are used for the color; for 32 colors, 5 bits are used.
             */
            while (!(currentLine == 0 && currentColumn == _pictureData.Width))
            {
                byte newColorPalette = GetRelativeIndexAt(currentColumn, currentLine);
                if (newColorPalette == currentColorPalette)
                {
                    repetitionCount++;
                }
                else
                {
                    while (repetitionCount > 255)
                    {
                        //write the repetition count in the next byte
                        bitStreamManager.AddByte(0, repetitionCountSize);
                        bitStreamManager.AddByte(currentColorPalette, colorSize);
                        bitStreamManager.AddByte(255);
                        repetitionCount -= 255;
                    }

                    if (repetitionCount > maxRepetitionCountValue)
                    {
                        //write the repetition count in the next byte
                        bitStreamManager.AddByte(0, repetitionCountSize);
                        bitStreamManager.AddByte(currentColorPalette, colorSize);

                        bitStreamManager.AddByte((byte)repetitionCount);
                    }
                    else
                    {
                        bitStreamManager.AddByte((byte)repetitionCount, repetitionCountSize);
                        bitStreamManager.AddByte(currentColorPalette, colorSize);
                    }
                    currentColorPalette = newColorPalette;

                    repetitionCount = 1;
                }

                currentLine++;
                if (currentLine == _pictureData.Height)
                {
                    currentLine = 0;
                    currentColumn++;
                }
            }

            if (repetitionCount > 0)
            {
                while (repetitionCount > 255)
                {
                    //write the repetition count in the next byte
                    bitStreamManager.AddByte(0, repetitionCountSize);
                    bitStreamManager.AddByte(currentColorPalette, colorSize);
                    bitStreamManager.AddByte(255);
                    repetitionCount -= 255;
                }

                if (repetitionCount > maxRepetitionCountValue)
                {
                    //write the repetition count in the next byte
                    bitStreamManager.AddByte(0, repetitionCountSize);
                    bitStreamManager.AddByte(currentColorPalette, colorSize);

                    bitStreamManager.AddByte((byte)repetitionCount);
                }
                else
                {
                    bitStreamManager.AddByte((byte)repetitionCount, repetitionCountSize);
                    bitStreamManager.AddByte(currentColorPalette, colorSize);
                }
            }


            _pictureData.ImageData = bitStreamManager.ToByteArray();
        }

        // Returns the raw costume-relative palette index stored at the given pixel of the indexed source.
        private byte GetRelativeIndexAt(int x, int y)
        {
            return _indexMatrix[x, y];
        }
    }
}