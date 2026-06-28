using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// The SCUMM v8 (The Curse of Monkey Island) LANGUAGE.TAB - the external localized text the game shows
    /// for almost every line. It is a plain, unencrypted, line-based file: each line is `TAG&lt;TAB&gt;text`
    /// (TAG an up-to-8-char string id; verified vs ScummVM ScummEngine::loadLanguageBundle). Text is stored
    /// as Latin-1 for an exact byte round-trip; the whole file is the template and only an edited entry's
    /// TEXT region (the bytes after the TAB, up to the line terminator) is spliced, so an unedited file
    /// rebuilds byte-identically and the line terminators / TAG bytes are never touched.
    ///
    /// The text can contain %varname% placeholders the engine substitutes at runtime - they must be kept
    /// verbatim in a translation (treated as ordinary text here). One string per line: a newline in an
    /// edited value is collapsed to a space so the line structure stays intact.
    /// </summary>
    public class LanguageTabFile : ILocalizedTextFile
    {
        public string FilePath { get; private set; }
        public bool IsValid { get; private set; }
        public string FileName { get { return Path.GetFileName(FilePath); } }

        private byte[] _body; // the whole file - the template the entries index into
        private readonly List<LocalizedTextEntry> _entries = new List<LocalizedTextEntry>();

        public IReadOnlyList<LocalizedTextEntry> Entries { get { return _entries; } }

        public LanguageTabFile(string filePath)
        {
            FilePath = filePath;
        }

        public void Load(byte[] bytes)
        {
            _body = bytes ?? System.Array.Empty<byte>();
            _entries.Clear();
            IsValid = false;
            if (_body.Length == 0) return;

            var usedKeys = new HashSet<string>();
            int pos = 0, index = 0;
            while (pos < _body.Length)
            {
                int contentEnd, lineEnd;
                LineBounds(_body, pos, out contentEnd, out lineEnd);

                int tab = IndexOfTab(_body, pos, contentEnd);
                if (tab > pos) // a "TAG<TAB>text" line (lines without a TAB stay in the template, not editable)
                {
                    string key = Encoding.Latin1.GetString(_body, pos, tab - pos).Trim();
                    if (key.Length == 0) key = "line" + index;
                    if (!usedKeys.Add(key)) key = key + " #" + index; // disambiguate the rare duplicate id

                    int textStart = tab + 1;
                    _entries.Add(new LocalizedTextEntry
                    {
                        Key = key,
                        Text = Encoding.Latin1.GetString(_body, textStart, contentEnd - textStart),
                        TextStart = textStart,
                        TextEnd = contentEnd, // the text region excludes the line terminator
                    });
                }

                index++;
                pos = lineEnd;
            }

            IsValid = _entries.Count > 0;
        }

        public byte[] BuildContent()
        {
            using (var ms = new MemoryStream())
            {
                int pos = 0;
                foreach (LocalizedTextEntry e in _entries)
                {
                    ms.Write(_body, pos, e.TextStart - pos);                 // TAG + TAB (and everything before), verbatim
                    byte[] text = Encoding.Latin1.GetBytes(NormalizeEditedText(e.Text ?? string.Empty));
                    ms.Write(text, 0, text.Length);
                    pos = e.TextEnd;                                         // skip the original text region
                }
                ms.Write(_body, pos, _body.Length - pos);                   // the trailing line terminator + any tail
                return ms.ToArray();
            }
        }

        public string ExportToText()
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(FileName).Append(" - KEY<TAB>TEXT, one per line. Edit TEXT only; keep %tokens% intact.\r\n");
            foreach (LocalizedTextEntry e in _entries)
            {
                sb.Append(e.Key).Append('\t').Append(Escape(e.Text)).Append("\r\n");
            }
            return sb.ToString();
        }

        public string ImportFromText(string content)
        {
            var map = new Dictionary<string, string>();
            foreach (string raw in content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                int tab = raw.IndexOf('\t');
                if (tab <= 0) continue;
                map[raw.Substring(0, tab)] = Unescape(raw.Substring(tab + 1));
            }

            int changed = 0, unmappable = 0;
            foreach (LocalizedTextEntry e in _entries)
            {
                string text;
                if (!map.TryGetValue(e.Key, out text) || text == e.Text) continue;
                e.Text = NormalizeEditedText(text);
                changed++;
                unmappable += LocalizedTextEntry.CountUnmappable(e.Text);
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
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            // LANGUAGE.TAB is strictly one string per line, so a newline in the value would split the line
            // and shift every later entry; collapse any newline to a space.
            return text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
        }

        // ---- helpers ----

        private static int IndexOfTab(byte[] b, int start, int end)
        {
            for (int i = start; i < end; i++) if (b[i] == (byte)'\t') return i;
            return -1;
        }

        private static void LineBounds(byte[] b, int pos, out int contentEnd, out int lineEnd)
        {
            int i = pos;
            while (i < b.Length && b[i] != (byte)'\r' && b[i] != (byte)'\n') i++;
            contentEnd = i;
            if (i < b.Length && b[i] == (byte)'\r') i++;
            if (i < b.Length && b[i] == (byte)'\n') i++;
            lineEnd = i;
        }

        private static string Escape(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\t", "\\t");
        }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    if (n == 't') sb.Append('\t');
                    else if (n == '\\') sb.Append('\\');
                    else { sb.Append('\\'); sb.Append(n); }
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
