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
        private List<ZPlaneStripData> _strips;

        private Bitmap _resultBitmap;

        public Bitmap Decode(RoomBlock roomBlock, int objectIndex, int imageIndex, int zPlaneIndex)
        {
            var obj = roomBlock.GetOBIMs()[objectIndex];

            ObjectImageHeader IMHD = obj.GetIMHD();

            _width = IMHD.Width;
            _height = IMHD.Height;
            _strips = obj.GetIMxx()[imageIndex].GetZPlanes()[zPlaneIndex].Strips;

            Decode();

            return _resultBitmap;
        }

        public Bitmap Decode(RoomBlock roomBlock, int zPlaneIndex)
        {
            var RMHD = roomBlock.GetRMHD();

            _width = RMHD.Width;
            _height = RMHD.Height;
            _strips = roomBlock.GetRMIM().GetIM00().GetZPlanes()[zPlaneIndex].Strips;

            Decode();

            return _resultBitmap;
        }

        /// <summary>
        /// Decodes a z-plane mask directly from its strips, without the v5/v6 room navigation. Used
        /// by the SCUMM v4 path, whose z-planes are embedded in the BM/OI block rather than in ZPnn
        /// sub-blocks. A strip with no data is a "full of 0" mask (drawn as unmasked / white).
        /// </summary>
        public Bitmap Decode(List<ZPlaneStripData> strips, int width, int height)
        {
            _strips = strips;
            _width = (ushort)width;
            _height = (ushort)height;
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

            for (int i = 0; i < _strips.Count; i++)
            {
                var strip = _strips[i];

                _currentLine = 0;
                _currentColumn = 0;
                _currentOffset = i * 8;//each strip has 8 pixels width, so with multiply the current strip by 8 to get the proper offset where it should be rendered.

                if (strip.ImageData == null || strip.ImageData.Length == 0)
                {
                    // "Stripe full of 0": no mask here, i.e. fully unmasked (white).
                    for (int line = 0; line < _height; line++) DrawLine(0);
                    continue;
                }

                DecodeZPlaneStrip(strip);
            }
        }

        private BitStreamManager _bitStreamManager;
        private int _currentOffset;

        private void DecodeZPlaneStrip(ZPlaneStripData strip)
        {
            _bitStreamManager = new BitStreamManager(strip.ImageData);

            // Mirrors ScummVM's decompressMaskImg. The run is a do/while loop there, so a stored
            // count of 0 means 256 (the byte underflows) - that lets a single control byte fill a
            // tall strip. The loop stops at the strip height (or if the data runs out).
            while (!CheckEndOfGraphics() && !_bitStreamManager.EndOfStream)
            {
                byte control = _bitStreamManager.ReadByte();
                if (BinaryHelper.CheckBitState(control, 7))
                {
                    // Repeat one line `run` times.
                    int run = BinaryHelper.GetBitsFromByte(control, 7); // &= 0x7F
                    if (run == 0) run = 256;
                    if (_bitStreamManager.EndOfStream) break; // truncated: no line byte follows
                    byte line = _bitStreamManager.ReadByte();
                    for (int i = 0; i < run && !CheckEndOfGraphics(); i++)
                    {
                        DrawLine(line);
                    }
                }
                else
                {
                    // Copy `run` distinct lines from the stream.
                    int run = control;
                    if (run == 0) run = 256;
                    for (int i = 0; i < run && !CheckEndOfGraphics() && !_bitStreamManager.EndOfStream; i++)
                    {
                        DrawLine(_bitStreamManager.ReadByte());
                    }
                }
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