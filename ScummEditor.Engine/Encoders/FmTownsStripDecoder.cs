using System.Drawing;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes SCUMM v3 "GF_OLD256" room/object images (Indy3 VGA, Zak/Indy3 FM-Towns). These use the
    /// v4-style VGA strip table (smapLen:LE32, then numStrips x LE32 offsets, then per-strip a codec
    /// byte + data), but the per-strip codecs are the FM-Towns set 1/2/3/4/7 (raw256 + unkDecode8/9/
    /// 10/11) that the v5/v6 ImageDecoder does not implement. Ports of ScummVM Gdi::drawStripRaw /
    /// unkDecode8..11 (gfx.cpp): every codec writes column-major (top-to-bottom, then the next column)
    /// into an 8-pixel-wide strip, and v3 uses an identity room palette so the decoded byte IS the
    /// 256-colour index. Offsets are relative to baseIndex (the smapLen word).
    /// </summary>
    public class FmTownsStripDecoder
    {
        private byte[] _src;
        private int _srcEnd;
        private int _srcPos;

        // READ_BIT_256 bit reader state (LSB-first within each byte).
        private int _mask = 128;
        private int _buffer;

        // Column-major write cursor within the current 8-wide strip.
        private byte[,] _matrix;
        private int _x0;
        private int _height;
        private int _col;
        private int _row;
        private bool _done;

        /// <summary>True when the per-strip codec id is one this decoder handles (1/2/3/4/7).</summary>
        public static bool IsFmTownsCodec(int codecId)
        {
            return codecId == 1 || codecId == 2 || codecId == 3 || codecId == 4 || codecId == 7;
        }

        /// <summary>
        /// Decodes an FM-Towns/256 strip-table image from a byte buffer. The strip table starts at
        /// baseIndex (smapLen:LE32, then LE32 offsets). Returns null if the table/strips fall outside
        /// the buffer.
        /// </summary>
        public Bitmap DecodeImage(byte[] body, int baseIndex, int width, int height, Color[] paletteColors)
        {
            if (body == null || paletteColors == null || width <= 0 || height <= 0)
            {
                return null;
            }

            int numStrips = width / 8;
            long tableEnd = (long)baseIndex + 4 + (long)numStrips * 4;
            if (numStrips <= 0 || baseIndex < 0 || body.Length < tableEnd)
            {
                return null;
            }

            int smapLen = ReadU32(body, baseIndex);
            if (smapLen <= 0 || baseIndex + smapLen > body.Length)
            {
                return null;
            }

            var offsets = new int[numStrips];
            for (int n = 0; n < numStrips; n++)
            {
                offsets[n] = ReadU32(body, baseIndex + 4 + n * 4);
            }

            var matrix = new byte[width, height];
            for (int n = 0; n < numStrips; n++)
            {
                int start = offsets[n];
                int end = (n < numStrips - 1) ? offsets[n + 1] : smapLen;
                if (start < 0 || end > smapLen || end <= start)
                {
                    return null;
                }

                int codecPosition = baseIndex + start;
                byte codec = body[codecPosition];
                DecodeStrip(codec, body, codecPosition + 1, baseIndex + end, matrix, n * 8, height);
            }

            return IndexedImageHelper.FromIndexMatrix(matrix, paletteColors, -1);
        }

        private void DecodeStrip(byte codec, byte[] src, int dataStart, int dataEnd, byte[,] matrix, int x0, int height)
        {
            _src = src;
            _srcPos = dataStart;
            _srcEnd = dataEnd;
            _mask = 128;
            _buffer = 0;
            _matrix = matrix;
            _x0 = x0;
            _height = height;
            _col = 0;
            _row = 0;
            _done = false;

            switch (codec)
            {
                case 1: Raw256(); break;          // drawStripRaw (GF_OLD256)
                case 2: UnkDecode8(); break;
                case 3: UnkDecode9(); break;
                case 4: UnkDecode10(); break;
                case 7: UnkDecode11(); break;
                default: break;                   // unknown codec: leave the strip blank
            }
        }

        /// <summary>Writes one pixel and advances column-major (down a column, then the next column).</summary>
        private void Emit(int value)
        {
            if (_done)
            {
                return;
            }
            _matrix[_x0 + _col, _row] = (byte)value;
            _row++;
            if (_row == _height)
            {
                _row = 0;
                _col++;
                if (_col == 8)
                {
                    _done = true;
                }
            }
        }

        private int ReadByte()
        {
            return _srcPos < _srcEnd ? _src[_srcPos++] : 0;
        }

        // READ_BIT_256: mask <<= 1; on overflow reload the buffer; return the masked bit.
        private int ReadBit()
        {
            _mask <<= 1;
            if (_mask == 256)
            {
                _buffer = _srcPos < _srcEnd ? _src[_srcPos++] : 0;
                _mask = 1;
            }
            return (_buffer & _mask) != 0 ? 1 : 0;
        }

        // READ_N_BITS: n bits, first read is the LSB.
        private int ReadBits(int n)
        {
            int c = 0;
            for (int b = 0; b < n; b++)
            {
                c += ReadBit() << b;
            }
            return c;
        }

        private void Raw256()
        {
            while (!_done && _srcPos < _srcEnd)
            {
                Emit(ReadByte());
            }
        }

        private void UnkDecode8()
        {
            while (!_done && _srcPos < _srcEnd)
            {
                int run = ReadByte() + 1;
                int color = ReadByte();
                while (run-- > 0)
                {
                    Emit(color);
                }
            }
        }

        private void UnkDecode9()
        {
            int run = 0;
            int guard = 0;
            while (!_done && _srcPos < _srcEnd && guard++ < 1000000)
            {
                int c = ReadBits(4);
                switch (c >> 2)
                {
                    case 0:
                    {
                        int color = ReadBits(4);
                        int count = (c & 3) + 2;
                        for (int i = 0; i < count; i++) Emit(run * 16 + color);
                        break;
                    }
                    case 1:
                    {
                        int count = (c & 3) + 1;
                        for (int i = 0; i < count; i++)
                        {
                            int color = ReadBits(4);
                            Emit(run * 16 + color);
                        }
                        break;
                    }
                    case 2:
                        run = ReadBits(4);
                        break;
                    default:
                        break;
                }
            }
        }

        private void UnkDecode10()
        {
            int numColors = ReadByte();
            var localPalette = new byte[256];
            for (int i = 0; i < numColors; i++)
            {
                localPalette[i] = (byte)ReadByte();
            }

            while (!_done && _srcPos < _srcEnd)
            {
                int color = ReadByte();
                if (color < numColors)
                {
                    Emit(localPalette[color]);
                }
                else
                {
                    int run = color - numColors + 1;
                    color = ReadByte();
                    while (run-- > 0)
                    {
                        Emit(color);
                    }
                }
            }
        }

        private void UnkDecode11()
        {
            int inc = 1;
            int color = ReadByte();

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < _height; y++)
                {
                    if (_x0 + x < _matrix.GetLength(0) && y < _matrix.GetLength(1))
                    {
                        _matrix[_x0 + x, y] = (byte)color;
                    }

                    int i = 0;
                    for (; i < 3; i++)
                    {
                        if (ReadBit() == 0) break;
                    }

                    switch (i)
                    {
                        case 1:
                            inc = -inc;
                            color = (color - inc) & 0xFF;
                            break;
                        case 2:
                            color = (color - inc) & 0xFF;
                            break;
                        case 3:
                            inc = 1;
                            color = ReadBits(8);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private static int ReadU32(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);
        }
    }
}
