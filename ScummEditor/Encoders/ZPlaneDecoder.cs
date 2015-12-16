using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    public class ZPlaneDecoder
    {
        private ushort _width;
        private ushort _height;
        private ZPlane _zPlane;

        private Bitmap _resultBitmap;

        public Bitmap Decode(RoomBlock roomBlock, int objectIndex, int imageIndex, int zPlaneIndex)
        {
            var obj = roomBlock.GetOBIMs()[objectIndex];

            ObjectImageHeader IMHD = obj.GetIMHD();

            _width = IMHD.Width;
            _height = IMHD.Height;
            _zPlane = obj.GetIMxx()[imageIndex].GetZPlanes()[zPlaneIndex];

            Decode();

            return _resultBitmap;
        }

        public Bitmap Decode(RoomBlock roomBlock, int zPlaneIndex)
        {
            var RMHD = roomBlock.GetRMHD();

            _width = RMHD.Width;
            _height = RMHD.Height;
            _zPlane = roomBlock.GetRMIM().GetIM00().GetZPlanes()[zPlaneIndex];

            Decode();

            return _resultBitmap;
        }

        private int _currentLine;
        int _currentColumn;

        public void Decode()
        {
            if (_width == 0 || _height == 0)
            {
                _resultBitmap = null;
                return;
            }

            _resultBitmap = new Bitmap(_width, _height);

            for (int i = 0; i < _zPlane.Strips.Count; i++)
            {
                var strip = _zPlane.Strips[i];

                _currentLine = 0;
                _currentColumn = 0;
                _currentOffset = i * 8;//each strip has 8 pixels width, so with multiply the current strip by 8 to get the proper offset where it should be rendered.

                DecodeZPlaneStrip(strip);
            }
        }

        private BitStreamManager _bitStreamManager;
        private int _currentOffset;

        private void DecodeZPlaneStrip(ZPlaneStripData strip)
        {
            _bitStreamManager = new BitStreamManager(strip.ImageData);
            bool finishDecode = false;

            while (!finishDecode)
            {
                byte count = _bitStreamManager.ReadByte();
                if (count > 0)
                {
                    if (BinaryHelper.CheckBitState(count,7))
                    { // write the same byte count times

                        count = BinaryHelper.GetBitsFromByte(count, 7); // &= 0x7F;
                        byte b = _bitStreamManager.ReadByte();

                        for (int i = 0; i < count; i++)
                        {
                            if (!CheckEndOfGraphics()) DrawLine(b);
                        }
                    }
                    else
                    {  // write count bytes as is from the input

                        for (int i = 0; i < count; i++)
                        {
                            if (!CheckEndOfGraphics()) DrawLine(_bitStreamManager.ReadByte());
                        }
                    }
                }
                //else
                //{
                //    Debugger.Break();
                //}

                if (CheckEndOfGraphics() || _bitStreamManager.EndOfStream) finishDecode = true;
            }
        }

        private bool CheckEndOfGraphics()
        {
            return (_currentColumn == 0 && _currentLine == _height);
        }

        private void DrawLine(byte b)
        {
            Color msk = Color.Empty;
            for (int i = 0; i < 8; i++)
            {
                if (BinaryHelper.CheckBitState(b, 7 - i))
                {
                    msk = Color.Black;
                }
                else
                {
                    msk = Color.White;
                }
                _resultBitmap.SetPixel(_currentOffset + i, _currentLine, msk);
            }
            _currentLine++;
        }

    }
}