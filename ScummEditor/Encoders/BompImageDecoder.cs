using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    public class BompImageDecoder
    {
        private ushort _width;
        private ushort _height;
        private PaletteData _pallete;
        private ImageBomp _imageData;

        private Bitmap _resultBitmap;

        public bool UseTransparentColor { get; set; }
        public int PaletteIndex { get; set; }

        public List<int> UsedIndexes { get; private set; }

        public BompImageDecoder()
        {
            PaletteIndex = 0;
        }

        public Bitmap Decode(RoomBlock roomBlock, int objectIndex, int imageIndex)
        {
            ObjectImage obj = roomBlock.GetOBIMs()[objectIndex];

            if (obj.GetIMxx()[imageIndex].GetBOMP() == null) return new Bitmap(1, 1);

            var IMHD = obj.GetIMHD();
            _width = IMHD.Width;
            _height = IMHD.Height;
            _imageData = obj.GetIMxx()[imageIndex].GetBOMP();

            if (PaletteIndex == 0)
            {
                _pallete = roomBlock.GetDefaultPalette();
            }
            else
            {
                _pallete = roomBlock.GetPALS().GetWRAP().GetAPALs()[PaletteIndex];
            }

            Decode();

            return _resultBitmap;
        }

        public void Decode()
        {
            UsedIndexes = new List<int>();

            _currentLine = 0;
            _currentColumn = 0;

            var bitStreamManager = new BitStreamManager(_imageData.Data);

            _resultBitmap = new Bitmap(_width, _height);

            //if (_pictureData.ImageData.Length == 0
            //    || (_pictureData.ImageData.Length == 1 && _pictureData.ImageData[0] == 0)) return; //Algumas imagens são vazias!!

            /*
             Each line start with a 16le storing the size of the encoded line (without the size header itself) followed by the RLE data.
              lines
                encoded size : 16le
                line data    : size bytes
            */
            bool finishDecode = false;
            while (!finishDecode)
            {
                uint lineSize = bitStreamManager.ReadUInt16();
                int streamPosition = bitStreamManager.Position;

                while ((bitStreamManager.Position - streamPosition) < (lineSize * 8))
                {
                    bool repeatSameColor = bitStreamManager.ReadBit();
                    int count = bitStreamManager.ReadValue(7) + 1;

                    if (count > _width) count = _width;

                    if (repeatSameColor)
                    {
                        byte colorIndex = bitStreamManager.ReadByte();
                        var color = GetColor(colorIndex);
                        for (int j = 0; j < count; j++)
                        {
                            DrawNextPixel(color);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < count; j++)
                        {
                            byte colorIndex = bitStreamManager.ReadByte();
                            var color = GetColor(colorIndex);
                            DrawNextPixel(color);
                        }
                    }

                    if ((_currentColumn == 0 && _currentLine == _imageData.Height) || bitStreamManager.EndOfStream) finishDecode = true;
                }
            }

            UsedIndexes.Sort();
        }

        private Color GetColor(int paletteIndex)
        {
            if (!UsedIndexes.Contains(paletteIndex)) UsedIndexes.Add(paletteIndex);

            var color = _pallete.Colors[paletteIndex];
            if ((paletteIndex == 255) && UseTransparentColor)
            {
                color = Color.FromArgb(0, color.R, color.G, color.B);
            }
            return color;
        }

        private int _currentColumn;
        private int _currentLine;
        private void DrawNextPixel(Color color)
        {
            if (_currentColumn == _resultBitmap.Width || _currentLine == _resultBitmap.Height) Debugger.Break();

            _resultBitmap.SetPixel(_currentColumn, _currentLine, color);
            _currentColumn++;
            if (_currentColumn == _width)
            {
                _currentColumn = 0;
                _currentLine++;
            }
        }

    }
}