using System.Drawing;
using System.Linq;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes a single cel (frame) of a SCUMM v7 AKOS costume (The Dig, Full Throttle) to a bitmap.
    /// AKOS keeps its data in sub-blocks (parsed as the AKOS block's children): AKHD (header: codec +
    /// cel count), AKOF (per-cel offset table: akcd offset + akci offset), AKCI (per-cel width/height),
    /// AKCD (the compressed cel pixels), AKPL (cel-colour count / room-palette remap) and RGBS (the
    /// costume's own RGB palette). The Dig and Full Throttle use cel codec 1 (BYLE RLE) - the same
    /// column-oriented run-length scheme as the v5/v6 COST costumes - so the decode mirrors
    /// CostumeImageDecoder; codec 5 (CDAT/BOMP) is decoded with the shared BOMP decoder.
    /// </summary>
    public static class AkosImageDecoder
    {
        /// <summary>Number of cels (frames) in the AKOS costume.</summary>
        public static int GetCelCount(BlockBase akos)
        {
            byte[] akhd = GetSubBlock(akos, "AKHD");
            return akhd != null && akhd.Length >= 8 ? ReadUInt16(akhd, 6) : 0;
        }

        /// <summary>The cel compression codec from AKHD (1 = BYLE RLE, 5 = CDAT/BOMP, 16 = MAJMIN).</summary>
        public static int GetCodec(BlockBase akos)
        {
            byte[] akhd = GetSubBlock(akos, "AKHD");
            return akhd != null && akhd.Length >= 10 ? ReadUInt16(akhd, 8) : 0;
        }

        /// <summary>The cel colour count = AKPL size (16/32/64 for codec 1; drives the BYLE-RLE bit split).</summary>
        public static int GetColorCount(BlockBase akos)
        {
            byte[] akpl = GetSubBlock(akos, "AKPL");
            return akpl != null ? akpl.Length : 0;
        }

        /// <summary>Width/height of a cel from its AKCI record, without decoding the pixels (0x0 if absent).
        /// Lets the GUI label cels and spot the tiny placeholder cels AKOS uses for unused frames.</summary>
        public static Size GetCelSize(BlockBase akos, int celIndex)
        {
            byte[] akhd = GetSubBlock(akos, "AKHD");
            byte[] akof = GetSubBlock(akos, "AKOF");
            byte[] akci = GetSubBlock(akos, "AKCI");
            if (akhd == null || akhd.Length < 8 || akof == null || akci == null) return Size.Empty;
            if (celIndex < 0 || celIndex >= ReadUInt16(akhd, 6)) return Size.Empty;

            int recordBase = celIndex * 6;
            if (recordBase + 6 > akof.Length) return Size.Empty;
            int akciOffset = ReadUInt16(akof, recordBase + 4);
            if (akciOffset + 4 > akci.Length) return Size.Empty;
            return new Size(ReadUInt16(akci, akciOffset), ReadUInt16(akci, akciOffset + 2));
        }

        /// <summary>Decodes cel <paramref name="celIndex"/> using the costume's own colours (RGBS / grayscale).</summary>
        public static Bitmap DecodeCel(BlockBase akos, int celIndex)
        {
            return DecodeCel(akos, celIndex, null);
        }

        /// <summary>
        /// Decodes cel <paramref name="celIndex"/> to an indexed bitmap. With <paramref name="roomPalette"/>
        /// null the costume's own colours are used: RGBS for codec 1/5 (the costume's standalone colour
        /// snapshot), grayscale for codec 16 masks (which carry no snapshot). When a 256-colour room palette
        /// is given, the cel is rendered as it would appear in that room: codec 1/5 map cel colour i through
        /// AKPL (i -&gt; akpl[i] -&gt; roomPalette, matching ScummVM's _palette[i]=akpl[i]); codec 16 indexes
        /// the room palette directly. Returns null for an empty/zero-size cel or an unsupported codec.
        /// </summary>
        public static Bitmap DecodeCel(BlockBase akos, int celIndex, Color[] roomPalette)
        {
            byte[] akhd = GetSubBlock(akos, "AKHD");
            byte[] akof = GetSubBlock(akos, "AKOF");
            byte[] akci = GetSubBlock(akos, "AKCI");
            byte[] akcd = GetSubBlock(akos, "AKCD");
            byte[] akpl = GetSubBlock(akos, "AKPL");
            byte[] rgbs = GetSubBlock(akos, "RGBS");
            if (akhd == null || akhd.Length < 10 || akof == null || akci == null || akcd == null || akpl == null)
            {
                return null;
            }

            int celCount = ReadUInt16(akhd, 6);
            int codec = ReadUInt16(akhd, 8);
            if (celIndex < 0 || celIndex >= celCount)
            {
                return null;
            }

            // AKOF record per cel: akcd offset (uint32 LE) + akci offset (uint16 LE) = 6 bytes.
            int recordBase = celIndex * 6;
            if (recordBase + 6 > akof.Length)
            {
                return null;
            }
            long akcdOffset = ReadUInt32(akof, recordBase);
            int akciOffset = ReadUInt16(akof, recordBase + 4);
            if (akciOffset + 4 > akci.Length || akcdOffset >= akcd.Length)
            {
                return null;
            }

            int width = ReadUInt16(akci, akciOffset);
            int height = ReadUInt16(akci, akciOffset + 2);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            Color[] palette = BuildPalette(codec, akpl, rgbs, roomPalette);

            // The Dig and Full Throttle use three cel codecs (matching ScummVM akos.cpp): 1 = BYLE RLE
            // (column-RLE), 5 = CDAT (a BOMP-encoded cel), 16 = MAJMIN (a bit-stream delta codec). Other
            // codecs (32/TRLE, HE-only) are left undecoded - the cel is preserved byte-for-byte either way.
            byte[,] indices;
            switch (codec)
            {
                case 1:
                    indices = DecodeByleRle(akcd, (int)akcdOffset, width, height, akpl.Length);
                    break;
                case 5:
                    indices = DecodeBomp(akcd, (int)akcdOffset, width, height);
                    break;
                case 16:
                    indices = DecodeMajMin(akcd, (int)akcdOffset, width, height);
                    break;
                default:
                    return null;
            }
            return IndexedImageHelper.FromIndexMatrix(indices, palette, -1);
        }

        /// <summary>
        /// AKOS codec 1 (BYLE RLE): byte-oriented run-length, decoded column by column. Each byte holds
        /// the colour in the high bits and the run length in the low bits; the split is keyed on the cel
        /// colour count (AKPL size), exactly as ScummVM BaseCostumeRenderer::paintCelByleRLECommon:
        /// 32 colours -&gt; 5 colour bits / 3 run bits, 64 -&gt; 6/2, otherwise (incl. 16) -&gt; 4/4. A zero
        /// run length means the next byte is the real length. NOTE: the 64-colour case is AKOS-specific -
        /// the v5/v6 COST codec only ever has 16 or 32 colours, so do NOT collapse this to a binary split.
        /// </summary>
        private static byte[,] DecodeByleRle(byte[] data, int offset, int width, int height, int paletteSize)
        {
            int runBits = paletteSize == 32 ? 3 : (paletteSize == 64 ? 2 : 4);
            int runMask = (1 << runBits) - 1;

            var result = new byte[width, height];
            int x = 0, y = 0, p = offset;
            while (x < width && p < data.Length)
            {
                byte b = data[p++];
                int color = b >> runBits;
                int run = b & runMask;
                if (run == 0)
                {
                    if (p >= data.Length) break;
                    run = data[p++];
                }
                for (int i = 0; i < run && x < width; i++)
                {
                    result[x, y] = (byte)color;
                    y++;
                    if (y == height)
                    {
                        y = 0;
                        x++;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// AKOS codec 5 (CDAT): a BOMP-encoded cel, decoded row by row (the same scheme as the v6/v7
        /// object BOMP images). Each row is a uint16 LE byte-length followed by byte-oriented RLE: a
        /// control byte whose low bit is the "repeat" flag and whose upper 7 bits are (run-1); a repeat
        /// run is one colour byte, a literal run is <c>run</c> colour bytes. Mirrors BompImageDecoder.
        /// </summary>
        private static byte[,] DecodeBomp(byte[] data, int offset, int width, int height)
        {
            var result = new byte[width, height];
            int p = offset;
            for (int y = 0; y < height && p + 2 <= data.Length; y++)
            {
                int lineSize = data[p] | (data[p + 1] << 8);
                p += 2;
                int lineEnd = p + lineSize;
                int x = 0;
                while (x < width && p < lineEnd && p < data.Length)
                {
                    byte control = data[p++];
                    int run = (control >> 1) + 1;
                    bool repeat = (control & 1) != 0;
                    if (repeat)
                    {
                        if (p >= data.Length) break;
                        byte color = data[p++];
                        for (int i = 0; i < run && x < width; i++) { result[x, y] = color; x++; }
                    }
                    else
                    {
                        for (int i = 0; i < run && x < width && p < data.Length; i++) { result[x, y] = data[p++]; x++; }
                    }
                }
                p = lineEnd; // each row is exactly lineSize bytes, even if it ended early
            }
            return result;
        }

        /// <summary>
        /// AKOS codec 16 (MAJMIN): a bit-stream delta codec, decoded row by row (matching ScummVM
        /// MajMinCodec). The header is [shift:1][startColour:1][initialBits:2 LE]; then per pixel: bit 0
        /// keeps the colour; "10" replaces it with <c>shift</c> raw bits; "11"+3 bits is a signed delta
        /// (-4..3), and a zero delta starts a repeat run whose length is the next 8 bits.
        /// </summary>
        private static byte[,] DecodeMajMin(byte[] data, int offset, int width, int height)
        {
            var result = new byte[width, height];
            if (offset + 4 > data.Length)
            {
                return result;
            }

            int shift = data[offset];
            int color = data[offset + 1];
            int bits = data[offset + 2] | (data[offset + 3] << 8);
            int dataPtr = offset + 4;
            int numBits = 16;
            bool repeatMode = false;
            int repeatCount = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[x, y] = (byte)color;

                    if (!repeatMode)
                    {
                        if (ReadBits(data, ref dataPtr, ref bits, ref numBits, 1) != 0)
                        {
                            if (ReadBits(data, ref dataPtr, ref bits, ref numBits, 1) != 0)
                            {
                                int diff = ReadBits(data, ref dataPtr, ref bits, ref numBits, 3) - 4;
                                if (diff != 0)
                                {
                                    color = (color + diff) & 0xFF;
                                }
                                else
                                {
                                    repeatMode = true;
                                    repeatCount = ReadBits(data, ref dataPtr, ref bits, ref numBits, 8) - 1;
                                }
                            }
                            else
                            {
                                color = ReadBits(data, ref dataPtr, ref bits, ref numBits, shift);
                            }
                        }
                    }
                    else if (--repeatCount == 0)
                    {
                        repeatMode = false;
                    }
                }
            }
            return result;
        }

        /// <summary>Reads <paramref name="n"/> low bits from the MAJMIN little-endian bit reservoir,
        /// refilling a byte at a time (ScummVM MajMinCodec readBits).</summary>
        private static int ReadBits(byte[] data, ref int dataPtr, ref int bits, ref int numBits, int n)
        {
            if (numBits <= 8 && dataPtr < data.Length)
            {
                bits |= data[dataPtr++] << numBits;
                numBits += 8;
            }
            int value = bits & ((1 << n) - 1);
            numBits -= n;
            bits >>= n;
            return value;
        }

        /// <summary>
        /// Builds the 256-entry render palette for a cel (so any colour byte indexes safely), honouring
        /// how the codec's pixel value is interpreted:
        /// - codec 1/5 pixel = a costume-colour index. With no room palette it maps through the costume's
        ///   own RGB snapshot (RGBS); with a room palette it maps colour i -&gt; akpl[i] -&gt; roomPalette
        ///   (ScummVM's _palette[i]=akpl[i]), i.e. the costume as it looks in that room.
        /// - codec 16 pixel = a room/screen palette index already, so a room palette is indexed directly;
        ///   with none it falls back to grayscale (these masks carry no RGBS snapshot of their own).
        /// </summary>
        private static Color[] BuildPalette(int codec, byte[] akpl, byte[] rgbs, Color[] roomPalette)
        {
            var palette = new Color[256];
            bool costumeIndexed = codec == 1 || codec == 5;

            if (roomPalette != null && roomPalette.Length >= 256)
            {
                for (int i = 0; i < 256; i++)
                {
                    if (costumeIndexed)
                    {
                        int roomIndex = (akpl != null && i < akpl.Length) ? akpl[i] : i;
                        palette[i] = roomPalette[roomIndex & 0xFF];
                    }
                    else
                    {
                        palette[i] = roomPalette[i];
                    }
                }
                return palette;
            }

            // Codec 16 pixels are direct 0-255 palette indices, so its grayscale fallback must span the
            // full 256-entry ramp (else indices above the AKPL size would wrap and band); codec 1/5 pixels
            // are costume-colour indices (0..AKPL size-1), so the ramp is scaled to the cel colour count.
            int count = codec == 16 ? 256 : (akpl != null && akpl.Length > 0 ? akpl.Length : 256);
            int denom = count > 1 ? count - 1 : 1;
            bool useRgbs = costumeIndexed && rgbs != null;
            for (int i = 0; i < 256; i++)
            {
                if (useRgbs && i * 3 + 2 < rgbs.Length)
                {
                    palette[i] = Color.FromArgb(rgbs[i * 3], rgbs[i * 3 + 1], rgbs[i * 3 + 2]);
                }
                else
                {
                    int g = (i < count ? i : i % count) * 255 / denom;
                    palette[i] = Color.FromArgb(g, g, g);
                }
            }
            return palette;
        }

        /// <summary>True when the AKOS has its own RGBS colour snapshot (codec 1/5 costumes); false for the
        /// codec-16 masks that have none (they need a room palette to show their true colours).</summary>
        public static bool HasOwnPalette(BlockBase akos)
        {
            byte[] rgbs = GetSubBlock(akos, "RGBS");
            return rgbs != null && rgbs.Length >= 3;
        }

        /// <summary>Raw bytes of the named AKOS sub-block (its child's Contents), or null when absent.</summary>
        private static byte[] GetSubBlock(BlockBase akos, string tag)
        {
            BlockBase child = akos.Childrens.FirstOrDefault(c => c.BlockType == tag);
            var raw = child as IRawContentBlock;
            return raw != null ? raw.Contents : null;
        }

        private static int ReadUInt16(byte[] b, int o)
        {
            return b[o] | (b[o + 1] << 8);
        }

        private static long ReadUInt32(byte[] b, int o)
        {
            return (long)b[o] | ((long)b[o + 1] << 8) | ((long)b[o + 2] << 16) | ((long)b[o + 3] << 24);
        }
    }
}
