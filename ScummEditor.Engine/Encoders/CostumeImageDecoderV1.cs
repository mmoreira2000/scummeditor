using System;
using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes a SCUMM v1 (format 0x57) costume frame to a bitmap. Unlike the v2/v3-old 0x58 bit-stream
    /// codec, a v1 CEL is a C64-style 2-bits-per-pixel (4-colour) byte RLE stored column-strip-major: each
    /// emitted byte is one 8-pixel-wide strip row (4 two-bit pixel pairs, low bits leftmost, each doubled to
    /// 2 screen pixels). The resource carries NO palette - the 4 colours come from a hardcoded actor palette
    /// at draw time (index 0 = transparent), so the caller supplies the 4 EGA indices for the preview.
    /// Mirrors ScummVM ClassicCostumeRenderer::byleRLEDecode_C64.
    /// </summary>
    public class CostumeImageDecoderV1
    {
        /// <summary>Decodes one frame using 4 EGA colour indices (egaIndices4[0] is the transparent slot). Null if unreadable.</summary>
        public Bitmap Decode(CostumeImageData frame, byte[] egaIndices4)
        {
            if (frame == null || egaIndices4 == null || egaIndices4.Length < 4) return null;
            int widthBytes = frame.Width / 8;
            int height = frame.Height;
            if (widthBytes <= 0 || height <= 0) return null;

            byte[] samples = DecodeC64Rle(frame.ImageData, 0, widthBytes, height);
            if (samples == null) return null;

            var matrix = new byte[frame.Width, height];
            for (int strip = 0; strip < widthBytes; strip++)
            {
                for (int y = 0; y < height; y++)
                {
                    byte cb = samples[strip * height + y]; // column-strip-major
                    for (int x = 0; x < 8; x++)
                        matrix[strip * 8 + x, y] = (byte)((cb >> (x & 6)) & 3); // low 2 bits = leftmost pixel pair
                }
            }

            var colors = new Color[4];
            for (int i = 0; i < 4; i++) colors[i] = EgaColorTable.Colors256[egaIndices4[i] & 0x0F];
            return IndexedImageHelper.FromIndexMatrix(matrix, colors, 0); // index 0 = transparent
        }

        /// <summary>A reasonable default 4-colour preview palette (EGA indices); the real colours are actor-dependent at runtime.</summary>
        public static byte[] DefaultPalette(bool isManiac)
        {
            // [0]=transparent, [1]=skin (8), [2]=suit (Maniac ~7 / Zak ~6), [3]=0 (lights-on draws index 3 as colour 0).
            return isManiac ? new byte[] { 0, 8, 7, 0 } : new byte[] { 0, 8, 6, 0 };
        }

        /// <summary>
        /// The C64 2-bit costume RLE -> widthBytes*height sample bytes (strip-major). A run byte: bit 0x80 =
        /// repeat one following colour byte (count = low 7 bits); otherwise a literal run of that many colour
        /// bytes. Null on a malformed stream.
        /// </summary>
        public static byte[] DecodeC64Rle(byte[] src, int offset, int widthBytes, int height)
        {
            int total = widthBytes * height;
            if (src == null || total <= 0 || offset < 0) return null;
            var dst = new byte[total];
            int p = offset, idx = 0;
            byte color = 0;
            try
            {
                while (idx < total)
                {
                    byte len = src[p++];
                    bool rep = (len & 0x80) != 0;
                    int n = len & 0x7F;
                    if (rep) color = src[p++];
                    for (int k = 0; k < n && idx < total; k++)
                    {
                        if (!rep) color = src[p++];
                        dst[idx++] = color;
                    }
                }
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            return dst;
        }
    }
}
