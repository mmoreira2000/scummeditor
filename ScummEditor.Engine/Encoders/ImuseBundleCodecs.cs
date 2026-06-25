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
        /// <summary>True for the codecs this port can decompress (everything the Dig/FT bundles use).</summary>
        public static bool CanDecode(int codec)
        {
            return codec >= 0 && codec <= 12;
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

                default:
                    throw new System.NotSupportedException("iMUSE bundle codec " + codec + " is not supported (VIMA 13/15 are CMI-only)");
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
                    while (size-- != 0)
                    {
                        dst[dstptr++] = dst[result++];
                    }
                }
            }
        }
    }
}
