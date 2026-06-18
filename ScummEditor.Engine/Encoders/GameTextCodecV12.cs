using System.Text;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Encodes/decodes the SCUMM v1/v2 inline-string format (Maniac Mansion, Zak McKracken). It is NOT
    /// the v3-v6 0xFF/0xFE escape scheme - each byte is: bit 0x80 = "append a trailing space", the low 7
    /// bits are the glyph, and a low value &lt; 8 is a control code that takes ONE extra (raw) byte when it
    /// is &gt; 3 (codes 1-3 take none). Mirrors ScummVM ScummEngine_v2::decodeParseString / descumm
    /// do_decodeparsestring_v2.
    ///
    /// The readable form uses {xNN} tokens for control bytes (and the following {xAA} token for a 4-7
    /// code's argument), printable ASCII verbatim, and a real space for a trailing-space flag. On encode a
    /// space is written as a literal 0x20 byte (a valid space glyph that the engine renders identically to
    /// the folded 0x80-bit form); unchanged strings are never re-encoded by the importer (it compares the
    /// decoded text), so the original folded-space bytes are preserved byte-for-byte.
    /// </summary>
    public class GameTextCodecV12
    {
        public static GameTextCodecV12 Default()
        {
            return new GameTextCodecV12();
        }

        /// <summary>Decodes <paramref name="contentLength"/> bytes at <paramref name="offset"/> into the readable token form.</summary>
        public string Decode(byte[] buf, int offset, int contentLength)
        {
            var sb = new StringBuilder();
            int end = offset + contentLength;
            for (int i = offset; i < end && i < buf.Length; i++)
            {
                byte b = buf[i];
                bool trailingSpace = (b & 0x80) != 0;
                int c = b & 0x7F;
                if (c < 8)
                {
                    sb.Append("{x").Append(c.ToString("X2")).Append('}');
                    if (c > 3 && i + 1 < end && i + 1 < buf.Length)
                    {
                        i++;
                        sb.Append("{x").Append(buf[i].ToString("X2")).Append('}');
                    }
                }
                else if (c == '{')
                {
                    sb.Append("{{"); // escape a literal brace so the token syntax stays unambiguous
                }
                else
                {
                    sb.Append((char)c);
                }
                if (trailingSpace) sb.Append(' ');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Encodes the readable token form back to v1/v2 string bytes (no terminator - the caller adds it).
        /// Returns null with an error when a character cannot be represented (only 7-bit glyphs 0x20-0x7E,
        /// spaces, {{ and {xNN} tokens are encodable; accented letters need the per-language glyph mapping
        /// added with the font work).
        /// </summary>
        public byte[] Encode(string text, out string error)
        {
            error = null;
            var bytes = new System.Collections.Generic.List<byte>();
            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];
                if (ch == '{')
                {
                    if (i + 1 < text.Length && text[i + 1] == '{') { bytes.Add((byte)'{'); i += 2; continue; }
                    // a {xNN} control token
                    if (i + 4 < text.Length && text[i + 1] == 'x' && text[i + 4] == '}')
                    {
                        int v;
                        if (!int.TryParse(text.Substring(i + 2, 2), System.Globalization.NumberStyles.HexNumber, null, out v))
                        {
                            error = "malformed control token at " + i;
                            return null;
                        }
                        bytes.Add((byte)v);
                        i += 5;
                        continue;
                    }
                    error = "unterminated '{' token at " + i;
                    return null;
                }
                if (ch >= 0x20 && ch <= 0x7E)
                {
                    bytes.Add((byte)ch);
                    i++;
                    continue;
                }
                error = "character '" + ch + "' (0x" + ((int)ch).ToString("X2") + ") is not encodable in a v2 string";
                return null;
            }
            return bytes.ToArray();
        }
    }
}
