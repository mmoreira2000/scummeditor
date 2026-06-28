using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// The Dig's external localized-text file (LANGUAGE.BND), where the translated in-game strings live for
    /// the non-English editions. Line-based text (verified vs ScummVM string.cpp:2042): a leading marker
    /// line `e` means the messages are XOR 0x13 encoded; `@TAG` opens a base tag (room/scope); `#NN` a
    /// subtag count (ignored); and `NNN/<message>` is string index NNN whose message (the part AFTER the
    /// slash, to end-of-line) is the (encoded) text. The runtime key is "TAG.NNN".
    ///
    /// The whole file is kept as the template (OriginalContent); only the message bytes of edited entries
    /// are re-encoded and spliced, so an unedited bundle rebuilds byte-identically. Text is stored as a
    /// Latin-1 string (a 1:1 byte&lt;-&gt;char mapping) so the round-trip is exact; the bytes are the game's
    /// DOS code page, so accents render as their code-page glyphs rather than Unicode (a future enhancement
    /// could apply the game's charmap for display).
    /// </summary>
    public class LanguageBundleFile : ILocalizedTextFile
    {
        private const byte XorKey = 0x13;

        public string FilePath { get; private set; }
        public byte[] OriginalContent { get; private set; }
        public bool IsValid { get; private set; }
        public bool Encoded { get; private set; }

        /// <summary>The CJK content marker the bundle declares, if any: 'j' Japanese, 'h' Korean (Hangul),
        /// 'c' Chinese (ScummVM string.cpp:2103-2108); '\0' for the Western (alphabetic) editions. A direct,
        /// reliable language signal that the word heuristic cannot give for the double-byte scripts.</summary>
        public char CjkMarker { get; private set; }

        private readonly List<LocalizedTextEntry> _entries = new List<LocalizedTextEntry>();
        public IReadOnlyList<LocalizedTextEntry> Entries { get { return _entries; } }
        public string FileName { get { return Path.GetFileName(FilePath); } }

        public LanguageBundleFile(string filePath)
        {
            FilePath = filePath;
        }

        public void Load(byte[] bytes)
        {
            OriginalContent = bytes;
            Parse();
        }

        private void Parse()
        {
            _entries.Clear();
            Encoded = false;
            CjkMarker = '\0';
            IsValid = false;
            byte[] b = OriginalContent;
            if (b == null || b.Length == 0) return;

            string baseTag = string.Empty;
            int pos = 0;
            while (pos < b.Length)
            {
                int contentEnd, lineEnd;
                LineBounds(b, pos, out contentEnd, out lineEnd);
                int len = contentEnd - pos;
                if (len > 0)
                {
                    byte first = b[pos];
                    if (first == (byte)'!')
                    {
                        // unknown marker - ignore
                    }
                    else if (first == (byte)'e' && len == 1)
                    {
                        Encoded = true; // messages are XOR 0x13 (h/j/c are content-type markers only, not encoding control)
                    }
                    else if (len == 1 && (first == (byte)'h' || first == (byte)'j' || first == (byte)'c'))
                    {
                        CjkMarker = (char)first; // double-byte script marker: 'h' Korean, 'j' Japanese, 'c' Chinese
                    }
                    else if (first == (byte)'@')
                    {
                        baseTag = Encoding.Latin1.GetString(b, pos + 1, len - 1);
                    }
                    else if (first == (byte)'#')
                    {
                        // subtag count - ignore
                    }
                    else if (first >= (byte)'0' && first <= (byte)'9')
                    {
                        int slash = IndexOf(b, pos, contentEnd, (byte)'/');
                        if (slash >= 0)
                        {
                            string idx = Encoding.Latin1.GetString(b, pos, slash - pos);
                            int msgStart = slash + 1;
                            _entries.Add(new LocalizedTextEntry
                            {
                                Key = baseTag + "." + idx,
                                Text = Decode(b, msgStart, contentEnd),
                                TextStart = msgStart,
                                TextEnd = contentEnd,
                            });
                        }
                    }
                    // any other line: ignored here, copied verbatim by BuildContent
                }
                pos = lineEnd;
            }
            IsValid = _entries.Count > 0;
        }

        /// <summary>Rebuilds the file bytes: everything verbatim except each entry's message region, which is
        /// re-encoded from the (possibly edited) Text. Byte-identical when no Text changed.</summary>
        public byte[] BuildContent()
        {
            using (var ms = new MemoryStream())
            {
                int pos = 0;
                foreach (LocalizedTextEntry e in _entries)
                {
                    ms.Write(OriginalContent, pos, e.TextStart - pos); // verbatim up to (and incl) the "NNN/"
                    byte[] enc = Encode(e.Text);
                    ms.Write(enc, 0, enc.Length);
                    pos = e.TextEnd;                                   // skip the original message bytes
                }
                ms.Write(OriginalContent, pos, OriginalContent.Length - pos); // tail
                return ms.ToArray();
            }
        }

        public string ExportToText()
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(FileName).Append(" - one line per string: KEY<TAB>TEXT. Edit the TEXT only.\r\n");
            foreach (LocalizedTextEntry e in _entries)
            {
                sb.Append(e.Key).Append('\t').Append(NormalizeSingleLine(e.Text)).Append("\r\n");
            }
            return sb.ToString();
        }

        public string ImportFromText(string content)
        {
            var map = new Dictionary<string, string>();
            // Normalise every line ending (incl. a lone CR) before splitting, so a dump saved by any editor
            // parses; a BND message is single-line, so collapse any stray newline in the value to a space.
            foreach (string raw in content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                int tab = raw.IndexOf('\t');
                if (tab <= 0) continue;
                map[raw.Substring(0, tab)] = NormalizeSingleLine(raw.Substring(tab + 1));
            }

            int changed = 0, unmappable = 0;
            foreach (LocalizedTextEntry e in _entries)
            {
                string text;
                if (map.TryGetValue(e.Key, out text) && text != e.Text)
                {
                    e.Text = text;
                    changed++;
                    unmappable += LocalizedTextEntry.CountUnmappable(text);
                }
            }
            string report = string.Format("{0} of {1} strings updated.", changed, _entries.Count);
            if (unmappable > 0)
            {
                report += string.Format("\nWARNING: {0} character(s) are outside the game's 8-bit code page and will be saved as '?'.", unmappable);
            }
            return report;
        }

        /// <inheritdoc/>
        public string NormalizeEditedText(string text)
        {
            return NormalizeSingleLine(text);
        }

        private string Decode(byte[] b, int start, int end)
        {
            int len = end - start;
            var msg = new byte[len];
            for (int i = 0; i < len; i++)
            {
                msg[i] = Encoded ? (byte)(b[start + i] ^ XorKey) : b[start + i];
            }
            return Encoding.Latin1.GetString(msg);
        }

        private byte[] Encode(string text)
        {
            byte[] msg = Encoding.Latin1.GetBytes(NormalizeSingleLine(text));
            if (Encoded)
            {
                for (int i = 0; i < msg.Length; i++) msg[i] ^= XorKey;
            }
            return msg;
        }

        /// <summary>LANGUAGE.BND holds exactly one message per line - ScummVM reads a message only up to the
        /// first CR/LF (string.cpp:2135) - so any newline the user typed in the editor is collapsed to a
        /// space, both on export and when the bytes are rebuilt, to keep the file's line-based structure
        /// intact (a raw newline would split one message into two on the next load).</summary>
        private static string NormalizeSingleLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
        }

        /// <summary>Finds the content end (before the line terminator) and the start of the next line.</summary>
        private static void LineBounds(byte[] b, int pos, out int contentEnd, out int lineEnd)
        {
            int i = pos;
            while (i < b.Length && b[i] != (byte)'\r' && b[i] != (byte)'\n') i++;
            contentEnd = i;
            if (i < b.Length && b[i] == (byte)'\r') i++;
            if (i < b.Length && b[i] == (byte)'\n') i++;
            lineEnd = i;
        }

        private static int IndexOf(byte[] b, int start, int end, byte value)
        {
            for (int i = start; i < end; i++) if (b[i] == value) return i;
            return -1;
        }
    }
}
