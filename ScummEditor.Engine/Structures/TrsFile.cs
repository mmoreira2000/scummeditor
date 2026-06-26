using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// A SCUMM v7 .TRS text resource: the cutscene-subtitle and UI strings (The Dig DIGTXT.TRS / DIG.TRS,
    /// Full Throttle's per-scene ACCIDENT.TRS / MINEROAD.TRS / ...). A .TRS is either plain text or, when it
    /// starts with the tag "ETRS", a 16-byte header followed by an XOR-0xCC encoded body (verified vs
    /// ScummVM smush_player.cpp getStrings). The decoded body is a series of `#define NAME id` blocks, each
    /// followed by the string text (which may span several lines and contain SMUSH codes like ^f01^c001).
    ///
    /// Each block is one editable entry (Key = the define identifier, Text = the lines that follow it up to
    /// the next #define). The decoded body is the template; only edited entries' text is spliced, then the
    /// body is re-encoded (XOR 0xCC + the original header) if it was ETRS - so an unedited file rebuilds
    /// byte-identically. Text is stored as Latin-1 for an exact round-trip.
    /// </summary>
    public class TrsFile : ILocalizedTextFile
    {
        private const byte XorKey = 0xCC;
        private const int EtrsHeaderLength = 16;
        private static readonly byte[] EtrsTag = { (byte)'E', (byte)'T', (byte)'R', (byte)'S' };

        public string FilePath { get; private set; }
        public byte[] OriginalContent { get; private set; }
        public bool Encoded { get; private set; } // ETRS / XOR 0xCC
        public bool IsValid { get; private set; }

        private byte[] _body;        // decoded body (the template the entries index into)
        private byte[] _header;      // the 16-byte ETRS header (empty when not encoded)
        private readonly List<LocalizedTextEntry> _entries = new List<LocalizedTextEntry>();

        public IReadOnlyList<LocalizedTextEntry> Entries { get { return _entries; } }
        public string FileName { get { return Path.GetFileName(FilePath); } }

        public TrsFile(string filePath)
        {
            FilePath = filePath;
        }

        public void Load(byte[] bytes)
        {
            OriginalContent = bytes;
            _entries.Clear();
            Encoded = false;
            IsValid = false;
            if (bytes == null || bytes.Length == 0) return;

            if (StartsWith(bytes, EtrsTag) && bytes.Length > EtrsHeaderLength)
            {
                Encoded = true;
                _header = new byte[EtrsHeaderLength];
                System.Array.Copy(bytes, 0, _header, 0, EtrsHeaderLength);
                _body = new byte[bytes.Length - EtrsHeaderLength];
                for (int i = 0; i < _body.Length; i++) _body[i] = (byte)(bytes[EtrsHeaderLength + i] ^ XorKey);
            }
            else
            {
                _header = System.Array.Empty<byte>();
                _body = bytes;
            }

            Parse();
            IsValid = _entries.Count > 0;
        }

        private void Parse()
        {
            // Collect the byte offsets of every line that starts with "#define".
            var defineLineStarts = new List<int>();
            var defineLineEnds = new List<int>();   // start of the line AFTER the #define line
            var defineContentEnds = new List<int>();
            int pos = 0;
            while (pos < _body.Length)
            {
                int contentEnd, lineEnd;
                LineBounds(_body, pos, out contentEnd, out lineEnd);
                if (IsDefineLine(_body, pos, contentEnd))
                {
                    defineLineStarts.Add(pos);
                    defineContentEnds.Add(contentEnd);
                    defineLineEnds.Add(lineEnd);
                }
                pos = lineEnd;
            }

            var usedKeys = new HashSet<string>();
            for (int i = 0; i < defineLineStarts.Count; i++)
            {
                int textStart = defineLineEnds[i];
                int textEnd = (i + 1 < defineLineStarts.Count) ? defineLineStarts[i + 1] : _body.Length;

                string key = MakeKey(_body, defineLineStarts[i], defineContentEnds[i]);
                if (!usedKeys.Add(key)) key = key + " #" + i; // disambiguate the rare duplicate

                _entries.Add(new LocalizedTextEntry
                {
                    Key = key,
                    Text = Encoding.Latin1.GetString(_body, textStart, textEnd - textStart),
                    TextStart = textStart,
                    TextEnd = textEnd,
                });
            }
        }

        public byte[] BuildContent()
        {
            byte[] body;
            using (var ms = new MemoryStream())
            {
                int pos = 0;
                foreach (LocalizedTextEntry e in _entries)
                {
                    ms.Write(_body, pos, e.TextStart - pos);          // verbatim incl. the "#define ..." line
                    byte[] text = Encoding.Latin1.GetBytes(e.Text ?? string.Empty);
                    ms.Write(text, 0, text.Length);
                    pos = e.TextEnd;
                }
                ms.Write(_body, pos, _body.Length - pos);
                body = ms.ToArray();
            }

            if (!Encoded) return body;

            var outBytes = new byte[_header.Length + body.Length];
            System.Array.Copy(_header, 0, outBytes, 0, _header.Length);
            for (int i = 0; i < body.Length; i++) outBytes[_header.Length + i] = (byte)(body[i] ^ XorKey);
            return outBytes;
        }

        public string ExportToText()
        {
            var sb = new StringBuilder();
            sb.Append("# ").Append(FileName).Append(" - KEY<TAB>TEXT, one per line; newlines escaped as \\n. Edit TEXT only.\r\n");
            foreach (LocalizedTextEntry e in _entries)
            {
                sb.Append(e.Key).Append('\t').Append(Escape(e.Text)).Append("\r\n");
            }
            return sb.ToString();
        }

        public string ImportFromText(string content)
        {
            var map = new Dictionary<string, string>();
            foreach (string raw in content.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.Length == 0 || raw[0] == '#') continue;
                int tab = raw.IndexOf('\t');
                if (tab <= 0) continue;
                map[raw.Substring(0, tab)] = Unescape(raw.Substring(tab + 1));
            }

            int changed = 0;
            foreach (LocalizedTextEntry e in _entries)
            {
                string text;
                if (map.TryGetValue(e.Key, out text) && text != e.Text)
                {
                    e.Text = text;
                    changed++;
                }
            }
            return string.Format("{0} of {1} strings updated.", changed, _entries.Count);
        }

        // ---- helpers ----

        private static bool IsDefineLine(byte[] b, int start, int contentEnd)
        {
            const string token = "#define";
            if (contentEnd - start < token.Length) return false;
            for (int i = 0; i < token.Length; i++) if (b[start + i] != (byte)token[i]) return false;
            return true;
        }

        /// <summary>Normalised define identifier ("#define  TRS_BTN_YES   80" -> "TRS_BTN_YES 80").</summary>
        private static string MakeKey(byte[] b, int start, int contentEnd)
        {
            string line = Encoding.Latin1.GetString(b, start, contentEnd - start);
            string rest = line.Substring("#define".Length).Trim();
            string[] parts = rest.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", parts);
        }

        private static string Escape(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\r\n", "\\n").Replace("\r", "\\n").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    if (n == 'n') sb.Append("\r\n");
                    else if (n == 't') sb.Append('\t');
                    else if (n == '\\') sb.Append('\\');
                    else { sb.Append('\\'); sb.Append(n); }
                }
                else sb.Append(s[i]);
            }
            return sb.ToString();
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

        private static bool StartsWith(byte[] b, byte[] prefix)
        {
            if (b.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++) if (b[i] != prefix[i]) return false;
            return true;
        }
    }
}
