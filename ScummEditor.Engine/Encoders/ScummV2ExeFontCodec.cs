using System;
using System.IO;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Exports / imports the SCUMM v2 EXE-embedded font (<see cref="ScummV2ExeFont"/>) as a 16x16 PNG atlas
    /// by reusing the v3 charset PNG codec. The v2 EXE font is just 128 contiguous 8x8 1-bpp glyphs (after
    /// RLE decode) with no header/width table, so it is wrapped in a synthetic <see cref="CharsetV3"/>
    /// (numChars=128, fontHeight=8, a flat width table) - whose glyph bit layout is identical - and then
    /// <see cref="CharsetV3PngCodec"/> handles the atlas verbatim. On import the edited glyphs are lifted
    /// back out and spliced into the executable in place by ScummV2ExeFont.
    /// </summary>
    public static class ScummV2ExeFontCodec
    {
        private const int HeaderSize = 8;

        /// <summary>Writes the EXE font as an editable PNG atlas (+ a scaled guide PNG).</summary>
        public static void ExportPng(ScummV2ExeFont font, string pngPath, string guidePath)
        {
            CharsetV3PngCodec.ExportPng(BuildCharset(font.GlyphBytes), pngPath, guidePath);
        }

        /// <summary>
        /// Imports an edited atlas and splices the changed glyphs into the executable in place. Throws
        /// ImageEncodeException if a glyph cannot be applied safely (run byte / would corrupt the EXE).
        /// Returns a human-readable summary. The caller writes <see cref="ScummV2ExeFont.ExeBytes"/> back.
        /// </summary>
        public static string ImportPng(ScummV2ExeFont font, string pngPath)
        {
            CharsetV3 charset = BuildCharset(font.GlyphBytes);
            string codecReport = CharsetV3PngCodec.ImportPng(charset, pngPath);

            byte[] edited = ExtractGlyphs(charset);
            string error;
            if (!font.TryApplyEditedGlyphs(edited, out error))
            {
                throw new ImageEncodeException(error);
            }

            string note = charset.NumChars > ScummV2ExeFont.GlyphCount
                ? " Note: slots 0x80 and above were ignored - the v2 EXE font only has codes 0x00-0x7F."
                : "";
            return codecReport + note;
        }

        /// <summary>
        /// Finds the v2 game executable (MANIAC.EXE / ZAK.EXE) in a game folder, or returns the first .exe
        /// as a fallback. Null when the folder has no executable.
        /// </summary>
        public static string FindGameExe(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return null;
            string fallback = null;
            foreach (string f in Directory.GetFiles(folder, "*.exe"))
            {
                string name = Path.GetFileName(f).ToUpperInvariant();
                if (name == "MANIAC.EXE" || name == "ZAK.EXE") return f;
                if (fallback == null) fallback = f;
            }
            return fallback;
        }

        /// <summary>Wraps the 1024-byte glyph buffer in a CharsetV3 RawContent so the PNG codec can read it.</summary>
        private static CharsetV3 BuildCharset(byte[] glyphBytes)
        {
            int numChars = ScummV2ExeFont.GlyphCount;
            int glyphTable = numChars * ScummV2ExeFont.GlyphBytesEach;
            var raw = new byte[HeaderSize + numChars + glyphTable];

            raw[6] = (byte)numChars;                         // numChars
            raw[7] = (byte)ScummV2ExeFont.GlyphHeight;       // fontHeight = 8
            for (int i = 0; i < numChars; i++)
            {
                raw[HeaderSize + i] = ScummV2ExeFont.GlyphBytesEach; // flat advance (the EXE has no width table)
            }
            Array.Copy(glyphBytes, 0, raw, HeaderSize + numChars, glyphTable);

            int sizeWord = raw.Length + 1; // the v3 codec keeps the "length + 1" size word relation
            raw[0] = (byte)sizeWord;
            raw[1] = (byte)(sizeWord >> 8);

            var charset = new CharsetV3();
            charset.LoadFromFileBytes(raw);
            return charset;
        }

        /// <summary>Lifts glyphs 0..127 out of the (possibly extended) charset back into a 1024-byte buffer.</summary>
        private static byte[] ExtractGlyphs(CharsetV3 charset)
        {
            var result = new byte[ScummV2ExeFont.GlyphCount * ScummV2ExeFont.GlyphBytesEach];
            byte[] raw = charset.RawContent;
            int glyphTable = HeaderSize + charset.NumChars;
            for (int i = 0; i < result.Length; i++)
            {
                int p = glyphTable + i;
                result[i] = (p >= 0 && p < raw.Length) ? raw[p] : (byte)0;
            }
            return result;
        }
    }
}
