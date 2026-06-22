using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Exports / imports the per-glyph X,Y DRAW OFFSETS of a SCUMM CHAR font (the editor's equivalent of
    /// scummtr's FontXY tool). Each glyph header is [width][height][xOffset:sbyte][yOffset:sbyte] + bitmap;
    /// the bitmap (and therefore width/height) is edited through the PNG atlas, but the X/Y offsets - which
    /// position / kern the glyph and which the PNG import only re-derives from ink bounds - can be tuned
    /// independently here. The edit is SIZE-NEUTRAL (it patches the two offset bytes in place), so no block
    /// relocation is needed; the parent re-saves the charset's RawContent verbatim.
    ///
    /// Text format: one line per PRESENT glyph, "&lt;hexIndex&gt;: &lt;xOffset&gt; &lt;yOffset&gt;", with a read-only
    /// "; WxH" width/height note. The index is the 2-digit HEX glyph slot id, matching the cell labels in
    /// the guide PNG, so a translator can line up each row with the glyph it sees. Lines starting with ';'
    /// and blank lines are ignored. Absent glyphs are omitted. Re-importing an unmodified export changes nothing.
    /// </summary>
    public static class FontMetricsCodec
    {
        public static string Export(Charset charset)
        {
            var sb = new StringBuilder();
            sb.Append("; SCUMM font glyph metrics - X/Y draw offsets (like scummtr FontXY).\r\n");
            sb.Append("; The index is the HEX glyph slot id (same as the cell labels in the guide PNG).\r\n");
            sb.Append("; Edit the two numbers after each index: <hexIndex>: <xOffset> <yOffset>   (range -128..127).\r\n");
            sb.Append("; The 'WxH' note is read-only - width/height come from the glyph bitmap (edit via the PNG atlas).\r\n");
            if (charset == null || charset.Glyphs == null) return sb.ToString();

            foreach (Glyph g in charset.Glyphs)
            {
                if (!g.Present) continue;
                sb.Append(g.Index.ToString("X2")).Append(": ")
                  .Append(g.XOffset).Append(' ').Append(g.YOffset)
                  .Append("   ; ").Append(g.Width).Append('x').Append(g.Height)
                  .Append("\r\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Applies edited metrics to the charset's RawContent in place. Returns the number of glyphs changed;
        /// <paramref name="errors"/> collects per-line problems (unknown / absent glyph, out-of-range value,
        /// malformed line) without aborting the rest.
        /// </summary>
        public static int Import(Charset charset, string text, out List<string> errors)
        {
            errors = new List<string>();
            int changed = 0;
            if (charset == null || charset.RawContent == null || text == null) return 0;

            var byIndex = new Dictionary<int, Glyph>();
            if (charset.Glyphs != null)
                foreach (Glyph g in charset.Glyphs) byIndex[g.Index] = g;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int ln = 0; ln < lines.Length; ln++)
            {
                string line = lines[ln].Trim();
                if (line.Length == 0 || line[0] == ';') continue;

                // strip an inline "; WxH" note
                int semi = line.IndexOf(';');
                if (semi >= 0) line = line.Substring(0, semi).Trim();
                if (line.Length == 0) continue;

                int colon = line.IndexOf(':');
                if (colon <= 0) { errors.Add(Line(ln) + "expected '<hexIndex>: <x> <y>'"); continue; }

                // The index is hex (matching the guide PNG cell labels); tolerate an optional 0x prefix.
                string idText = line.Substring(0, colon).Trim();
                if (idText.StartsWith("0x") || idText.StartsWith("0X")) idText = idText.Substring(2);
                int index;
                if (!int.TryParse(idText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out index))
                { errors.Add(Line(ln) + "invalid glyph index (expected hex, e.g. E3)"); continue; }

                string[] parts = line.Substring(colon + 1).Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                int x, y;
                if (parts.Length != 2 ||
                    !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
                { errors.Add(Line(ln) + "expected two numbers '<x> <y>'"); continue; }

                if (x < -128 || x > 127 || y < -128 || y > 127)
                { errors.Add(Line(ln) + "offset out of range (-128..127)"); continue; }

                Glyph glyph;
                if (!byIndex.TryGetValue(index, out glyph) || !glyph.Present)
                { errors.Add(Line(ln) + "glyph " + index + " is absent in this font"); continue; }

                int p = glyph.DataOffset; // [+0 w][+1 h][+2 xOff][+3 yOff]
                if (p + 4 > charset.RawContent.Length) { errors.Add(Line(ln) + "glyph " + index + " is out of bounds"); continue; }

                byte nx = unchecked((byte)(sbyte)x), ny = unchecked((byte)(sbyte)y);
                if (charset.RawContent[p + 2] != nx || charset.RawContent[p + 3] != ny)
                {
                    charset.RawContent[p + 2] = nx;
                    charset.RawContent[p + 3] = ny;
                    changed++;
                }
            }

            if (changed > 0) charset.Reparse();
            return changed;
        }

        private static string Line(int zeroBased)
        {
            return "line " + (zeroBased + 1) + ": ";
        }
    }
}
