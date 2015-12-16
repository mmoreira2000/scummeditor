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
        private PaletteData _palette;

        public int PaletteIndex { get; set; }
        public CostumeImageEncoder()
        {
            PaletteIndex = 0;
        }

        public void Encode(RoomBlock roomBlock, Costume costume, int frameIndex, Bitmap imageToEncode)
        {
            _costume = costume;
            _pictureData = costume.Pictures[frameIndex];
            _imageToEncode = imageToEncode;
            if (PaletteIndex == 0)
            {
                _palette = roomBlock.GetDefaultPalette();
            }
            else
            {
                _palette = roomBlock.GetPALS().GetWRAP().GetAPALs()[PaletteIndex];
            }

            Encode();
        }
        private void Encode()
        {
            if (_imageToEncode.Width != _pictureData.Width || _imageToEncode.Height != _pictureData.Height)
            {
                throw new ImageEncodeException("The image must have the same size as original costume frame");
            }

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

            Color currentColor = _imageToEncode.GetPixel(0, 0);
            byte currentColorPalette = GetRelativePaletteIndex(currentColor);
            int repetitionCount = 0;

            /* The compression used for the costume data is a simple byte based RLE compression. 
             * However, it works by columns, not by lines. 
             * Each byte contains the color in the high bits and the repetition count in the low bits. 
             * If the repetition count is 0, then the next byte contains the actual repetition count. 
             * How many bits are used for the color depends on the palette size: for a 16 color palette, 4 bits are used for the color; for 32 colors, 5 bits are used.
             */
            while (!(currentLine == 0 && currentColumn == _pictureData.Width))
            {
                Color newColor = _imageToEncode.GetPixel(currentColumn, currentLine);
                if (newColor == currentColor)
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
                    currentColor = newColor;
                    currentColorPalette = GetRelativePaletteIndex(currentColor);

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

        private byte GetRelativePaletteIndex(Color color)
        {
            //The same color can appear more than one time in the palette index.
            //Because we don't know what absolute index the relative index refers (usually the last absolute index),
            //we will need to verify all indexes.
            var absolutePaletteIndexes = new List<int>();

            for (int i = 0; i < _palette.Colors.Length; i++)
            {
                var colorCheck = _palette.Colors[i];
                if (colorCheck.R == color.R &&
                    colorCheck.G == color.G &&
                    colorCheck.B == color.B)
                {
                    absolutePaletteIndexes.Add(i);
                }
            }

            if (absolutePaletteIndexes.Count == 0)
            {
                throw new ImageEncodeException(string.Format("This color (R:{0}, G:{1}, B:{2}) does not exist in the palette.", color.R, color.G, color.B));
            }

            foreach (int absolutePaletteIndex in absolutePaletteIndexes)
            {
                for (int i = 0; i < _costume.Palette.Count; i++)
                {
                    if (_costume.Palette[i] == absolutePaletteIndex)
                    {
                        return (byte)i;
                    }
                }
            }

            throw new ImageEncodeException(string.Format("This color (R:{0}, G:{1}, B:{2}) does exist in the palette, but it's not in the costume palette.", color.R, color.G, color.B));
        }
    }
}