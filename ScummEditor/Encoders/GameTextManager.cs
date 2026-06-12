using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    /// <summary>One translatable text with its stable id (e.g. "LF003.SCRP000.t005").</summary>
    public class GameTextEntry
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Text { get; set; }
    }

    public class GameTextImportReport
    {
        public int LinesParsed { get; set; }
        public int EntriesMatched { get; set; }
        public int StringsChanged { get; set; }
        public int BlocksRebuilt { get; set; }
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> GlyphNotes = new List<string>();

        public bool HasChanges { get { return StringsChanged > 0; } }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Text lines read: " + LinesParsed);
            sb.AppendLine("Texts matched in the game: " + EntriesMatched);
            sb.AppendLine("Texts changed: " + StringsChanged);
            sb.AppendLine("Blocks rebuilt: " + BlocksRebuilt);
            if (Errors.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ERRORS (" + Errors.Count + "):");
                for (int i = 0; i < Errors.Count && i < 20; i++) sb.AppendLine("  " + Errors[i]);
                if (Errors.Count > 20) sb.AppendLine("  ... and " + (Errors.Count - 20) + " more");
            }
            if (Warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings (" + Warnings.Count + "):");
                for (int i = 0; i < Warnings.Count && i < 20; i++) sb.AppendLine("  " + Warnings[i]);
                if (Warnings.Count > 20) sb.AppendLine("  ... and " + (Warnings.Count - 20) + " more");
            }
            if (GlyphNotes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Fonts (CHAR) x characters used:");
                foreach (string n in GlyphNotes) sb.AppendLine("  " + n);
            }
            return sb.ToString();
        }
    }

    /*
    Exports every translatable game text to a flat .txt (one string per line, stable ids,
    control codes as {tokens}) and imports it back, rebuilding the affected bytecode.

    Text lives in:
      - SCRP / LSCR / ENCD / EXCD script blocks (talkActor, print*, names, arrays...)
      - OBCD VERB bytecode (the per-verb scripts), plus the OBNA object name

    Importing a string with a different length shifts the rest of its block, so the importer
    rebuilds the block: it splices the new string bytes, remaps every relative-jump operand
    (if / ifNot / jump / wait retry) and the VERB offset table, then re-disassembles the result
    and only accepts it when the instruction stream is provably identical (same string and jump
    layout, fully decoded to the end). Block sizes / LOFF / index directories are recomputed by
    the regular save pipeline (ScummV6GameData.PostProcessChanges).
    */
    public static class GameTextManager
    {
        // ---------------------------------------------------------------------
        // Source enumeration (shared by export and import; ids must be stable)
        // ---------------------------------------------------------------------

        private class Source
        {
            public string Id;
            public ScriptBlock Script;   // script-block source
            public ObjectCode Obcd;      // OBCD source (verb bytecode, or name when IsName)
            public bool IsName;
        }

        private static List<Source> EnumerateSources(ScummV6DataFile dataFile)
        {
            var list = new List<Source>();
            List<DiskBlock> lflfs = dataFile.GetLFLFs();

            for (int i = 0; i < lflfs.Count; i++)
            {
                string lf = "LF" + i.ToString("D3");
                DiskBlock disk = lflfs[i];

                RoomBlock room = disk.GetROOM();
                if (room != null)
                {
                    int encd = 0, excd = 0, obcdOrdinal = 0;
                    var usedLabels = new HashSet<string>(); // a room can repeat an object id (e.g. S&M obj 561)
                    foreach (BlockBase child in room.Childrens)
                    {
                        var script = child as ScriptBlock;
                        if (script != null)
                        {
                            if (script.BlockType == "ENCD")
                                list.Add(new Source { Id = lf + ".ENCD" + (encd++).ToString("D3"), Script = script });
                            else if (script.BlockType == "EXCD")
                                list.Add(new Source { Id = lf + ".EXCD" + (excd++).ToString("D3"), Script = script });
                            else if (script.BlockType == "LSCR")
                                list.Add(new Source { Id = lf + ".LSCR" + Math.Max(script.ScriptId, 0).ToString("D3"), Script = script });
                            continue;
                        }

                        var obcd = child as ObjectCode;
                        if (obcd != null)
                        {
                            string obj = obcd.HasCodeHeader
                                ? "OBJ" + obcd.ObjectId.ToString("D5")
                                : "OBC" + obcdOrdinal.ToString("D3");
                            obcdOrdinal++;
                            string baseObj = obj;
                            for (int dup = 2; !usedLabels.Add(obj); dup++) obj = baseObj + "x" + dup;

                            if (obcd.VerbCodeOffset >= 0 && obcd.VerbCodeLength > 0)
                                list.Add(new Source { Id = lf + "." + obj, Obcd = obcd });
                            if (obcd.ObnaBodyOffset >= 0)
                                list.Add(new Source { Id = lf + "." + obj + ".name", Obcd = obcd, IsName = true });
                        }
                    }
                }

                int scrp = 0;
                foreach (BlockBase child in disk.Childrens)
                {
                    var script = child as ScriptBlock;
                    if (script != null && script.BlockType == "SCRP")
                        list.Add(new Source { Id = lf + ".SCRP" + (scrp++).ToString("D3"), Script = script });
                }
            }
            return list;
        }

        private static byte[] VerbCodeSlice(ObjectCode obcd)
        {
            var slice = new byte[obcd.VerbCodeLength];
            Array.Copy(obcd.RawContent, obcd.VerbCodeOffset, slice, 0, obcd.VerbCodeLength);
            return slice;
        }

        /// <summary>Disassembles with the right engine for the game's SCUMM version.</summary>
        private static Scumm6Disassembler.Result Disassemble(BlockBase context, byte[] code, int start)
        {
            if (context.GameInfo != null && context.GameInfo.ScummVersion == 5)
            {
                return Scumm5Disassembler.Disassemble(code, start);
            }
            return Scumm6Disassembler.Disassemble(code, start);
        }

        // String kinds that are never exported/imported: internal actor names and
        // savegame file names (roomOps saveString/loadString).
        private static bool IsExcludedKind(string kind)
        {
            return kind == "actorName" || kind == "file";
        }

        private static int ObnaNameLength(ObjectCode obcd)
        {
            int n = 0;
            while (n < obcd.ObnaBodyLength && obcd.RawContent[obcd.ObnaBodyOffset + n] != 0) n++;
            return n;
        }

        // ---------------------------------------------------------------------
        // Export
        // ---------------------------------------------------------------------

        public static List<GameTextEntry> Extract(ScummV6DataFile dataFile, GameTextCodec codec)
        {
            var entries = new List<GameTextEntry>();
            foreach (Source source in EnumerateSources(dataFile))
            {
                if (source.IsName)
                {
                    string name = codec.Decode(source.Obcd.RawContent, source.Obcd.ObnaBodyOffset, ObnaNameLength(source.Obcd));
                    if (!HasTranslatableContent(name)) continue; // empty/blank names are noise
                    entries.Add(new GameTextEntry { Id = source.Id, Kind = "objectName", Text = name });
                    continue;
                }

                byte[] buf = source.Script != null ? source.Script.RawContent : VerbCodeSlice(source.Obcd);
                int start = source.Script != null ? source.Script.CodeOffset : 0;
                BlockBase contextBlock = source.Script != null ? (BlockBase)source.Script : source.Obcd;
                Scumm6Disassembler.Result scan = Disassemble(contextBlock, buf, start);

                for (int k = 0; k < scan.Strings.Count; k++)
                {
                    Scumm6Disassembler.StringRef s = scan.Strings[k];
                    if (IsExcludedKind(s.Kind)) continue; // internal names / filenames are not translated

                    string text = codec.Decode(buf, s.Offset, s.Length - (s.Terminated ? 1 : 0));
                    if (!HasTranslatableContent(text)) continue; // empty or escape-tokens-only line
                    entries.Add(new GameTextEntry
                    {
                        Id = source.Id + ".t" + k.ToString("D3"),
                        Kind = s.Kind,
                        Text = text
                    });
                }
            }
            return entries;
        }

        /// <summary>
        /// True when the display text has something a translator can act on: any visible
        /// character outside escape tokens. Whitespace and escape tokens ({br}, {sound:N}...)
        /// are not content; literal braces ({{ / }}) and raw glyph bytes ({0xNN}) are.
        /// Entries without content are skipped on export - the import keeps the original
        /// string for every id missing from the file.
        /// </summary>
        private static bool HasTranslatableContent(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '{')
                {
                    if (i + 1 < text.Length && text[i + 1] == '{') return true; // literal '{'

                    int close = text.IndexOf('}', i + 1);
                    if (close < 0) return true; // malformed - keep it visible to the translator

                    string token = text.Substring(i + 1, close - i - 1);
                    if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return true; // visible glyph
                    i = close + 1;
                    continue;
                }
                if (c == '}')
                {
                    if (i + 1 < text.Length && text[i + 1] == '}') return true; // literal '}'
                    i++;
                    continue;
                }
                if (!char.IsWhiteSpace(c)) return true;
                i++;
            }
            return false;
        }

        public static int ExportToFile(ScummV6DataFile dataFile, string path, GameTextCodec codec, string gameLabel)
        {
            List<GameTextEntry> entries = Extract(dataFile, codec);

            var sb = new StringBuilder();
            sb.AppendLine("; ScummEditor game text export v1");
            sb.AppendLine("; game: " + gameLabel);
            sb.AppendLine("; charmap: " + codec.ToMapSpec());
            sb.AppendLine("; total: " + entries.Count);
            sb.AppendLine(";");
            sb.AppendLine("; Edit only the text after \" = \". Do not change the IDs.");
            sb.AppendLine("; Tokens like {br} {wait} {keep} {int:N} {sound:N} must be kept in the translation.");
            sb.AppendLine("; The import rebuilds the character mapping from the '; charmap:' line above.");
            sb.AppendLine("; To use a free font slot, add a \"char=0xNN\" pair to that charmap, or write {0xNN}");
            sb.AppendLine("; directly in the text - e.g. an 'ss' ligature drawn at slot 0xC1 can be used as \"Cla{0xC1}e\".");
            sb.AppendLine("; Save this file as UTF-8.");

            string lastGroup = null;
            foreach (GameTextEntry entry in entries)
            {
                string group = entry.Id.Substring(0, entry.Id.IndexOf('.'));
                if (group != lastGroup)
                {
                    sb.AppendLine(";");
                    sb.AppendLine("; ===== " + group + " =====");
                    lastGroup = group;
                }
                sb.AppendLine(entry.Id + " = " + entry.Text);
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return entries.Count;
        }

        // ---------------------------------------------------------------------
        // Import
        // ---------------------------------------------------------------------

        public static GameTextImportReport ImportFromFile(ScummV6DataFile dataFile, string path)
        {
            var report = new GameTextImportReport();

            // --- parse the file -------------------------------------------------
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            GameTextCodec codec = null;
            var fileTexts = new Dictionary<string, string>();

            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                if (line.Length == 0) continue;
                if (line[0] == ';')
                {
                    string trimmed = line.Substring(1).TrimStart();
                    if (codec == null && trimmed.StartsWith("charmap:"))
                    {
                        try { codec = GameTextCodec.FromMapSpec(trimmed.Substring(8).Trim()); }
                        catch (FormatException ex) { report.Errors.Add("line " + (n + 1) + ": " + ex.Message); }
                    }
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    report.Errors.Add("line " + (n + 1) + ": invalid format (expected 'ID = text')");
                    continue;
                }
                string id = line.Substring(0, eq).Trim();
                string text = line.Substring(eq + 1);
                if (text.StartsWith(" ")) text = text.Substring(1);

                if (fileTexts.ContainsKey(id))
                {
                    report.Errors.Add("line " + (n + 1) + ": duplicated ID '" + id + "'");
                    continue;
                }
                fileTexts.Add(id, text);
                report.LinesParsed++;
            }

            if (codec == null)
            {
                report.Errors.Add("the file has no valid '; charmap: ...' header line - the import depends on it to rebuild the character mapping");
                return report;
            }

            // --- walk the game, compare and rebuild -----------------------------
            var matchedIds = new HashSet<string>();
            var changedBytes = new List<byte[]>(); // new string contents, for glyph validation

            foreach (Source source in EnumerateSources(dataFile))
            {
                if (source.IsName)
                {
                    string newText;
                    if (!fileTexts.TryGetValue(source.Id, out newText)) continue;
                    matchedIds.Add(source.Id);

                    string error;
                    byte[] newName = codec.Encode(newText, out error);
                    if (newName == null) { report.Errors.Add(source.Id + ": " + error); continue; }

                    ObjectCode obcd = source.Obcd;
                    int nameLen = ObnaNameLength(obcd);
                    if (SliceEquals(obcd.RawContent, obcd.ObnaBodyOffset, nameLen, newName)) continue;

                    RebuildObjectName(obcd, newName);
                    report.StringsChanged++;
                    report.BlocksRebuilt++;
                    changedBytes.Add(newName);
                    continue;
                }

                // script or VERB bytecode source
                byte[] buf = source.Script != null ? source.Script.RawContent : VerbCodeSlice(source.Obcd);
                int start = source.Script != null ? source.Script.CodeOffset : 0;
                BlockBase contextBlock = source.Script != null ? (BlockBase)source.Script : source.Obcd;
                Scumm6Disassembler.Result scan = Disassemble(contextBlock, buf, start);

                var replacements = new Dictionary<int, byte[]>();
                for (int k = 0; k < scan.Strings.Count; k++)
                {
                    string id = source.Id + ".t" + k.ToString("D3");
                    string newText;
                    if (!fileTexts.TryGetValue(id, out newText)) continue;
                    if (IsExcludedKind(scan.Strings[k].Kind)) { matchedIds.Add(id); continue; } // never imported
                    matchedIds.Add(id);

                    string error;
                    byte[] content = codec.Encode(newText, out error);
                    if (content == null) { report.Errors.Add(id + ": " + error); continue; }

                    Scumm6Disassembler.StringRef s = scan.Strings[k];
                    int contentLen = s.Length - (s.Terminated ? 1 : 0);
                    if (SliceEquals(buf, s.Offset, contentLen, content)) continue;

                    replacements.Add(k, content);
                    changedBytes.Add(content);
                }
                if (replacements.Count == 0) continue;

                if (!scan.DecodedToEnd)
                {
                    report.Errors.Add(source.Id + ": the bytecode does not decode to the end; block left unchanged");
                    continue;
                }

                string rebuildError;
                byte[] rebuilt = RebuildCode(contextBlock, buf, start, scan, replacements, out rebuildError);
                if (rebuilt == null)
                {
                    report.Errors.Add(source.Id + ": " + rebuildError + "; block left unchanged");
                    continue;
                }

                if (source.Script != null)
                {
                    source.Script.RawContent = rebuilt;
                }
                else
                {
                    string verbError;
                    if (!ReplaceVerbCode(source.Obcd, rebuilt, scan, replacements, out verbError))
                    {
                        report.Errors.Add(source.Id + ": " + verbError + "; block left unchanged");
                        continue;
                    }
                }

                report.StringsChanged += replacements.Count;
                report.BlocksRebuilt++;
            }

            report.EntriesMatched = matchedIds.Count;
            foreach (KeyValuePair<string, string> kv in fileTexts)
                if (!matchedIds.Contains(kv.Key) && report.Warnings.Count < 50)
                    report.Warnings.Add("ID not found in the game: " + kv.Key);

            ValidateGlyphs(dataFile, changedBytes, codec, report);
            return report;
        }

        private static bool SliceEquals(byte[] buf, int offset, int length, byte[] other)
        {
            if (other.Length != length) return false;
            for (int i = 0; i < length; i++)
                if (buf[offset + i] != other[i]) return false;
            return true;
        }

        // ---------------------------------------------------------------------
        // Bytecode rebuild
        // ---------------------------------------------------------------------

        /// <summary>
        /// Rebuilds a code buffer with some strings replaced, remapping every relative jump.
        /// Returns null (with an error message) if anything cannot be remapped safely; the
        /// result is re-disassembled and verified before being accepted.
        /// </summary>
        private static byte[] RebuildCode(BlockBase context, byte[] buf, int codeStart, Scumm6Disassembler.Result scan,
                                          Dictionary<int, byte[]> replacements, out string error)
        {
            error = null;
            List<Scumm6Disassembler.StringRef> strings = scan.Strings;

            // --- splice ---------------------------------------------------------
            var output = new List<byte>(buf.Length + 64);
            var newStarts = new int[strings.Count];
            var newLengths = new int[strings.Count];
            int prev = 0;
            for (int k = 0; k < strings.Count; k++)
            {
                Scumm6Disassembler.StringRef s = strings[k];
                for (int i = prev; i < s.Offset; i++) output.Add(buf[i]);

                newStarts[k] = output.Count;
                byte[] content;
                if (replacements.TryGetValue(k, out content))
                {
                    output.AddRange(content);
                }
                else
                {
                    int contentLen = s.Length - (s.Terminated ? 1 : 0);
                    for (int i = 0; i < contentLen; i++) output.Add(buf[s.Offset + i]);
                }
                if (s.Terminated) output.Add(0);
                newLengths[k] = output.Count - newStarts[k];
                prev = s.Offset + s.Length;
            }
            for (int i = prev; i < buf.Length; i++) output.Add(buf[i]);
            byte[] result = output.ToArray();

            // --- old -> new position map (valid outside string regions) ----------
            // (local function via delegate to keep the project's C# style simple)
            Func<int, int> map = null;
            string mapError = null;
            map = delegate(int pos)
            {
                int delta = 0;
                for (int k = 0; k < strings.Count; k++)
                {
                    Scumm6Disassembler.StringRef s = strings[k];
                    if (s.Offset + s.Length <= pos) { delta += newLengths[k] - s.Length; continue; }
                    if (pos > s.Offset && mapError == null)
                        mapError = "position 0x" + pos.ToString("X4") + " falls inside a string";
                    break;
                }
                return pos + delta;
            };

            // --- jump fix-up ------------------------------------------------------
            foreach (Scumm6Disassembler.JumpRef jump in scan.Jumps)
            {
                int opNew = map(jump.OperandOffset);
                int targetNew = map(jump.Target);
                if (mapError != null) { error = mapError; return null; }

                int rel = targetNew - (opNew + 2);
                if (rel < short.MinValue || rel > short.MaxValue)
                {
                    error = "a jump exceeds +-32767 bytes after the translation (shorten the texts of this block)";
                    return null;
                }
                result[opNew] = (byte)(rel & 0xFF);
                result[opNew + 1] = (byte)((rel >> 8) & 0xFF);
            }

            // --- verify: the rebuilt code must decode to an identical structure ---
            Scumm6Disassembler.Result rescan = Disassemble(context, result, codeStart);
            if (!rescan.DecodedToEnd ||
                rescan.Strings.Count != strings.Count ||
                rescan.Jumps.Count != scan.Jumps.Count)
            {
                error = "verification failed: the rebuilt code does not decode identically";
                return null;
            }
            for (int k = 0; k < strings.Count; k++)
            {
                if (rescan.Strings[k].Offset != newStarts[k] || rescan.Strings[k].Length != newLengths[k])
                {
                    error = "verification failed: strings misaligned after the rebuild";
                    return null;
                }
            }
            for (int j = 0; j < scan.Jumps.Count; j++)
            {
                if (rescan.Jumps[j].OperandOffset != map(scan.Jumps[j].OperandOffset) ||
                    rescan.Jumps[j].Target != map(scan.Jumps[j].Target))
                {
                    error = "verification failed: jumps misaligned after the rebuild";
                    return null;
                }
            }
            return result;
        }

        /// <summary>Splices a rebuilt VERB bytecode region back into the OBCD, remapping the verb offset table.</summary>
        private static bool ReplaceVerbCode(ObjectCode obcd, byte[] newCode, Scumm6Disassembler.Result scan,
                                            Dictionary<int, byte[]> replacements, out string error)
        {
            error = null;

            // Rebuild the old->new map exactly like RebuildCode did.
            var newLengths = new int[scan.Strings.Count];
            for (int k = 0; k < scan.Strings.Count; k++)
            {
                byte[] content;
                newLengths[k] = replacements.TryGetValue(k, out content)
                    ? content.Length + (scan.Strings[k].Terminated ? 1 : 0)
                    : scan.Strings[k].Length;
            }
            Func<int, int> map = delegate(int pos)
            {
                int delta = 0;
                for (int k = 0; k < scan.Strings.Count; k++)
                {
                    Scumm6Disassembler.StringRef s = scan.Strings[k];
                    if (s.Offset + s.Length <= pos) delta += newLengths[k] - s.Length;
                    else break;
                }
                return pos + delta;
            };

            byte[] raw = obcd.RawContent;
            int headerToCode = obcd.VerbCodeOffset - obcd.VerbBlockOffset; // 8-byte header + table size

            // New offset-table values (offsets are relative to the VERB tag).
            var newOffsets = new int[obcd.VerbEntries.Count];
            for (int e = 0; e < obcd.VerbEntries.Count; e++)
            {
                int sliceRel = obcd.VerbBlockOffset + obcd.VerbEntries[e].Offset - obcd.VerbCodeOffset;
                if (sliceRel < 0 || sliceRel >= obcd.VerbCodeLength)
                {
                    error = "verb " + obcd.VerbEntries[e].Id + " offset outside the code region";
                    return false;
                }
                int newOff = headerToCode + map(sliceRel);
                if (newOff > 0xFFFF)
                {
                    error = "verb " + obcd.VerbEntries[e].Id + " offset exceeds 0xFFFF after the translation";
                    return false;
                }
                newOffsets[e] = newOff;
            }

            int newVerbSize = headerToCode + newCode.Length;
            var rebuilt = new byte[raw.Length - obcd.VerbBlockSize + newVerbSize];

            // [0 .. VERB) unchanged
            Array.Copy(raw, 0, rebuilt, 0, obcd.VerbBlockOffset);
            // VERB header + table (then patch size and table offsets)
            Array.Copy(raw, obcd.VerbBlockOffset, rebuilt, obcd.VerbBlockOffset, headerToCode);
            WriteUInt32BE(rebuilt, obcd.VerbBlockOffset + 4, (uint)newVerbSize);
            for (int e = 0; e < newOffsets.Length; e++)
            {
                int p = obcd.VerbBlockOffset + 8 + e * 3 + 1;
                rebuilt[p] = (byte)(newOffsets[e] & 0xFF);
                rebuilt[p + 1] = (byte)(newOffsets[e] >> 8);
            }
            // new bytecode
            Array.Copy(newCode, 0, rebuilt, obcd.VerbCodeOffset, newCode.Length);
            // everything after the old VERB block (OBNA etc.) unchanged
            int oldTail = obcd.VerbBlockOffset + obcd.VerbBlockSize;
            Array.Copy(raw, oldTail, rebuilt, obcd.VerbCodeOffset + newCode.Length, raw.Length - oldTail);

            obcd.RawContent = rebuilt;
            obcd.Reparse();

            if (obcd.VerbCodeLength != newCode.Length)
            {
                error = "verification failed: the rebuilt VERB block does not re-parse";
                return false;
            }
            return true;
        }

        private static void RebuildObjectName(ObjectCode obcd, byte[] newName)
        {
            byte[] raw = obcd.RawContent;
            int nameLen = ObnaNameLength(obcd);
            bool hadTerminator = nameLen < obcd.ObnaBodyLength; // original had a 0x00
            int tailStart = obcd.ObnaBodyOffset + nameLen + (hadTerminator ? 1 : 0);
            int tailLen = obcd.ObnaBodyOffset + obcd.ObnaBodyLength - tailStart;

            int newBodyLen = newName.Length + (hadTerminator ? 1 : 0) + tailLen;
            var rebuilt = new byte[raw.Length - obcd.ObnaBodyLength + newBodyLen];

            Array.Copy(raw, 0, rebuilt, 0, obcd.ObnaBodyOffset);
            Array.Copy(newName, 0, rebuilt, obcd.ObnaBodyOffset, newName.Length);
            int p = obcd.ObnaBodyOffset + newName.Length;
            if (hadTerminator) rebuilt[p++] = 0;
            Array.Copy(raw, tailStart, rebuilt, p, tailLen);
            int afterBody = obcd.ObnaBodyOffset + obcd.ObnaBodyLength;
            Array.Copy(raw, afterBody, rebuilt, p + tailLen, raw.Length - afterBody);

            WriteUInt32BE(rebuilt, obcd.ObnaBlockOffset + 4, (uint)(8 + newBodyLen));
            obcd.RawContent = rebuilt;
            obcd.Reparse();
        }

        private static void WriteUInt32BE(byte[] buf, int p, uint value)
        {
            buf[p] = (byte)(value >> 24);
            buf[p + 1] = (byte)(value >> 16);
            buf[p + 2] = (byte)(value >> 8);
            buf[p + 3] = (byte)value;
        }

        // ---------------------------------------------------------------------
        // Glyph validation: warn when an imported byte has no glyph in the fonts
        // ---------------------------------------------------------------------

        private static void ValidateGlyphs(ScummV6DataFile dataFile, List<byte[]> changedContents,
                                           GameTextCodec codec, GameTextImportReport report)
        {
            var used = new HashSet<byte>();
            foreach (byte[] content in changedContents)
            {
                int i = 0;
                while (i < content.Length)
                {
                    byte b = content[i];
                    if (b == 0xFF || b == 0xFE)
                    {
                        // skip escape (and its argument) so control bytes are not treated as glyphs
                        if (i + 1 >= content.Length) break;
                        byte code = content[i + 1];
                        i += (code == 1 || code == 2 || code == 3 || code == 8) ? 2 : 4;
                        continue;
                    }
                    if (b >= 0x80) used.Add(b);
                    i++;
                }
            }
            if (used.Count == 0) return;

            var charsets = new List<Charset>();
            CollectCharsets(dataFile, charsets);
            if (charsets.Count == 0) return;

            var sorted = new List<byte>(used);
            sorted.Sort();
            foreach (byte b in sorted)
            {
                int present = 0;
                for (int c = 0; c < charsets.Count; c++)
                    if (b < charsets[c].Glyphs.Count && charsets[c].Glyphs[b].Present) present++;

                string label = "0x" + b.ToString("X2");
                string mapped = codec.Decode(new[] { b }, 0, 1);
                if (mapped.Length == 1) label += " ('" + mapped + "')";

                if (present == 0)
                    report.Warnings.Add("byte " + label + " has no glyph in ANY font of the game - it will render wrong");
                else if (present < charsets.Count)
                    report.GlyphNotes.Add(label + ": glyph present in " + present + " of " + charsets.Count + " fonts");
            }
        }

        private static void CollectCharsets(BlockBase block, List<Charset> result)
        {
            var charset = block as Charset;
            if (charset != null) result.Add(charset);
            foreach (BlockBase child in block.Childrens) CollectCharsets(child, result);
        }
    }
}
