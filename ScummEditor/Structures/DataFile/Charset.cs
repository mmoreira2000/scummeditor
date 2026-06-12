using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /*
    CHAR - charset / font resource (SCUMM v6). Layout within the block body:

      0x00  uint32   size (of the data, internal)
      0x04  2 bytes  format marker
      0x06  15 bytes color map (font palette indices)
      0x15  byte     bits per pixel (1/2/4/8)
      0x16  byte     font height
      0x17  uint16   number of characters (LE)
      0x19  uint32[numChars]  per-char data offset, RELATIVE TO 0x15 (0 = char absent)

      char data (at 0x15 + offset):
        byte  width
        byte  height
        sbyte x offset
        sbyte y offset
        bitmap: width*height pixels, MSB-first continuous bitstream, "bits per pixel" bits each;
                pixel value 0 = transparent, non-zero = ink (mapped through the color map in-game).

    Read-only decode: the original bytes are kept and written back verbatim on save, so the
    game file always round-trips byte-identically. Glyphs are rendered on demand for the viewer.
    */
    public class Charset : BlockBase
    {
        // Char offsets in the table are relative to this position in the block body.
        private const int OffsetBase = 0x15;
        private const int TableStart = 0x19;

        public Charset(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "CHAR"; }
        }

        public byte[] RawContent { get; set; }

        public byte[] ColorMap { get; private set; }
        public int BitsPerPixel { get; private set; }
        public int FontHeight { get; private set; }
        public int NumChars { get; private set; }
        public List<Glyph> Glyphs { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - 8));
            ParseForDisplay();
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }

        private void ParseForDisplay()
        {
            Glyphs = new List<Glyph>();
            ColorMap = new byte[0];
            if (RawContent.Length < TableStart) return;

            ColorMap = new byte[15];
            Array.Copy(RawContent, 0x06, ColorMap, 0, 15);
            BitsPerPixel = RawContent[0x15];
            FontHeight = RawContent[0x16];
            NumChars = RawContent[0x17] | (RawContent[0x18] << 8);

            for (int i = 0; i < NumChars; i++)
            {
                int p = TableStart + i * 4;
                if (p + 4 > RawContent.Length) break;

                uint off = ReadUInt32(p);
                var glyph = new Glyph { Index = i };

                if (off != 0)
                {
                    int hp = OffsetBase + (int)off;
                    if (hp + 4 <= RawContent.Length)
                    {
                        glyph.Present = true;
                        glyph.DataOffset = hp;
                        glyph.Width = RawContent[hp];
                        glyph.Height = RawContent[hp + 1];
                        glyph.XOffset = (sbyte)RawContent[hp + 2];
                        glyph.YOffset = (sbyte)RawContent[hp + 3];
                    }
                }

                Glyphs.Add(glyph);
            }
        }

        /// <summary>
        /// Renders a glyph to a 32bpp bitmap (ink opaque, background transparent), or null when
        /// the glyph is absent/empty. Higher bit depths render as grayscale by pixel value.
        /// </summary>
        public Bitmap RenderGlyph(Glyph glyph)
        {
            if (glyph == null || !glyph.Present || glyph.Width <= 0 || glyph.Height <= 0) return null;
            if (BitsPerPixel <= 0 || BitsPerPixel > 8) return null;

            var bitmap = new Bitmap(glyph.Width, glyph.Height, PixelFormat.Format32bppArgb);
            int bitPos = (glyph.DataOffset + 4) * 8; // bitmap starts after the 4-byte glyph header
            int maxVal = (1 << BitsPerPixel) - 1;

            for (int y = 0; y < glyph.Height; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    int value = ReadBits(ref bitPos, BitsPerPixel);
                    if (value == 0)
                    {
                        bitmap.SetPixel(x, y, Color.Transparent);
                        continue;
                    }
                    // 1bpp -> solid ink; deeper -> grayscale ramp (darker = more ink).
                    int level = BitsPerPixel == 1 ? 0 : 255 - (255 * value / maxVal);
                    bitmap.SetPixel(x, y, Color.FromArgb(255, level, level, level));
                }
            }
            return bitmap;
        }

        public int PresentGlyphCount()
        {
            int n = 0;
            if (Glyphs != null)
                foreach (var g in Glyphs) if (g.Present) n++;
            return n;
        }

        private int ReadBits(ref int bitPos, int count)
        {
            int value = 0;
            for (int i = 0; i < count; i++)
            {
                int bytePos = bitPos >> 3;
                int bit = 7 - (bitPos & 7);
                int b = bytePos < RawContent.Length ? ((RawContent[bytePos] >> bit) & 1) : 0;
                value = (value << 1) | b;
                bitPos++;
            }
            return value;
        }

        private uint ReadUInt32(int p)
        {
            return (uint)(RawContent[p] | (RawContent[p + 1] << 8) | (RawContent[p + 2] << 16) | (RawContent[p + 3] << 24));
        }

        /// <summary>Re-parses the structural info after RawContent is replaced (PNG import).</summary>
        public void Reparse()
        {
            ParseForDisplay();
        }
    }

    public class Glyph
    {
        public int Index { get; set; }
        public bool Present { get; set; }
        public int DataOffset { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int XOffset { get; set; }
        public int YOffset { get; set; }
    }
}
