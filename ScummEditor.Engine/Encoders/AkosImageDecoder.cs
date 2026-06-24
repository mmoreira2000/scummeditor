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

        /// <summary>The cel compression codec from AKHD (1 = BYLE RLE, 5 = CDAT/BOMP).</summary>
        public static int GetCodec(BlockBase akos)
        {
            byte[] akhd = GetSubBlock(akos, "AKHD");
            return akhd != null && akhd.Length >= 10 ? ReadUInt16(akhd, 8) : 0;
        }

        /// <summary>
        /// Decodes cel <paramref name="celIndex"/> to an indexed bitmap using the costume's own RGB
        /// palette (RGBS). Returns null for an empty/zero-size cel or an unsupported codec.
        /// </summary>
        public static Bitmap DecodeCel(BlockBase akos, int celIndex)
        {
            byte[] akhd = GetSubBlock(akos, "AKHD");
            byte[] akof = GetSubBlock(akos, "AKOF");
            byte[] akci = GetSubBlock(akos, "AKCI");
            byte[] akcd = GetSubBlock(akos, "AKCD");
            byte[] akpl = GetSubBlock(akos, "AKPL");
            byte[] rgbs = GetSubBlock(akos, "RGBS");
            if (akhd == null || akof == null || akci == null || akcd == null || akpl == null)
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

            Color[] palette = BuildPalette(akpl, rgbs);

            if (codec != 1)
            {
                // The Dig and Full Throttle use codec 1 (BYLE RLE). Codec 5 (CDAT/BOMP) and the HE-only
                // codecs are left undecoded for now (the cel is preserved byte-for-byte either way).
                return null;
            }

            byte[,] indices = DecodeByleRle(akcd, (int)akcdOffset, width, height, akpl.Length);
            return IndexedImageHelper.FromIndexMatrix(indices, palette, -1);
        }

        /// <summary>
        /// AKOS codec 1 (BYLE RLE): byte-oriented run-length, decoded column by column. Each byte holds
        /// the colour in the high bits and the run length in the low bits (the split depends on the cel
        /// colour count: 4/4 for &lt;=16 colours, 5/3 otherwise); a zero run length means the next byte
        /// is the real length. Identical to the v5/v6 COST codec.
        /// </summary>
        private static byte[,] DecodeByleRle(byte[] data, int offset, int width, int height, int paletteSize)
        {
            int colorBits = paletteSize <= 16 ? 4 : 5;
            int runBits = 8 - colorBits;
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
        /// The costume palette: prefer RGBS (the costume's own absolute RGB triplets, one per cel
        /// colour); fall back to a grayscale ramp when it is absent so the cel is still visible.
        /// </summary>
        private static Color[] BuildPalette(byte[] akpl, byte[] rgbs)
        {
            int count = akpl.Length;
            var palette = new Color[count];
            for (int i = 0; i < count; i++)
            {
                if (rgbs != null && i * 3 + 2 < rgbs.Length)
                {
                    palette[i] = Color.FromArgb(rgbs[i * 3], rgbs[i * 3 + 1], rgbs[i * 3 + 2]);
                }
                else
                {
                    int g = count > 1 ? i * 255 / (count - 1) : 0;
                    palette[i] = Color.FromArgb(g, g, g);
                }
            }
            return palette;
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
