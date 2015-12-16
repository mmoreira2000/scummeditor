using System;
using System.Drawing;
using System.Linq;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    public class CostumeImageDecoder
    {
        CostumeImageData _pictureData;
        private Costume _costume;

        private Bitmap _resultBitmap;
        public bool UseTransparentColor { get; set; }

        private PaletteData _palette;

        public int PaletteIndex { get; set; }
        public CostumeImageDecoder()
        {
            PaletteIndex = 0;
        }

        public Bitmap Decode(RoomBlock roomBlock, Costume costume, int frameIndex)
        {
            _pictureData = costume.Pictures[frameIndex];
            _costume = costume;

            if (PaletteIndex == 0)
            {
                _palette = roomBlock.GetDefaultPalette();
            }
            else
            {
                _palette = roomBlock.GetPALS().GetWRAP().GetAPALs()[PaletteIndex];
            }

            Decode();

            return _resultBitmap;
        }


        public void Decode()
        {
            /* The compression used for the costume data is a simple byte based RLE compression. 
             * However, it works by columns, not by lines. 
             * Each byte contains the color in the high bits and the repetition count in the low bits. 
             * If the repetition count is 0, then the next byte contains the actual repetition count. 
             * How many bits are used for the color depends on the palette size: for a 16 color palette, 4 bits are used for the color; for 32 colors, 5 bits are used.
             */

            var bitStreamManager = new BitStreamManager(_pictureData.ImageData);
            int colorSize = 0;
            int repetitionCountSize = 0;

            if (_costume.PaletteSize == 16)
            {
                colorSize = 4;
                repetitionCountSize = 4;
            }
            else
            {
                colorSize = 5;
                repetitionCountSize = 3;
            }

            if (_pictureData.Width == 0 || _pictureData.Height == 0)
            {
                _resultBitmap = null;
                return;
            }

            _resultBitmap = new Bitmap(_pictureData.Width, _pictureData.Height);

            if (_pictureData.ImageData.Length == 0
                || (_pictureData.ImageData.Length == 1 && _pictureData.ImageData[0] == 0)) return; //Algumas imagens são vazias!!

            /*
                while(1)
                      {
                        rep = read_byte();
                        color = rep >> shift;
                        rep &= mask;
                        if(!rep)
                          rep = read_byte();
                        while(rep > 0) {
                          set_pixel(x,y,color);
                          rep--;
                          y++;
                          if(y >= height) {
                            y = 0;
                            x++;
                            if(x >= width) break;
                          }
                        }
                      }

             */
            bool finishDecode = false;
            int currentLine = 0;
            int currentColumn = 0;
            while (!finishDecode)
            {
                //if (currentColumn == _pictureData.Width -1 && currentLine == _pictureData.Height -1) Debugger.Break();

                int repetitionCount = bitStreamManager.ReadValue(repetitionCountSize);
                byte paletteIndex = bitStreamManager.ReadValue(colorSize);

                //The second conditiong wad added because of a bug when decoding DOTT, ROOM 65, Costume 001, Frame 7. This tweak solves.
                if (repetitionCount == 0 && bitStreamManager.Position != bitStreamManager.Lenght)
                {
                    repetitionCount = bitStreamManager.ReadByte();
                }

                SetCurrentColor(paletteIndex);
                for (int i = 0; i < repetitionCount; i++)
                {
                    _resultBitmap.SetPixel(currentColumn, currentLine, _currentColor);
                    currentLine++;
                    if (currentLine == _pictureData.Height)
                    {
                        currentLine = 0;
                        currentColumn++;
                    }
                }
                if ((currentColumn == _pictureData.Width && currentLine == 0) || bitStreamManager.EndOfStream) finishDecode = true;
            }
        }

        private Color _currentColor;

        private void SetCurrentColor(int paletteRelativeIndex)
        {
            int paletteRealIndex = _costume.Palette[paletteRelativeIndex];
            _currentColor = _palette.Colors[paletteRealIndex];

            if ((paletteRelativeIndex == 0) && UseTransparentColor)
            {
                _currentColor = Color.FromArgb(0, _currentColor.R, _currentColor.G, _currentColor.B);
            }
        }
    }
}