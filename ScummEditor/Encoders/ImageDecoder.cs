using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    public class ImageDecoder
    {
        private ushort _width;
        private ushort _height;
        private byte _transparency;
        private PaletteData _pallete;
        private List<StripData> _strips;

        private Bitmap _resultBitmap;

        public List<int> UsedIndexes { get; private set; }

        public bool UseTransparentColor { get; set; }
        public int PaletteIndex { get; set; }
        public ImageDecoder()
        {
            PaletteIndex = 0;
        }

        public Bitmap Decode(RoomBlock roomBlock, int objectIndex, int imageIndex)
        {
            var obj = roomBlock.GetOBIMs()[objectIndex];

            if (obj.GetIMxx()[imageIndex].GetSMAP() == null) return new Bitmap(1, 1);

            var IMHD = obj.GetIMHD();

            _width = IMHD.Width;
            _height = IMHD.Height;
            _transparency = roomBlock.GetTRNS().Value;
            _strips = obj.GetIMxx()[imageIndex].GetSMAP().Strips;

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

        public Bitmap Decode(RoomBlock roomBlock)
        {
            var RMHD = roomBlock.GetRMHD();

            _width = RMHD.Width;
            _height = RMHD.Height;
            _transparency = roomBlock.GetTRNS().Value;
            _strips = roomBlock.GetRMIM().GetIM00().GetSMAP().Strips;

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

        private int _currentLine;
        int _currentColumn;
        private RenderingDirections _renderdingDirection;

        public void Decode()
        {
            UsedIndexes = new List<int>();

            if (_width == 0 || _height == 0)
            {
                _resultBitmap = null;
                return;
            }

            _resultBitmap = new Bitmap(_width, _height);

            for (int i = 0; i < _strips.Count; i++)
            {
                var strip = _strips[i];

                _currentLine = 0;
                _currentColumn = 0;
                _renderdingDirection = strip.RenderdingDirection;
                _currentOffset = i * 8;//each strip has 8 pixels width, so with multiply the current strip by 8 to get the proper offset where it should be rendered.

                if (strip.CompressionType == CompressionTypes.Method1 || strip.CompressionType == CompressionTypes.Method2)
                {
                    DecodeCompressed(strip);
                }
                else if (strip.CompressionType == CompressionTypes.Uncompressed)
                {
                    DecodeUncompressed(strip);
                }
            }
        }

        private int _numBitPerPaletteEntry;
        private int _substrationVariable;
        private BitStreamManager _bitStreamManager;
        private int _paletteIndex;
        private Color _currentColor;
        private int _currentOffset;

        private void DecodeCompressed(StripData strip)
        {
            _numBitPerPaletteEntry = strip.CodecId - strip.ParamSubtraction;
            _substrationVariable = 1;
            _bitStreamManager = new BitStreamManager(strip.ImageData);

            _currentLine = 0;
            _currentColumn = 0;
            bool finishDecode = false;

            //if (strip.CodecId >= 0x54 && strip.CodecId <= 0x58) Debugger.Break();
            //if (strip.CodecId >= 0x40 && strip.CodecId <= 0x44) Debugger.Break();

            //Read palette and Draw the first pixel.
            _paletteIndex = _bitStreamManager.ReadByte();

            SetCurrentColor();

            _resultBitmap.SetPixel(_currentOffset + _currentColumn, _currentLine, _currentColor);

            while (!finishDecode)
            {

                var changeAction = _bitStreamManager.ReadBit();
                //changeAction = false (0): Draw next pixel with current palette index. 
                //                          Otherwise changeAction is true and we need the next bit to decide what to do.

                //changeAction = true (1): We need the next bit to discover what to do.
                if (changeAction)
                {
                    if (strip.CompressionType == CompressionTypes.Method1)
                    {
                        DecodeCode1Specifics();
                    }
                    else if (strip.CompressionType == CompressionTypes.Method2)
                    {
                        DecodeCode2Specifics();
                    }
                }
                else
                {
                    DrawNextPixel();
                }


                if ((_currentColumn == 7 && _currentLine == (_height - 1)) || _bitStreamManager.EndOfStream) finishDecode = true;
            }

            UsedIndexes.Sort();
        }

        private void SetCurrentColor()
        {
            if (!UsedIndexes.Contains(_paletteIndex)) UsedIndexes.Add(_paletteIndex);

            _currentColor = _pallete.Colors[_paletteIndex];
            if ((_paletteIndex == _transparency) && UseTransparentColor)
            {
                _currentColor = Color.FromArgb(0, _currentColor.R, _currentColor.G, _currentColor.B);
            }
        }

        private void DecodeCode1Specifics()
        {
            // Decoder specific actions. If we are here, is because the previous read bit by DecodeCompressed was True (1).

            var nextBitCode = _bitStreamManager.ReadBit();
            if (!nextBitCode) // next bit is false (0). 
            {
                //previous bit was false (0). So now we have a code 10 (1 previous bit and 0 from this one).
                //10: Read a new palette index from the bit stream, i.e., read the number of bits that the parameter 
                //    specifies as a value (see the Tiny Bits of Decompression chapter).
                //    Set the subtraction variable to 1.
                //    Draw the next pixel.
                _paletteIndex = _bitStreamManager.ReadValue(_numBitPerPaletteEntry);
                _substrationVariable = 1;
                SetCurrentColor();
            }
            else
            {
                //previous bit was true (1). So now we have a code 11 (1 previous bit and 1 from this one).
                //11 alone is not enough. We will need the next bit to know what to do.
                nextBitCode = _bitStreamManager.ReadBit();
                if (!nextBitCode)
                {
                    //previous bit was false. Now we have a code 110 (1 from first bit, 1 from second and 0 from the last read.)
                    //110: Subtract the subtraction variable from the palette index.
                    //     Draw the next pixel.
                    _paletteIndex -= _substrationVariable;
                    SetCurrentColor();
                }
                else
                {
                    //previous bit was true. Now we have a code 111 (1 from first bit, 1 from second and 1 from the last read.)
                    //111: Negate the subtraction variable (i.e., if it's 1, change it to -1, if it's -1, change it to 1). 
                    //     Subtract it from the palette index.
                    //     Draw the next pixel.
                    _substrationVariable = _substrationVariable * -1;
                    _paletteIndex -= _substrationVariable;
                    SetCurrentColor();
                }
            }

            DrawNextPixel();
        }

        private void DecodeCode2Specifics()
        {
            // Decoder specific actions. If we are here, is because the previous read bit by DecodeCompressed was True (1).

            /*
            10: Read a new palette index from the bitstream (i.e., the number of bits specified by the parameter), and draw the next pixel.
            11: Read the next 3 bit value, and perform an action, depending on the value:
                   000 (0): Decrease current palette index by 4.
                   001 (1): Decrease current palette index by 3.
                   010 (2): Decrease current palette index by 2.
                   011 (3): Decrease current palette index by 1.
                   100 (4): Read next 8 bits. Draw the number of pixels specified by these 8 bits with the current palette index (somewhat similar to RLE).
                   101 (5): Increase current palette index by 1.
                   110 (6): Increase current palette index by 2.
                   111 (7): Increase current palette index by 3. 
            */

            var nextBitCode = _bitStreamManager.ReadBit();
            if (!nextBitCode) // last bit is false (0). 
            {
                //last read bit is false (0). So now we have a code 10 (1 previous bit and 0 from this one).
                //10: Read a new palette index from the bit stream, i.e., read the number of bits that the parameter 
                //    specifies as a value (see the Tiny Bits of Decompression chapter).
                //    Set the subtraction variable to 1.
                //    Draw the next pixel.
                _paletteIndex = _bitStreamManager.ReadValue(_numBitPerPaletteEntry);
                SetCurrentColor();
            }
            else // last bit is true (1). 
            {
                //11: Read the next 3 bit value, and perform an action, depending on the value:
                byte nextValue = _bitStreamManager.ReadValue(3);
                switch (nextValue)
                {
                    case 0: //000: Decrease current palette index by 4.
                        _paletteIndex -= 4;
                        SetCurrentColor(); break;
                    case 1: //001: Decrease current palette index by 3.
                        _paletteIndex -= 3;
                        SetCurrentColor(); break;
                    case 2: //010: Decrease current palette index by 2.
                        _paletteIndex -= 2;
                        SetCurrentColor(); break;
                    case 3: //011: Decrease current palette index by 1.
                        _paletteIndex -= 1;
                        SetCurrentColor(); break;
                    case 4: //100: Read next 8 bits. 
                        //Draw the number of pixels specified by these 8 bits with the current palette index (somewhat similar to RLE).
                        var numPixels = _bitStreamManager.ReadByte();
                        for (int i = 0; i < numPixels; i++)
                        {
                            //if (!((_currentColumn == 7 && _currentLine == (_height - 1))))
                            {
                                DrawNextPixel();
                            }
                        }
                        return;
                    case 5: //101: Increase current palette index by 1.
                        _paletteIndex += 1;
                        SetCurrentColor(); break;
                    case 6: //110: Increase current palette index by 2.
                        _paletteIndex += 2;
                        SetCurrentColor(); break;
                    case 7: //111: Increase current palette index by 3. 
                        _paletteIndex += 3;
                        SetCurrentColor(); break;
                    default:
                        Debugger.Break();
                        break;
                }
            }
            DrawNextPixel();
        }

        private void DecodeUncompressed(StripData strip)
        {
            _bitStreamManager = new BitStreamManager(strip.ImageData);

            bool finishDecode = false;
            _paletteIndex = _bitStreamManager.ReadByte();
            SetCurrentColor();
            _resultBitmap.SetPixel(_currentOffset, 0, _currentColor);

            while (!finishDecode)
            {
                _paletteIndex = _bitStreamManager.ReadByte();
                SetCurrentColor();
                DrawNextPixel();
                if ((_currentColumn == 7 && _currentLine == (_height - 1)) || _bitStreamManager.EndOfStream) finishDecode = true;
            }
        }

        private void DrawNextPixel()
        {
            if (_renderdingDirection == RenderingDirections.Horizontal)
            {
                _currentColumn++;
                if (_currentColumn == 8)
                {
                    _currentColumn = 0;
                    _currentLine++;
                }
            }
            else
            {
                _currentLine++;
                if (_currentLine == _height)
                {
                    _currentLine = 0;
                    _currentColumn++;
                }
            }
            _resultBitmap.SetPixel(_currentOffset + _currentColumn, _currentLine, _currentColor);
        }

    }
}

