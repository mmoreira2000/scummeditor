namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decompresses the per-block COMP codecs used inside SCUMM v7 iMUSE bundles (The Dig's DIGMUSIC.BUN /
    /// DIGVOICE.BUN). A direct port of ScummVM's BundleCodecs (dimuse_codecs.cpp): compDecode is the LZ77
    /// inner decoder; decompressCodec dispatches codecs 0-12 = raw / LZ77 / 1st- &amp; 2nd-order delta /
    /// delta + 12-bit nibble repack. The shipped Dig bundles use only 0-12 (verified); the IMA-ADPCM
    /// "VIMA" codecs 13/15 are CMI-only and not implemented. Concatenating the decompressed blocks of an
    /// entry rebuilds its iMUS resource, which ImuseAudioDecoder then turns into WAV.
    /// </summary>
    public static class ImuseBundleCodecs
    {
        /// <summary>True for the codecs this port can decompress: 0-12 (the Dig/FT LZ77 family) and the
        /// VIMA IMA-ADPCM codecs 13 (mono) / 15 (stereo) that COMI's music + voice bundles use.</summary>
        public static bool CanDecode(int codec)
        {
            return (codec >= 0 && codec <= 12) || codec == 13 || codec == 15;
        }

        /// <summary>
        /// Decompresses one COMP block. <paramref name="output"/> must hold at least 0x2000 bytes (the
        /// bundle chunk size); returns the number of bytes written. Throws for the unsupported VIMA codecs.
        /// </summary>
        public static int DecompressCodec(int codec, byte[] input, int inputSize, byte[] output)
        {
            int outputSize;
            int offset1, offset2, offset3, length, k, c, s, j, r, t, z;
            byte tmp1, tmp2;

            switch (codec)
            {
                case 0:
                    System.Array.Copy(input, 0, output, 0, inputSize);
                    return inputSize;

                case 1:
                    return CompDecode(input, output);

                case 2:
                    outputSize = CompDecode(input, output);
                    for (z = 1; z < outputSize; z++) output[z] += output[z - 1];
                    return outputSize;

                case 3:
                    outputSize = CompDecode(input, output);
                    for (z = 2; z < outputSize; z++) output[z] += output[z - 1];
                    for (z = 1; z < outputSize; z++) output[z] += output[z - 1];
                    return outputSize;

                case 4:
                {
                    outputSize = CompDecode(input, output);
                    for (z = 2; z < outputSize; z++) output[z] += output[z - 1];
                    for (z = 1; z < outputSize; z++) output[z] += output[z - 1];

                    if (outputSize < 2) return outputSize; // the 12-bit repack needs >= 2 bytes; guard a degenerate/empty block

                    var tbl = new byte[outputSize];
                    length = (outputSize << 3) / 12;
                    k = 0;
                    if (length > 0)
                    {
                        c = -12; s = 0; j = 0;
                        do
                        {
                            int ptr = length + (k >> 1);
                            tmp2 = output[j];
                            if ((k & 1) != 0)
                            {
                                r = c >> 3;
                                tbl[r + 2] = (byte)(((tmp2 & 0x0f) << 4) | (output[ptr + 1] >> 4));
                                tbl[r + 1] = (byte)((tmp2 & 0xf0) | tbl[r + 1]);
                            }
                            else
                            {
                                r = s >> 3;
                                tbl[r + 0] = (byte)(((tmp2 & 0x0f) << 4) | (output[ptr + 0] & 0x0f));
                                tbl[r + 1] = (byte)(tmp2 >> 4);
                            }
                            s += 12; c += 12; k++; j++;
                        } while (k < length);
                    }
                    offset1 = ((length - 1) * 3) >> 1;
                    tbl[offset1 + 1] = (byte)(tbl[offset1 + 1] | (output[length - 1] & 0xf0));
                    System.Array.Copy(tbl, 0, output, 0, outputSize);
                    return outputSize;
                }

                case 5:
                {
                    outputSize = CompDecode(input, output);
                    for (z = 2; z < outputSize; z++) output[z] += output[z - 1];
                    for (z = 1; z < outputSize; z++) output[z] += output[z - 1];

                    if (outputSize < 2) return outputSize; // the 12-bit repack needs >= 2 bytes; guard a degenerate/empty block

                    var tbl = new byte[outputSize];
                    length = (outputSize << 3) / 12;
                    k = 1; c = 0; s = 12;
                    tbl[0] = (byte)(output[length] >> 4);
                    t = length + k;
                    j = 1;
                    if (t > k)
                    {
                        do
                        {
                            tmp1 = output[length + (k >> 1)];
                            tmp2 = output[j - 1];
                            if ((k & 1) != 0)
                            {
                                r = c >> 3;
                                tbl[r + 0] = (byte)((tmp2 & 0xf0) | tbl[r]);
                                tbl[r + 1] = (byte)(((tmp2 & 0x0f) << 4) | (tmp1 & 0x0f));
                            }
                            else
                            {
                                r = s >> 3;
                                tbl[r + 0] = (byte)(tmp2 >> 4);
                                tbl[r - 1] = (byte)(((tmp2 & 0x0f) << 4) | (tmp1 >> 4));
                            }
                            s += 12; c += 12; k++; j++;
                        } while (k < t);
                    }
                    System.Array.Copy(tbl, 0, output, 0, outputSize);
                    return outputSize;
                }

                case 6:
                {
                    outputSize = CompDecode(input, output);
                    for (z = 2; z < outputSize; z++) output[z] += output[z - 1];
                    for (z = 1; z < outputSize; z++) output[z] += output[z - 1];

                    if (outputSize < 2) return outputSize; // the 12-bit repack needs >= 2 bytes; guard a degenerate/empty block

                    var tbl = new byte[outputSize];
                    length = (outputSize << 3) / 12;
                    k = 0; c = 0; j = 0; s = -12;
                    tbl[0] = output[outputSize - 1];
                    tbl[outputSize - 1] = output[length - 1];
                    t = length - 1;
                    if (t > 0)
                    {
                        do
                        {
                            tmp1 = output[length + (k >> 1)];
                            tmp2 = output[j];
                            if ((k & 1) != 0)
                            {
                                r = s >> 3;
                                tbl[r + 2] = (byte)((tmp2 & 0xf0) | tbl[r + 2]);
                                tbl[r + 3] = (byte)(((tmp2 & 0x0f) << 4) | (tmp1 >> 4));
                            }
                            else
                            {
                                r = c >> 3;
                                tbl[r + 2] = (byte)(tmp2 >> 4);
                                tbl[r + 1] = (byte)(((tmp2 & 0x0f) << 4) | (tmp1 & 0x0f));
                            }
                            s += 12; c += 12; k++; j++;
                        } while (k < t);
                    }
                    System.Array.Copy(tbl, 0, output, 0, outputSize);
                    return outputSize;
                }

                case 10:
                {
                    outputSize = CompDecode(input, output);
                    for (z = 2; z < outputSize; z++) output[z] += output[z - 1];
                    for (z = 1; z < outputSize; z++) output[z] += output[z - 1];

                    if (outputSize < 2) return outputSize; // the 12-bit repack needs >= 2 bytes; guard a degenerate/empty block

                    var tbl = new byte[outputSize];
                    System.Array.Copy(output, 0, tbl, 0, outputSize);

                    offset1 = outputSize / 3; offset2 = offset1 << 1; offset3 = offset2;
                    while (offset1-- != 0)
                    {
                        offset2 -= 2; offset3--;
                        tbl[offset2 + 0] = output[offset1];
                        tbl[offset2 + 1] = output[offset3];
                    }

                    length = (outputSize << 3) / 12;
                    k = 0;
                    if (length > 0)
                    {
                        c = -12; s = 0;
                        do
                        {
                            j = length + (k >> 1);
                            tmp1 = tbl[k];
                            if ((k & 1) != 0)
                            {
                                r = c >> 3;
                                tmp2 = tbl[j + 1];
                                output[r + 2] = (byte)(((tmp1 & 0x0f) << 4) | (tmp2 >> 4));
                                output[r + 1] = (byte)(output[r + 1] | (tmp1 & 0xf0));
                            }
                            else
                            {
                                r = s >> 3;
                                tmp2 = tbl[j];
                                output[r + 0] = (byte)(((tmp1 & 0x0f) << 4) | (tmp2 & 0x0f));
                                output[r + 1] = (byte)(tmp1 >> 4);
                            }
                            s += 12; c += 12; k++;
                        } while (k < length);
                    }
                    offset1 = ((length - 1) * 3) >> 1;
                    output[offset1 + 1] = (byte)((tbl[length] & 0xf0) | output[offset1 + 1]);
                    return outputSize;
                }

                case 11:
                {
                    outputSize = CompDecode(input, output);
                    for (z = 2; z < outputSize; z++) output[z] += output[z - 1];
                    for (z = 1; z < outputSize; z++) output[z] += output[z - 1];

                    if (outputSize < 2) return outputSize; // the 12-bit repack needs >= 2 bytes; guard a degenerate/empty block

                    var tbl = new byte[outputSize];
                    System.Array.Copy(output, 0, tbl, 0, outputSize);

                    offset1 = outputSize / 3; offset2 = offset1 << 1; offset3 = offset2;
                    while (offset1-- != 0)
                    {
                        offset2 -= 2; offset3--;
                        tbl[offset2 + 0] = output[offset1];
                        tbl[offset2 + 1] = output[offset3];
                    }

                    length = (outputSize << 3) / 12;
                    k = 1; c = 0; s = 12;
                    output[0] = (byte)(tbl[length] >> 4);
                    t = length + k;
                    if (t > k)
                    {
                        do
                        {
                            j = length + (k >> 1);
                            tmp1 = tbl[k - 1];
                            tmp2 = tbl[j];
                            if ((k & 1) != 0)
                            {
                                r = c >> 3;
                                output[r + 0] = (byte)(output[r] | (tmp1 & 0xf0));
                                output[r + 1] = (byte)(((tmp1 & 0x0f) << 4) | (tmp2 & 0x0f));
                            }
                            else
                            {
                                r = s >> 3;
                                output[r + 0] = (byte)(tmp1 >> 4);
                                output[r - 1] = (byte)(((tmp1 & 0x0f) << 4) | (tmp2 >> 4));
                            }
                            s += 12; c += 12; k++;
                        } while (k < t);
                    }
                    return outputSize;
                }

                case 12:
                {
                    outputSize = CompDecode(input, output);
                    for (z = 2; z < outputSize; z++) output[z] += output[z - 1];
                    for (z = 1; z < outputSize; z++) output[z] += output[z - 1];

                    if (outputSize < 2) return outputSize; // the 12-bit repack needs >= 2 bytes; guard a degenerate/empty block

                    var tbl = new byte[outputSize];
                    System.Array.Copy(output, 0, tbl, 0, outputSize);

                    offset1 = outputSize / 3; offset2 = offset1 << 1; offset3 = offset2;
                    while (offset1-- != 0)
                    {
                        offset2 -= 2; offset3--;
                        tbl[offset2 + 0] = output[offset1];
                        tbl[offset2 + 1] = output[offset3];
                    }

                    length = (outputSize << 3) / 12;
                    k = 0; c = 0; s = -12;
                    output[0] = tbl[outputSize - 1];
                    output[outputSize - 1] = tbl[length - 1];
                    t = length - 1;
                    if (t > 0)
                    {
                        do
                        {
                            j = length + (k >> 1);
                            tmp1 = tbl[k];
                            tmp2 = tbl[j];
                            if ((k & 1) != 0)
                            {
                                r = s >> 3;
                                output[r + 2] = (byte)(output[r + 2] | (tmp1 & 0xf0));
                                output[r + 3] = (byte)(((tmp1 & 0x0f) << 4) | (tmp2 >> 4));
                            }
                            else
                            {
                                r = c >> 3;
                                output[r + 2] = (byte)(tmp1 >> 4);
                                output[r + 1] = (byte)(((tmp1 & 0x0f) << 4) | (tmp2 & 0x0f));
                            }
                            s += 12; c += 12; k++;
                        } while (k < t);
                    }
                    return outputSize;
                }

                case 13:
                case 15:
                    InitializeImcTables();
                    return DecompressAdpcm(input, inputSize, output, codec == 13 ? 1 : 2);

                default:
                    throw new System.NotSupportedException("iMUSE bundle codec " + codec + " is not supported");
            }
        }

        /// <summary>The LZ77 inner decoder (ScummVM compDecode): a bit-stream of literals and back-references.</summary>
        private static int CompDecode(byte[] src, byte[] dst)
        {
            int srcptr = 0, dstptr = 0;
            int bitsleft = 16;
            int mask = src[srcptr] | (src[srcptr + 1] << 8);
            srcptr += 2;

            int NextBit()
            {
                int b = mask & 1;
                mask >>= 1;
                if (--bitsleft == 0)
                {
                    mask = src[srcptr] | (src[srcptr + 1] << 8);
                    srcptr += 2;
                    bitsleft = 16;
                }
                return b;
            }

            for (;;)
            {
                if (NextBit() != 0)
                {
                    dst[dstptr++] = src[srcptr++];
                }
                else
                {
                    int data, size;
                    if (NextBit() == 0)
                    {
                        size = NextBit() << 1;
                        size = (size | NextBit()) + 3;
                        data = src[srcptr++] - 256; // byte | 0xffffff00
                    }
                    else
                    {
                        data = src[srcptr++];
                        size = src[srcptr++];
                        data |= unchecked((int)(0xfffff000u + (uint)((size & 0xf0) << 4)));
                        size = (size & 0x0f) + 3;
                        if (size == 3)
                        {
                            if ((src[srcptr++] + 1) == 1)
                            {
                                return dstptr;
                            }
                        }
                    }
                    int result = dstptr + data; // data is negative -> back-reference
                    if (result < 0) return dstptr; // malformed stream: invalid back-reference, stop gracefully
                    while (size-- != 0)
                    {
                        dst[dstptr++] = dst[result++];
                    }
                }
            }
        }

        // -------------------------------------------------------------------------
        // VIMA - the variable-bitwidth IMA-ADPCM codec (13 = mono, 15 = stereo) used by COMI's music
        // (MUSDISK) and voice (VOXDISK) bundles. Direct port of ScummVM dimuse_codecs.cpp decompressADPCM
        // + initializeImcTables, with the standard 89-entry IMA step table. Each block decompresses to
        // 0x2000 bytes of signed 16-bit LE PCM (which becomes the iMUS DATA, so ImuseAudioDecoder's 16-bit
        // path turns it into WAV).
        // -------------------------------------------------------------------------

        // The standard IMA-ADPCM step-size table (Audio::Ima_ADPCMStream::_imaTable).
        private static readonly int[] ImaTable =
        {
                7,    8,    9,   10,   11,   12,   13,   14,   16,   17,   19,   21,   23,   25,   28,   31,
               34,   37,   41,   45,   50,   55,   60,   66,   73,   80,   88,   97,  107,  118,  130,  143,
              157,  173,  190,  209,  230,  253,  279,  307,  337,  371,  408,  449,  494,  544,  598,  658,
              724,  796,  876,  963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024,
             3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493,10442,11487,12635,13899,
            15289,16818,18500,20350,22385,24623,27086,29794,32767
        };

        // imxOtherTable[bits-2][data] - the table-position adjustment (-1 = 0xFF in the C source).
        private static readonly sbyte[][] ImxOtherTable =
        {
            new sbyte[] { -1, 4 },
            new sbyte[] { -1, -1, 2, 8 },
            new sbyte[] { -1, -1, -1, -1, 1, 2, 4, 6 },
            new sbyte[] { -1, -1, -1, -1, -1, -1, -1, -1, 1, 2, 4, 6, 8, 12, 16, 32 },
            new sbyte[] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                           1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24, 26, 28, 32 },
            new sbyte[] { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                          -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
                           1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16,
                          17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 }
        };

        private static byte[] _destImcTable;
        private static uint[] _destImcTable2;
        private static readonly object _imcLock = new object();

        private static void InitializeImcTables()
        {
            if (_destImcTable != null) return;
            lock (_imcLock)
            {
                if (_destImcTable != null) return;
                var table = new byte[89];
                var table2 = new uint[89 * 64];

                for (int pos = 0; pos <= 88; pos++)
                {
                    byte put = 1;
                    int tableValue = ((ImaTable[pos] * 4) / 7) / 2;
                    while (tableValue != 0) { tableValue /= 2; put++; }
                    if (put < 3) put = 3;
                    if (put > 8) put = 8;
                    table[pos] = (byte)(put - 1);
                }

                for (int n = 0; n < 64; n++)
                {
                    for (int pos = 0; pos <= 88; pos++)
                    {
                        int count = 32, put = 0, tableValue = ImaTable[pos];
                        do
                        {
                            if ((count & n) != 0) put += tableValue;
                            count /= 2;
                            tableValue /= 2;
                        } while (count != 0);
                        table2[n + pos * 64] = (uint)put;
                    }
                }

                _destImcTable2 = table2;
                _destImcTable = table; // publish last (the null check above gates on this)
            }
        }

        private static int DecompressAdpcm(byte[] src, int srcSize, byte[] dst, int channels)
        {
            const int MaxChannels = 2;
            int outputSamplesLeft = 0x1000;
            var initialTablePos = new int[MaxChannels];
            var initialOutputWord = new int[MaxChannels];

            int s = 0;
            int firstWord = (src[s] << 8) | src[s + 1]; s += 2;
            int rawBase = 0;
            if (firstWord != 0)
            {
                // a leading block of raw (already-PCM) audio; clamp to the buffers (real data fits in 0x2000)
                int copy = firstWord;
                if (copy > dst.Length) copy = dst.Length;
                if (s + copy > src.Length) copy = src.Length - s;
                if (copy < 0) copy = 0;
                System.Array.Copy(src, s, dst, 0, copy);
                rawBase = copy;
                s += copy;
                // Decrement by the SAME (possibly clamped) byte count we copied so the sample budget and the
                // source pointer stay consistent (ScummVM uses the unclamped firstWord, but real COMI blocks
                // never take this path - firstWord is always 0 - so consistency here is the safe choice).
                outputSamplesLeft -= copy / 2;
            }
            else
            {
                for (int i = 0; i < channels; i++)
                {
                    initialTablePos[i] = src[s]; s += 1;
                    s += 4; // skip the (unused) initial imcTable entry
                    initialOutputWord[i] = ReadBE32(src, s); s += 4;
                }
            }

            int bitstreamStart = s;
            int totalBitOffset = 0;
            for (int chan = 0; chan < channels; chan++)
            {
                int curTablePos = initialTablePos[chan];
                int outputWord = initialOutputWord[chan];
                int destPos = rawBase + chan * 2;

                int bound = (channels == 1)
                    ? outputSamplesLeft
                    : (chan == 0 ? (outputSamplesLeft + 1) / 2 : outputSamplesLeft / 2);

                for (int i = 0; i < bound; i++)
                {
                    int bits = _destImcTable[curTablePos];
                    int readPos = bitstreamStart + (totalBitOffset >> 3);
                    int readWord = (ushort)(ReadBE16(src, readPos) << (totalBitOffset & 7));
                    int packet = (byte)(readWord >> (16 - bits));
                    totalBitOffset += bits;

                    int signBitMask = 1 << (bits - 1);
                    int dataBitMask = signBitMask - 1;
                    int data = packet & dataBitMask;

                    int tmpA = data << (7 - bits);
                    int imcTableEntry = ImaTable[curTablePos] >> (bits - 1);
                    int delta = imcTableEntry + (int)_destImcTable2[tmpA + curTablePos * 64];
                    if ((packet & signBitMask) != 0) delta = -delta;

                    outputWord += delta;
                    if (outputWord < -0x8000) outputWord = -0x8000;
                    if (outputWord > 0x7fff) outputWord = 0x7fff;

                    if (destPos + 1 < dst.Length)
                    {
                        dst[destPos] = (byte)(outputWord & 0xFF);
                        dst[destPos + 1] = (byte)((outputWord >> 8) & 0xFF);
                    }
                    destPos += channels << 1;

                    curTablePos += ImxOtherTable[bits - 2][data];
                    if (curTablePos < 0) curTablePos = 0;
                    if (curTablePos > 88) curTablePos = 88;
                }
            }

            return 0x2000;
        }

        private static int ReadBE16(byte[] b, int o)
        {
            int hi = (o >= 0 && o < b.Length) ? b[o] : 0;
            int lo = (o + 1 >= 0 && o + 1 < b.Length) ? b[o + 1] : 0;
            return (hi << 8) | lo;
        }

        private static int ReadBE32(byte[] b, int o)
        {
            if (o < 0 || o + 4 > b.Length) return 0;
            return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        }
    }
}
