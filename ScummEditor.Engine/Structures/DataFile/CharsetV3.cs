using System.Drawing;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    SCUMM v3 standalone charset/font file (9N.LFL: 99.LFL = charset 0, 98.LFL = charset 1, ...).
    Always stored UNENCRYPTED, even in the otherwise XOR-0xFF v3 old-bundle games. Completely
    different from the v4/v5/v6 CHAR block, so it gets its own type (not the Charset class).

    Layout (verified against Loom EGA and Indy3 99.LFL):
        +0  uint16  size word (file length + 1; not relied upon)
        +2  uint16  reserved (observed 0x0000)
        +4  uint16  reserved field (observed 0x0163) - preserved verbatim
        +6  uint8   numChars
        +7  uint8   fontHeight
        +8  uint8   width table   (numChars bytes: the cursor advance of each character)
        +8+numChars glyph bitmaps (numChars x 8 bytes; each glyph is 8 rows of 8 1-bpp pixels,
                                   bit 7 = leftmost). File length = 8 + numChars*9.

    The whole file is kept verbatim in RawContent so an unedited font round-trips byte-for-byte; the
    parsed fields are a view, and edits (PNG import) rewrite the glyph bytes in place.
    */
    public class CharsetV3
    {
        private const int GlyphBytes = 8; // 8 rows, 1 bpp, 8 px wide

        public byte[] RawContent { get; private set; }
        public int NumChars { get; private set; }
        public int FontHeight { get; private set; }

        /// <summary>Loads a 9N.LFL font from its (already plaintext) bytes.</summary>
        public void LoadFromFileBytes(byte[] bytes)
        {
            RawContent = bytes;
            NumChars = bytes.Length > 6 ? bytes[6] : 0;
            FontHeight = bytes.Length > 7 ? bytes[7] : 0;
        }

        private int WidthTableStart { get { return 8; } }
        private int GlyphTableStart { get { return 8 + NumChars; } }

        /// <summary>The cursor-advance width of a character (0..NumChars-1).</summary>
        public int CharWidth(int charIndex)
        {
            int p = WidthTableStart + charIndex;
            return (charIndex >= 0 && charIndex < NumChars && p < RawContent.Length) ? RawContent[p] : 0;
        }

        /// <summary>True when the glyph of this character is present in the file.</summary>
        public bool HasGlyph(int charIndex)
        {
            return charIndex >= 0 && charIndex < NumChars
                   && GlyphTableStart + charIndex * GlyphBytes + GlyphBytes <= RawContent.Length;
        }

        /// <summary>Renders one glyph as an 8 x 8 indexed bitmap (index 1 = ink, 0 = background).</summary>
        public Bitmap RenderGlyph(int charIndex, Color background, Color ink)
        {
            if (!HasGlyph(charIndex))
            {
                return null;
            }

            int baseOffset = GlyphTableStart + charIndex * GlyphBytes;
            var indices = new byte[8, GlyphBytes];
            for (int row = 0; row < GlyphBytes; row++)
            {
                byte bits = RawContent[baseOffset + row];
                for (int col = 0; col < 8; col++)
                {
                    indices[col, row] = (byte)((bits >> (7 - col)) & 1);
                }
            }

            return Encoders.IndexedImageHelper.FromIndexMatrix(indices, new[] { background, ink }, -1);
        }

        /// <summary>Replaces a glyph's 8 raw bytes (used by PNG import); no-op if out of range.</summary>
        public void SetGlyphBytes(int charIndex, byte[] glyph)
        {
            if (!HasGlyph(charIndex) || glyph == null || glyph.Length != GlyphBytes)
            {
                return;
            }
            System.Array.Copy(glyph, 0, RawContent, GlyphTableStart + charIndex * GlyphBytes, GlyphBytes);
        }
    }
}
