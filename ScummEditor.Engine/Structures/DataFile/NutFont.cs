using System.Collections.Generic;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    NUT - SCUMM v7 external SMUSH font / sprite-sheet file (The Dig, Full Throttle). Unlike the CHAR
    charset, a NUT file lives OUTSIDE the .LA0/.LA1 container, as its own file in the game folder
    (FONT0.NUT, SCUMMFNT.NUT, BIGFONT.NUT, ...). It is a SMUSH animation container, one frame per glyph:

      ANIM  (big-endian [tag:4]["BE size":4]) container of:
        AHDR  (big-endian tag+size) header; number of glyphs = uint16 LE at AHDR + 10
        FRME * numChars   (big-endian tag+size) one per glyph, each holding:
          FOBJ  (big-endian tag+size) the frame object:
            +8  uint16 LE  codec (1/3 = BOMP RLE, 21/44 = skip-copy RLE)
            +10 int16  LE  x offset
            +12 int16  LE  y offset
            +14 uint16 LE  width
            +16 uint16 LE  height
            +18 4 bytes    (unused)
            +22 ...        encoded pixel payload (length = FOBJ size - 14)

    Glyph pixels are 8-bit indices into the game's runtime palette (NUT carries no palette of its own).
    The whole file is kept verbatim (RawContent) and written back unchanged, so an unedited font always
    round-trips byte-identically; only an edited glyph is re-encoded (NutImageEncoder rebuilds RawContent).
    */
    public class NutFont
    {
        public string FilePath { get; set; }
        public byte[] RawContent { get; set; }

        /// <summary>Glyph (frame) count declared in AHDR; equals Glyphs.Count when the file parses fully.</summary>
        public int NumChars { get; private set; }

        public List<NutGlyph> Glyphs { get; private set; } = new List<NutGlyph>();

        /// <summary>True when the ANIM/AHDR header is well-formed and at least one glyph parsed.</summary>
        public bool IsValid { get; private set; }

        public void LoadFromFileBytes(byte[] fileBytes)
        {
            RawContent = fileBytes;
            Reparse();
        }

        /// <summary>Re-reads the frame table from RawContent (after a glyph is re-encoded).</summary>
        public void Reparse()
        {
            Glyphs = new List<NutGlyph>();
            NumChars = 0;
            IsValid = false;
            byte[] data = RawContent;
            if (data == null || data.Length < 24) return;

            if (Tag(data, 0) != "ANIM" || Tag(data, 8) != "AHDR") return;

            int ahdrSize = (int)ReadUInt32BE(data, 12);
            NumChars = ReadUInt16LE(data, 18); // AHDR + 10

            // First FRME follows the AHDR chunk (tag+size = 8 bytes), padded to an even boundary.
            int p = 16 + ahdrSize;
            if ((ahdrSize & 1) != 0) p++;

            while (Glyphs.Count < NumChars && p + 8 <= data.Length)
            {
                if (Tag(data, p) != "FRME") break;
                int frameSize = (int)ReadUInt32BE(data, p + 4);

                var glyph = new NutGlyph { Index = Glyphs.Count, FrameOffset = p, FrameSize = frameSize };

                int fp = p + 8; // FOBJ sits at the start of the FRME body
                if (fp + 22 <= data.Length && Tag(data, fp) == "FOBJ")
                {
                    int fobjSize = (int)ReadUInt32BE(data, fp + 4);
                    glyph.FobjOffset = fp;
                    glyph.FobjSize = fobjSize;
                    glyph.Codec = ReadUInt16LE(data, fp + 8);
                    glyph.XOffset = (short)ReadUInt16LE(data, fp + 10);
                    glyph.YOffset = (short)ReadUInt16LE(data, fp + 12);
                    glyph.Width = ReadUInt16LE(data, fp + 14);
                    glyph.Height = ReadUInt16LE(data, fp + 16);
                    glyph.PayloadOffset = fp + 22;
                    glyph.PayloadLength = fobjSize - 14;
                    if (glyph.PayloadLength < 0 || glyph.PayloadOffset + glyph.PayloadLength > data.Length)
                    {
                        glyph.PayloadLength = 0; // malformed: leave it undecodable rather than over-read
                    }
                    else
                    {
                        glyph.HasFobj = true;
                    }
                }

                Glyphs.Add(glyph);

                long next = (long)p + 8 + frameSize;
                if ((frameSize & 1) != 0) next++;
                if (next <= p) break; // guard against a zero/negative advance
                p = (int)next;
            }

            IsValid = Glyphs.Count > 0;
        }

        private static string Tag(byte[] data, int offset)
        {
            if (offset + 4 > data.Length) return string.Empty;
            return string.Concat((char)data[offset], (char)data[offset + 1], (char)data[offset + 2], (char)data[offset + 3]);
        }

        private static int ReadUInt16LE(byte[] b, int o)
        {
            return b[o] | (b[o + 1] << 8);
        }

        private static uint ReadUInt32BE(byte[] b, int o)
        {
            return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
        }
    }

    /// <summary>One glyph (FRME/FOBJ) of a NUT font: its codec, dimensions and the byte range of its
    /// encoded payload within the parent <see cref="NutFont.RawContent"/>.</summary>
    public class NutGlyph
    {
        public int Index { get; set; }
        public int Codec { get; set; }
        public int XOffset { get; set; }
        public int YOffset { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public int FrameOffset { get; set; } // offset of the FRME tag in RawContent
        public int FrameSize { get; set; }   // FRME chunk size (bytes after the FRME tag+size)
        public int FobjOffset { get; set; }   // offset of the FOBJ tag
        public int FobjSize { get; set; }
        public int PayloadOffset { get; set; } // first encoded pixel byte
        public int PayloadLength { get; set; }

        /// <summary>True when a valid FOBJ was found (some FRMEs are empty placeholders).</summary>
        public bool HasFobj { get; set; }

        /// <summary>True when the glyph has decodable pixels.</summary>
        public bool HasPixels { get { return HasFobj && Width > 0 && Height > 0; } }
    }
}
