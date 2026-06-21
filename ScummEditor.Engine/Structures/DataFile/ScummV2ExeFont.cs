using System;
using System.Collections.Generic;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// The SCUMM v2 (Maniac Mansion / Zak McKracken) text font, which lives INSIDE the game executable
    /// (MANIAC.EXE / ZAK.EXE), not in the LFL data. It is an 8x8 1-bpp glyph table (8 bytes/glyph, bit 7 =
    /// leftmost - the same layout as <see cref="CharsetV3"/>), but stored RLE-COMPRESSED with the encoding
    /// ScummVM documents in charset-fontdata.cpp (the dead #if-0 block): walking the stream at offset o,
    /// a 7-byte token [x][00][B2][value][count][00][B0] emits <value> <count> times (the leading x is
    /// ignored); any other byte is a literal that emits itself.
    ///
    /// The stream is located by a unique 16-byte signature (decoded glyphs 1+2, the box-drawing chars),
    /// never a hardcoded offset, since the offset varies by edition. It decodes to 1016 bytes = glyphs
    /// 1..127 (code 0 is a blank glyph that is NOT physically stored). Character c renders at fontPtr+c*8
    /// (no base offset), so an editable buffer is 128 glyphs x 8 = 1024 bytes with a synthetic zero glyph 0.
    ///
    /// Editing is an IN-PLACE, same-size splice: each accent-candidate glyph is stored as literal bytes, so
    /// changing it overwrites 8 bytes at their literal file offsets without touching the compressed runs or
    /// the EXE length (the executable is an unpacked MZ image with no relocations). An edit that would land
    /// on a run byte, or that re-decodes to anything other than the intended glyphs (a marker collision), is
    /// refused rather than risk corrupting the executable. NOTE: ScummVM ignores this font (it hardcodes its
    /// own); an edit here is visible only under the original DOS engine (DOSBox).
    /// </summary>
    public class ScummV2ExeFont
    {
        /// <summary>Decoded glyphs 1+2 (box-drawing chars); unique in every shipped v2 EXE edition.</summary>
        private static readonly byte[] Signature =
        {
            0x01, 0x03, 0x06, 0x0C, 0x18, 0x3E, 0x03, 0x00,
            0x80, 0xC0, 0x60, 0x30, 0x18, 0x7C, 0xC0, 0x00
        };

        public const int GlyphCount = 128;       // character codes 0..127 (0 = synthetic blank)
        public const int GlyphHeight = 8;         // 8 rows
        public const int GlyphBytesEach = 8;      // 1 byte per row
        private const int StoredGlyphs = 127;     // glyphs 1..127 are physically in the EXE
        private const int DecodedBytes = StoredGlyphs * GlyphBytesEach; // 1016

        /// <summary>The whole executable; replaced (same length) when an edit is applied.</summary>
        public byte[] ExeBytes { get; private set; }

        /// <summary>File offset where the compressed font stream begins (== start of glyph 1).</summary>
        public int StreamStart { get; private set; }

        /// <summary>Bytes the compressed stream consumes to produce the 1016 decoded bytes.</summary>
        public int CompressedLength { get; private set; }

        /// <summary>128 glyphs x 8 bytes: glyph 0 is a synthetic blank, glyphs 1..127 are the decoded font.</summary>
        public byte[] GlyphBytes { get; private set; }

        // For each of the 1016 decoded bytes (glyphs 1..127), the EXE file offset of its literal source,
        // or -1 when it was produced by a compressed run (and so cannot be patched in place).
        private int[] _sourceOffset;

        private ScummV2ExeFont() { }

        /// <summary>The file offset of the font signature, or -1 if not found (e.g. a packed EXE).</summary>
        public static int Locate(byte[] exeBytes)
        {
            if (exeBytes == null || exeBytes.Length < Signature.Length) return -1;
            int last = exeBytes.Length - Signature.Length;
            for (int i = 0; i <= last; i++)
            {
                int k = 0;
                while (k < Signature.Length && exeBytes[i + k] == Signature[k]) k++;
                if (k == Signature.Length) return i;
            }
            return -1;
        }

        /// <summary>
        /// Locates and decodes the font in an executable's bytes. Returns null (with a reason) when the
        /// signature is absent or the stream cannot be decoded (truncated / packed EXE).
        /// </summary>
        public static ScummV2ExeFont Read(byte[] exeBytes, out string error)
        {
            error = null;
            int start = Locate(exeBytes);
            if (start < 0)
            {
                error = "the v2 font signature was not found in this executable (it may be packed/compressed or not a v2 game EXE).";
                return null;
            }

            int consumed;
            int[] source;
            byte[] decoded = Decode(exeBytes, start, out consumed, out source);
            if (decoded == null)
            {
                error = "the font data is truncated or not in the expected v2 RLE format.";
                return null;
            }

            var font = new ScummV2ExeFont
            {
                ExeBytes = exeBytes,
                StreamStart = start,
                CompressedLength = consumed,
                _sourceOffset = source,
                GlyphBytes = new byte[GlyphCount * GlyphBytesEach] // glyph 0 stays zero
            };
            Array.Copy(decoded, 0, font.GlyphBytes, GlyphBytesEach, DecodedBytes);
            return font;
        }

        /// <summary>
        /// Applies an edited 1024-byte glyph buffer (128 glyphs) by overwriting the changed glyphs' literal
        /// bytes in place. Returns false (with a reason) when an edit lands on a compressed run, would change
        /// the stream length, or fails the re-decode verification - in which case the EXE is left untouched.
        /// </summary>
        public bool TryApplyEditedGlyphs(byte[] newGlyphBytes, out string error)
        {
            error = null;
            if (newGlyphBytes == null || newGlyphBytes.Length != GlyphCount * GlyphBytesEach)
            {
                error = "the edited font must be exactly 128 glyphs (1024 bytes).";
                return false;
            }

            var patches = new List<int>();       // decoded-byte indices that changed and are literal
            var blocked = new SortedSet<int>();  // glyph codes whose change falls on a compressed run
            for (int i = 0; i < DecodedBytes; i++)
            {
                if (newGlyphBytes[GlyphBytesEach + i] == GlyphBytes[GlyphBytesEach + i]) continue; // unchanged
                if (_sourceOffset[i] >= 0) patches.Add(i);
                else blocked.Add(1 + i / GlyphBytesEach); // glyph code 1..127
            }

            if (blocked.Count > 0)
            {
                var codes = new List<string>();
                foreach (int c in blocked) codes.Add("0x" + c.ToString("X2"));
                error = "these glyph codes are stored inside a compressed run and cannot be edited in place: "
                        + string.Join(", ", codes.ToArray())
                        + ". Edit only the other glyphs (the accent/punctuation slots are all editable).";
                return false;
            }

            if (patches.Count == 0)
            {
                return true; // nothing changed (glyph 0 edits are ignored - it is not stored)
            }

            byte[] patched = (byte[])ExeBytes.Clone();
            foreach (int i in patches) patched[_sourceOffset[i]] = newGlyphBytes[GlyphBytesEach + i];

            // Verify the patched stream still decodes to exactly the intended glyphs with the same footprint
            // (a single literal change could otherwise form a spurious 00 B2 .. 00 B0 marker).
            int consumed;
            int[] source;
            byte[] reDecoded = Decode(patched, StreamStart, out consumed, out source);
            if (reDecoded == null || consumed != CompressedLength)
            {
                error = "the edit changed the compressed structure and was rejected to protect the executable.";
                return false;
            }
            for (int i = 0; i < DecodedBytes; i++)
            {
                if (reDecoded[i] != newGlyphBytes[GlyphBytesEach + i])
                {
                    error = "the edit could not be applied safely (it would be misread by the game's decoder).";
                    return false;
                }
            }

            // The font is located by the 16-byte signature, which IS the bytes of glyphs 1 and 2 (the
            // box-drawing chars). Editing those bytes would still render in the DOS engine but make the
            // font unfindable for any later re-edit, so refuse it. (These are not accent slots.)
            if (Locate(patched) != StreamStart)
            {
                error = "this edit changes glyph code 0x01 or 0x02 (the box-drawing characters the tool uses "
                        + "to find the font); edit a different slot so the font stays re-editable.";
                return false;
            }

            ExeBytes = patched;
            _sourceOffset = source;
            Array.Copy(reDecoded, 0, GlyphBytes, GlyphBytesEach, DecodedBytes);
            return true;
        }

        /// <summary>
        /// Walks the RLE stream from <paramref name="start"/> producing the 1016 decoded glyph bytes and,
        /// per output byte, the file offset of its literal source (-1 for run bytes). Returns null if the
        /// stream is truncated before 1016 bytes are produced.
        /// </summary>
        private static byte[] Decode(byte[] exe, int start, out int consumed, out int[] source)
        {
            var decoded = new byte[DecodedBytes];
            source = new int[DecodedBytes];
            int o = start, outIdx = 0;
            while (outIdx < DecodedBytes)
            {
                if (o >= exe.Length) { consumed = o - start; return null; }
                bool isRun = o + 6 < exe.Length
                             && exe[o + 1] == 0x00 && exe[o + 2] == 0xB2
                             && exe[o + 5] == 0x00 && exe[o + 6] == 0xB0;
                if (isRun)
                {
                    byte value = exe[o + 3];
                    int count = exe[o + 4];
                    for (int k = 0; k < count && outIdx < DecodedBytes; k++)
                    {
                        decoded[outIdx] = value;
                        source[outIdx] = -1;
                        outIdx++;
                    }
                    o += 7;
                }
                else
                {
                    decoded[outIdx] = exe[o];
                    source[outIdx] = o;
                    outIdx++;
                    o += 1;
                }
            }
            consumed = o - start;
            return decoded;
        }
    }
}
