using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// space is written as a literal 0x20 byte (a valid space glyph the engine renders identically to the
    /// folded 0x80-bit form). NOTE: the importer skips a string by comparing the RE-ENCODED BYTES to the
    /// original, so a string whose original bytes used the folded 0x80-space form re-encodes to literal
    /// 0x20 bytes (render-identical, but one byte longer per folded space) and is treated as changed; for a
    /// name/verb bounded by a 1-byte offset that growth can hit the "left unchanged" path - safe (no
    /// corruption), just not byte-minimal.
    ///
    /// ACCENT MAP: v2 fonts are 7-bit (codes 0..127) with no free slots above 0x7E, so a translation that
    /// needs accented letters REUSES rarely-used punctuation/symbol slots - the translator redraws those
    /// glyphs in the EXE font (see ScummV2ExeFont) and maps each accented letter to its slot byte here. The
    /// map is bidirectional: decode shows the accent instead of the punctuation, encode emits the slot byte.
    /// It is serialized to the export file's "; charmap:" line (same syntax as the v3-v6 GameTextCodec) so
    /// the team can tune it. The cursor advance is a fixed 8px cell in v2 (verified in DOSBox), so a
    /// repurposed slot needs no width change.
    /// </summary>
    public class GameTextCodecV12
    {
        private readonly Dictionary<char, byte> _accentToByte = new Dictionary<char, byte>();
        private readonly Dictionary<byte, char> _byteToAccent = new Dictionary<byte, char>();

        /// <summary>The plain v1/v2 codec with no accent remapping (printable ASCII only).</summary>
        public static GameTextCodecV12 Default()
        {
            return new GameTextCodecV12();
        }

        /// <summary>
        /// A starting Portuguese accent map. Each accented letter is parked on a symbol slot that NEVER
        /// appears as a literal glyph in the shipped Maniac/Zak text (verified empirically; see the
        /// V2AccentCharmapTests regression that re-checks this against the games), so decoding the original
        /// text shows no false accents and the team can tell their typed accents from script punctuation.
        /// The team redraws those slots in the EXE font and edits the "; charmap:" line to match. Uppercase
        /// accents are left unmapped (no free slots remain) - add them only on a verified-free slot.
        /// </summary>
        public static GameTextCodecV12 Portuguese()
        {
            // Most common accents on the most exotic slots; rarer ones (à â õ ô ú ü) on the rest. The brace
            // slots {/} are deliberately avoided (they collide with the {token} / {{ escape syntax).
            return FromAccentSpec("á=0x7E ã=0x7C ç=0x5C é=0x5B ê=0x5D í=0x5F ó=0x3C ô=0x3E õ=0x3D ú=0x2A â=0x2F à=0x25 ü=0x22");
        }

        /// <summary>
        /// Builds a codec from a serialized accent map ("á=0x7E ç=0x5C ..."); blank/null gives the plain
        /// codec. Each byte must be a printable glyph slot (0x21..0x7E) that is NOT a letter or digit (those
        /// carry real text), and each character must be non-ASCII (an accented letter). Throws on a
        /// malformed, duplicated, or out-of-range entry.
        /// </summary>
        public static GameTextCodecV12 FromAccentSpec(string spec)
        {
            var codec = new GameTextCodecV12();
            if (string.IsNullOrWhiteSpace(spec)) return codec;

            foreach (string token in spec.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = token.IndexOf('=');
                if (eq != 1) throw new FormatException("invalid charmap entry: '" + token + "' (expected 'character=0xNN')");
                char ch = token[0];
                string hex = token.Substring(eq + 1);
                if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
                byte b;
                if (!byte.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                    throw new FormatException("invalid byte in the charmap: '" + token + "'");

                if (ch <= 0x7E)
                    throw new FormatException("a v2 charmap maps an accented (non-ASCII) letter: '" + token + "'");
                if (b < 0x21 || b > 0x7E)
                    throw new FormatException("the slot must be a printable glyph 0x21-0x7E: '" + token + "'");
                if (IsLetterOrDigit(b))
                    throw new FormatException("slot 0x" + b.ToString("X2") + " is a letter/digit and carries real text; pick a punctuation slot: '" + token + "'");
                if (b == 0x7B || b == 0x7D)
                    throw new FormatException("slots 0x7B/0x7D ('{' '}') collide with the {token} / {{ escape syntax; pick another slot: '" + token + "'");
                if (codec._accentToByte.ContainsKey(ch))
                    throw new FormatException("duplicated character in the charmap: '" + ch + "'");
                if (codec._byteToAccent.ContainsKey(b))
                    throw new FormatException("duplicated slot in the charmap: '0x" + b.ToString("X2") + "'");

                codec._accentToByte[ch] = b;
                codec._byteToAccent[b] = ch;
            }
            return codec;
        }

        /// <summary>Serializes the accent map for the export header ("á=0x7E ç=0x5C ..."); empty when none.</summary>
        public string ToAccentSpec()
        {
            var keys = new List<byte>(_byteToAccent.Keys);
            keys.Sort();
            var sb = new StringBuilder();
            foreach (byte b in keys)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(_byteToAccent[b]).Append("=0x").Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        private static bool IsLetterOrDigit(byte b)
        {
            return (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9');
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
                char accent;
                if (c < 8)
                {
                    sb.Append("{x").Append(c.ToString("X2")).Append('}');
                    if (c > 3 && i + 1 < end && i + 1 < buf.Length)
                    {
                        i++;
                        sb.Append("{x").Append(buf[i].ToString("X2")).Append('}');
                    }
                }
                else if (_byteToAccent.TryGetValue((byte)c, out accent))
                {
                    sb.Append(accent); // a punctuation slot repurposed as an accented letter
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
                        // 0x00 is the string terminator in every SCUMM version; embedding it mid-string would
                        // truncate the game text at runtime (mirrors the v3-v6/v8 GameTextCodec guard).
                        if (v == 0)
                        {
                            error = "cannot embed {x00} (the string terminator) in text";
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
                    // A literal symbol whose slot was repurposed for an accent cannot be emitted as itself
                    // (the engine renders the redrawn accent glyph there); reject it instead of silently
                    // turning the user's '~' into 'á'. Accented letters take the dedicated branch below.
                    char accentAtSlot;
                    if (_byteToAccent.TryGetValue((byte)ch, out accentAtSlot))
                    {
                        error = "character '" + ch + "' (0x" + ((int)ch).ToString("X2")
                                + ") is used by this translation as the accent '" + accentAtSlot
                                + "' - it cannot also be printed literally; remove it or remap the charmap";
                        return null;
                    }
                    bytes.Add((byte)ch);
                    i++;
                    continue;
                }
                byte slot;
                if (_accentToByte.TryGetValue(ch, out slot))
                {
                    bytes.Add(slot); // accented letter -> its repurposed punctuation slot
                    i++;
                    continue;
                }
                error = "character '" + ch + "' (U+" + ((int)ch).ToString("X4")
                        + ") has no font slot - add it to the '; charmap:' line and redraw that slot in the EXE font";
                return null;
            }
            return bytes.ToArray();
        }
    }
}
