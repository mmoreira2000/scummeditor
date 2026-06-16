using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ScummEditor.Encoders
{
    /*
    Converts SCUMM message bytes to/from a translator-friendly display form.

    In-game text bytes are glyph indexes into the active CHAR (charset) resource, so there is
    no fixed encoding: each translation project decides which byte holds which accented glyph.
    The codec carries that decision as a character map (Unicode char <-> game byte). The map is
    serialized into the "; charmap:" header line of the export file and is rebuilt from that
    line on import - the file is the single source of truth, so the translation team can remap
    free font slots by editing the header (and/or use literal {0xNN} tokens in the text, e.g.
    "Cla{0xC1}e" to print a custom 'ss' ligature drawn at slot 0xC1).

    Display form rules:
      - ASCII 0x20..0x7E pass through ('{' and '}' are doubled: "{{", "}}").
      - Mapped high bytes become their Unicode character (e.g. 0xE3 -> 'ã').
      - Unmapped bytes become "{0xNN}".
      - 0xFF/0xFE escape sequences become tokens:
          {br} {keep} {wait} {e8}                      (codes 1, 2, 3, 8 - no argument)
          {int:N} {verb:N} {name:N} {str:N} {anim:N}
          {sound:N} {color:N} {unk13:N} {font:N}       (codes 4..14 - 16-bit argument)
          {escNN:N}                                    (any other code)
        A 0xFE prefix uses the same tokens with a "fe-" prefix, e.g. {fe-br}.
    */
    public class GameTextCodec
    {
        private readonly Dictionary<byte, char> _byteToChar = new Dictionary<byte, char>();
        private readonly Dictionary<char, byte> _charToByte = new Dictionary<char, byte>();

        // FF/FE escape codes without an argument; all the others carry a 16-bit LE argument.
        private static readonly int[] NoArgCodes = { 1, 2, 3, 8 };

        private static readonly Dictionary<int, string> EscapeNames = new Dictionary<int, string>
        {
            { 1, "br" }, { 2, "keep" }, { 3, "wait" }, { 8, "e8" },
            { 4, "int" }, { 5, "verb" }, { 6, "name" }, { 7, "str" },
            { 9, "anim" }, { 10, "sound" }, { 12, "color" }, { 13, "unk13" }, { 14, "font" }
        };

        private GameTextCodec()
        {
        }

        private void Map(char ch, int b)
        {
            _byteToChar[(byte)b] = ch;
            if (!_charToByte.ContainsKey(ch)) _charToByte.Add(ch, (byte)b);
        }

        // ---------------------------------------------------------------------
        // The default map
        // ---------------------------------------------------------------------

        /// <summary>
        /// The single built-in map, written to the export header. It follows the convention of
        /// the ScummBR Sam &amp; Max translation: accents the original LEC fonts already had stay
        /// at their CP437 slots; the missing ones (ã, õ, uppercase accents...) live at their
        /// Latin-1 positions. The translation team is free to edit the "; charmap:" line of the
        /// exported file - the import always rebuilds the map from there.
        /// </summary>
        public static GameTextCodec Default()
        {
            var codec = new GameTextCodec();
            codec.Map('Ç', 0x80); codec.Map('ü', 0x81); codec.Map('é', 0x82); codec.Map('â', 0x83);
            codec.Map('à', 0x85); codec.Map('ç', 0x87); codec.Map('ê', 0x88); codec.Map('ë', 0x89);
            codec.Map('É', 0x90); codec.Map('ô', 0x93); codec.Map('á', 0xA0);
            codec.Map('À', 0xC0); codec.Map('Á', 0xC1); codec.Map('Â', 0xC2); codec.Map('Ã', 0xC3);
            codec.Map('È', 0xC8); codec.Map('Ê', 0xCA); codec.Map('Í', 0xCD); codec.Map('Ó', 0xD3);
            codec.Map('Ô', 0xD4); codec.Map('Õ', 0xD5); codec.Map('Ú', 0xDA);
            codec.Map('ã', 0xE3); codec.Map('è', 0xE8); codec.Map('í', 0xED); codec.Map('ò', 0xF2);
            codec.Map('ó', 0xF3); codec.Map('õ', 0xF5); codec.Map('ú', 0xFA); codec.Map('ý', 0xFD);
            return codec;
        }

        /// <summary>
        /// Builds a codec from a charmap line pasted by the user - either the full header line
        /// ("; charmap: é=0x82 ...") or just the pairs. Blank input returns the default map.
        /// </summary>
        public static GameTextCodec ParsePastedCharmap(string pasted)
        {
            if (pasted == null) return Default();
            string s = pasted.Trim();
            if (s.StartsWith(";")) s = s.Substring(1).TrimStart();
            if (s.StartsWith("charmap:", StringComparison.OrdinalIgnoreCase)) s = s.Substring(8).TrimStart();
            if (s.Length == 0) return Default();
            return FromMapSpec(s);
        }

        /// <summary>
        /// Builds a codec from a serialized map ("Ç=0x80 é=0x82 ..."), validating it: the team
        /// edits this line by hand, and a duplicated or reserved mapping would silently corrupt
        /// the texts on import.
        /// </summary>
        public static GameTextCodec FromMapSpec(string spec)
        {
            var codec = new GameTextCodec();
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
                    throw new FormatException("the charmap cannot remap ASCII characters: '" + token + "'");
                if (b == 0x00 || b == 0xFE || b == 0xFF)
                    throw new FormatException("reserved byte in the charmap (0x00/0xFE/0xFF): '" + token + "'");
                if (codec._charToByte.ContainsKey(ch))
                    throw new FormatException("duplicated character in the charmap: '" + ch + "'");
                if (codec._byteToChar.ContainsKey(b))
                    throw new FormatException("duplicated byte in the charmap: '0x" + b.ToString("X2") + "'");

                codec.Map(ch, b);
            }
            return codec;
        }

        /// <summary>Serializes the map for the export file header ("Ç=0x80 é=0x82 ...").</summary>
        public string ToMapSpec()
        {
            var keys = new List<byte>(_byteToChar.Keys);
            keys.Sort();
            var sb = new StringBuilder();
            foreach (byte b in keys)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(_byteToChar[b]).Append("=0x").Append(b.ToString("X2"));
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        // Bytes -> display text
        // ---------------------------------------------------------------------

        /// <summary>Decodes string content bytes (without the 0x00 terminator) to display form.</summary>
        public string Decode(byte[] buf, int offset, int contentLength)
        {
            var sb = new StringBuilder();
            int end = offset + contentLength;
            int i = offset;
            while (i < end)
            {
                byte b = buf[i];

                if ((b == 0xFF || b == 0xFE) && i + 1 < end)
                {
                    byte code = buf[i + 1];
                    string prefix = b == 0xFE ? "fe-" : "";
                    string name;
                    if (!EscapeNames.TryGetValue(code, out name)) name = "esc" + code;

                    if (Array.IndexOf(NoArgCodes, (int)code) >= 0)
                    {
                        sb.Append('{').Append(prefix).Append(name).Append('}');
                        i += 2;
                    }
                    else if (i + 3 < end)
                    {
                        int val = buf[i + 2] | (buf[i + 3] << 8);
                        sb.Append('{').Append(prefix).Append(name).Append(':').Append(val).Append('}');
                        i += 4;
                    }
                    else
                    {
                        sb.Append("{0x").Append(b.ToString("X2")).Append('}'); // truncated escape
                        i++;
                    }
                }
                else if (b == (byte)'{') { sb.Append("{{"); i++; }
                else if (b == (byte)'}') { sb.Append("}}"); i++; }
                else if (b >= 0x20 && b <= 0x7E) { sb.Append((char)b); i++; }
                else
                {
                    char ch;
                    if (_byteToChar.TryGetValue(b, out ch)) sb.Append(ch);
                    else sb.Append("{0x").Append(b.ToString("X2")).Append('}');
                    i++;
                }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        // Display text -> bytes
        // ---------------------------------------------------------------------

        /// <summary>
        /// Encodes display text back to game bytes (without terminator). Returns null and sets
        /// <paramref name="error"/> when the text contains an unmapped character or a bad token.
        /// </summary>
        public byte[] Encode(string text, out string error)
        {
            error = null;
            var bytes = new List<byte>(text.Length + 8);
            int i = 0;
            while (i < text.Length)
            {
                char ch = text[i];

                if (ch == '{')
                {
                    if (i + 1 < text.Length && text[i + 1] == '{') { bytes.Add((byte)'{'); i += 2; continue; }

                    int close = text.IndexOf('}', i + 1);
                    if (close < 0) { error = "'{' sem '}' correspondente"; return null; }
                    string token = text.Substring(i + 1, close - i - 1);
                    if (!EncodeToken(token, bytes, out error)) return null;
                    i = close + 1;
                }
                else if (ch == '}')
                {
                    if (i + 1 < text.Length && text[i + 1] == '}') { bytes.Add((byte)'}'); i += 2; continue; }
                    error = "'}' sem '{' correspondente";
                    return null;
                }
                else if (ch >= 0x20 && ch <= 0x7E) { bytes.Add((byte)ch); i++; }
                else
                {
                    byte b;
                    if (_charToByte.TryGetValue(ch, out b)) { bytes.Add(b); i++; }
                    else
                    {
                        error = "character with no charmap mapping: '" + ch + "' (U+" + ((int)ch).ToString("X4") + ")";
                        return null;
                    }
                }
            }
            return bytes.ToArray();
        }

        private bool EncodeToken(string token, List<byte> bytes, out string error)
        {
            error = null;

            // {0xNN} - literal byte
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                byte raw;
                if (!byte.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out raw))
                {
                    error = "invalid token: {" + token + "}";
                    return false;
                }
                bytes.Add(raw);
                return true;
            }

            byte prefix = 0xFF;
            string body = token;
            if (body.StartsWith("fe-")) { prefix = 0xFE; body = body.Substring(3); }

            string name = body;
            string argText = null;
            int colon = body.IndexOf(':');
            if (colon >= 0)
            {
                name = body.Substring(0, colon);
                argText = body.Substring(colon + 1);
            }

            int code = -1;
            foreach (KeyValuePair<int, string> kv in EscapeNames)
                if (kv.Value == name) { code = kv.Key; break; }
            if (code < 0 && name.StartsWith("esc"))
            {
                int n;
                if (int.TryParse(name.Substring(3), out n) && n >= 0 && n <= 255) code = n;
            }
            if (code < 0)
            {
                error = "unknown token: {" + token + "}";
                return false;
            }

            bool hasArg = Array.IndexOf(NoArgCodes, code) < 0;
            if (hasArg != (argText != null))
            {
                error = "token {" + token + "} " + (hasArg ? "requires an" : "does not take an") + " argument";
                return false;
            }

            bytes.Add(prefix);
            bytes.Add((byte)code);
            if (hasArg)
            {
                int val;
                if (!int.TryParse(argText, out val) || val < 0 || val > 0xFFFF)
                {
                    error = "invalid argument in {" + token + "}";
                    return false;
                }
                bytes.Add((byte)(val & 0xFF));
                bytes.Add((byte)(val >> 8));
            }
            return true;
        }
    }
}
