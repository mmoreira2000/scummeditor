using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes one glyph (frame) of a SCUMM v7 .NUT SMUSH font to a palette-index matrix or a bitmap.
    /// NUT glyphs use SMUSH frame-object codecs (verified against ScummVM nut_renderer.cpp / codec1.cpp /
    /// bomp.cpp and the real Dig/Full Throttle font files):
    ///   - codec 1 and codec 3  : BOMP run-length, row by row (each row a uint16 LE byte-length prefix +
    ///     bompDecodeLine). codec 3 (SMUSH_CODEC_RLE_ALT) decodes identically to codec 1.
    ///   - codec 21 and codec 44: a "skip + copy" run-length, row by row (each row a uint16 LE length +
    ///     records of [skip:uint16][run-1:uint16][run bytes]). codec 44 decodes the same way but its
    ///     transparent fill colour is 2 instead of 0.
    /// Glyph pixels are direct 8-bit indices into the game's runtime palette; the index matrix is what the
    /// PNG export/import round-trips (palette-independent), and a palette is only applied for the preview.
    /// </summary>
    public static class NutImageDecoder
    {
        /// <summary>The SMUSH codecs NUT fonts use that this engine can decode/encode.</summary>
        public static bool IsSupportedCodec(int codec)
        {
            return codec == 1 || codec == 3 || codec == 21 || codec == 44;
        }

        /// <summary>The colour index a codec leaves where no pixel is drawn (transparent): 2 for codec 44,
        /// 0 for the others - matching ScummVM's kSmush44TransparentColor / kDefaultTransparentColor.</summary>
        public static int TransparencyIndex(int codec)
        {
            return codec == 44 ? 2 : 0;
        }

        /// <summary>
        /// Decodes glyph <paramref name="index"/> to its raw palette-index matrix [width, height], or null
        /// for an empty glyph or an unsupported codec. Pixels not written by the codec are left at the
        /// codec's transparent index. This is the shared core used by the viewer and the encoder.
        /// </summary>
        public static byte[,] DecodeGlyphIndices(NutFont font, int index)
        {
            if (font == null || font.RawContent == null || index < 0 || index >= font.Glyphs.Count)
            {
                return null;
            }

            NutGlyph glyph = font.Glyphs[index];
            if (!glyph.HasPixels || !IsSupportedCodec(glyph.Codec))
            {
                return null;
            }

            byte[] data = font.RawContent;
            int start = glyph.PayloadOffset;
            int end = glyph.PayloadOffset + glyph.PayloadLength;
            int width = glyph.Width;
            int height = glyph.Height;

            var result = new byte[width, height];
            int transparency = TransparencyIndex(glyph.Codec);
            if (transparency != 0)
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        result[x, y] = (byte)transparency;
            }

            if (glyph.Codec == 1 || glyph.Codec == 3)
            {
                DecodeBomp(data, start, end, width, height, result);
            }
            else // 21 or 44
            {
                DecodeSkipCopy(data, start, end, width, height, result);
            }
            return result;
        }

        /// <summary>
        /// Decodes glyph <paramref name="index"/> to an indexed bitmap. With <paramref name="palette"/> null
        /// the pixels are shown on a 256-level grayscale ramp (the index value); when a 256-colour game/room
        /// palette is supplied the glyph is shown as it would look on screen. The transparent index is
        /// rendered fully transparent. Returns null for an empty glyph or an unsupported codec.
        /// </summary>
        public static Bitmap DecodeGlyph(NutFont font, int index, Color[] palette)
        {
            byte[,] indices = DecodeGlyphIndices(font, index);
            if (indices == null)
            {
                return null;
            }

            Color[] effective = (palette != null && palette.Length >= 256) ? palette : Grayscale();
            int transparency = TransparencyIndex(font.Glyphs[index].Codec);
            return IndexedImageHelper.FromIndexMatrix(indices, effective, transparency);
        }

        /// <summary>Width/height of a glyph without decoding the pixels.</summary>
        public static Size GetGlyphSize(NutFont font, int index)
        {
            if (font == null || index < 0 || index >= font.Glyphs.Count) return Size.Empty;
            NutGlyph g = font.Glyphs[index];
            return new Size(g.Width, g.Height);
        }

        /// <summary>
        /// BOMP run-length (codec 1/3): each row is [size:uint16 LE] then control bytes - low bit = repeat,
        /// upper 7 bits = run-1; a repeat run is one colour byte, a literal run is <c>run</c> colour bytes.
        /// Colour 0 stays transparent (the matrix is pre-zeroed). Mirrors smushDecodeRLE + bompDecodeLine.
        /// </summary>
        private static void DecodeBomp(byte[] data, int start, int end, int width, int height, byte[,] result)
        {
            int p = start;
            for (int y = 0; y < height && p + 2 <= end; y++)
            {
                int lineSize = data[p] | (data[p + 1] << 8);
                p += 2;
                int lineEnd = p + lineSize;
                if (lineEnd > end) lineEnd = end;

                int x = 0;
                while (x < width && p < lineEnd)
                {
                    byte control = data[p++];
                    int run = (control >> 1) + 1;
                    bool repeat = (control & 1) != 0;
                    if (repeat)
                    {
                        if (p >= lineEnd) break;
                        byte color = data[p++];
                        for (int i = 0; i < run && x < width; i++) { result[x, y] = color; x++; }
                    }
                    else
                    {
                        for (int i = 0; i < run && x < width && p < lineEnd; i++) { result[x, y] = data[p++]; x++; }
                    }
                }
                p = lineEnd; // each row occupies exactly lineSize bytes
            }
        }

        /// <summary>
        /// Skip-copy run-length (codec 21/44): each row is [size:uint16 LE] then records of
        /// [skip:uint16 LE] (transparent pixels to skip) + [run-1:uint16 LE] + <c>run</c> raw colour bytes.
        /// Skipped pixels keep the pre-filled transparent index. Mirrors NutRenderer::codec21.
        /// </summary>
        private static void DecodeSkipCopy(byte[] data, int start, int end, int width, int height, byte[,] result)
        {
            int p = start;
            for (int y = 0; y < height && p + 2 <= end; y++)
            {
                int lineSize = data[p] | (data[p + 1] << 8);
                p += 2;
                int lineEnd = p + lineSize;
                if (lineEnd > end) lineEnd = end;

                int x = 0;
                while (x < width && p + 2 <= lineEnd)
                {
                    int skip = data[p] | (data[p + 1] << 8);
                    p += 2;
                    x += skip;
                    if (x >= width || p + 2 > lineEnd) break;

                    int run = (data[p] | (data[p + 1] << 8)) + 1;
                    p += 2;
                    for (int i = 0; i < run && x < width && p < lineEnd; i++) { result[x, y] = data[p++]; x++; }
                }
                p = lineEnd;
            }
        }

        private static Color[] Grayscale()
        {
            var palette = new Color[256];
            for (int i = 0; i < 256; i++) palette[i] = Color.FromArgb(i, i, i);
            return palette;
        }
    }
}
