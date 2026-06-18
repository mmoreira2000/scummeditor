using System;
using System.Collections.Generic;
using System.Linq;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Read-side text extraction for SCUMM v2 games (Maniac Mansion, Zak McKracken). The translatable
    /// strings live in: object names (a byte pointer at OBCD+14), object verb code (a [verbId:1][off:1]
    /// table at OBCD+15), the room exit/entry scripts (EXCD/ENCD), and the global scripts (index SCRIPT
    /// directory). All are v1/v2 bytecode disassembled by ScummV12Disassembler; their inline strings are
    /// decoded with GameTextCodecV12. Mirrors ScummV3OldTextManager, but with the v2 room layout
    /// (ScummV2Room) and the byte-oriented opcode/string format. (Read side first; the write-back is
    /// ScummV2Writer/Import.)
    /// </summary>
    public static class ScummV2TextManager
    {
        public static List<GameTextEntry> Extract(ScummGameData game, GameTextCodecV12 codec)
        {
            var entries = new List<GameTextEntry>();
            var index = game.IndexFile as ScummV3OldBundleIndexFile;

            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(System.IO.Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                byte[] data = df.RawContent;
                string lf = roomNo.ToString("D3");
                var room = new ScummV2Room(data);

                AddObjectsAndVerbCode(entries, data, room, lf, codec);
                AddRoomScripts(entries, data, room, lf, codec);
                AddGlobalScripts(entries, data, index, roomNo, lf, codec);
            }

            return entries;
        }

        // --- object names + object verb code -----------------------------------

        private static void AddObjectsAndVerbCode(List<GameTextEntry> entries, byte[] data, ScummV2Room room, string lf, GameTextCodecV12 codec)
        {
            List<int> boundaries = CollectBoundaries(data, room);
            for (int i = 0; i < room.NumObjects; i++)
            {
                int objptr = room.ObjectCodeOffset(i);
                if (objptr <= 0 || objptr >= data.Length) continue;
                int objId = room.ObjectId(i);
                string oid = lf + ".OBJ" + objId.ToString("D3");

                // Object name: byte pointer @objptr+14 -> NUL-terminated name string.
                int nameRel = room.ObjectNameRelativeOffset(i);
                if (nameRel != 0)
                {
                    int nameOffset = objptr + nameRel;
                    if (nameOffset > 0 && nameOffset < data.Length)
                    {
                        int len = ZeroTerminatedLength(data, nameOffset);
                        string name = codec.Decode(data, nameOffset, len);
                        if (HasContent(name)) entries.Add(new GameTextEntry { Id = oid + ".name", Kind = "objectName", Text = name });
                    }
                }

                // Verb code: [verbId:1][offset:1]* at objptr+15, terminated by verbId==0; each segment is
                // bounded by the next entry's offset (sorted) or the next structural element after objptr.
                int tableStart = objptr + 15;
                var verbOffsets = new List<int>();
                int p = tableStart;
                while (p + 1 < data.Length && data[p] != 0)
                {
                    int rel = data[p + 1];
                    if (rel != 0) verbOffsets.Add(objptr + rel);
                    p += 2;
                }
                if (verbOffsets.Count == 0) continue;
                verbOffsets.Sort();
                int objEnd = NextBoundaryAbove(boundaries, objptr, data.Length);
                for (int v = 0; v < verbOffsets.Count; v++)
                {
                    int start = verbOffsets[v];
                    int end = v + 1 < verbOffsets.Count ? verbOffsets[v + 1] : objEnd;
                    AddBytecodeStrings(entries, data, start, end, oid + ".v" + v.ToString("D2"), codec);
                }
            }
        }

        // --- room exit/entry scripts -------------------------------------------

        private static void AddRoomScripts(List<GameTextEntry> entries, byte[] data, ScummV2Room room, string lf, GameTextCodecV12 codec)
        {
            List<int> boundaries = CollectBoundaries(data, room);
            int excd = room.ExitScriptOffset;
            int encd = room.EntryScriptOffset;
            if (excd > 0 && excd < data.Length)
                AddBytecodeStrings(entries, data, excd, NextBoundaryAbove(boundaries, excd, data.Length), lf + ".EXCD", codec);
            if (encd > 0 && encd < data.Length)
                AddBytecodeStrings(entries, data, encd, NextBoundaryAbove(boundaries, encd, data.Length), lf + ".ENCD", codec);
        }

        // --- global scripts (index SCRIPT directory) ---------------------------

        private static void AddGlobalScripts(List<GameTextEntry> entries, byte[] data, ScummV3OldBundleIndexFile index, int roomNo, string lf, GameTextCodecV12 codec)
        {
            if (index == null || index.ScriptDirectory == null) return;
            V3OldResourceDirectory dir = index.ScriptDirectory;
            for (int s = 0; s < dir.Count; s++)
            {
                if (dir.RoomNumbers[s] != roomNo) continue;
                int off = dir.Offsets[s];
                if (off == 0xFFFF || off == 0 || off + 4 > data.Length) continue;
                int end = ScriptEnd(data, off, NextResourceOffsetInRoom(index, roomNo, off, data.Length));
                AddBytecodeStrings(entries, data, off + 4, end, lf + ".SC" + s.ToString("D3"), codec);
            }
        }

        /// <summary>Extracts all v2 text to a translation file (the shared "id = value" format).</summary>
        public static int ExportToFile(ScummGameData game, string path, string gameLabel)
        {
            List<GameTextEntry> entries = Extract(game, GameTextCodecV12.Default());
            GameTextManager.WriteEntriesFile(entries, path, GameTextCodec.Default(), gameLabel);
            return entries.Count;
        }

        /// <summary>Parses an edited translation file and imports it into the v2 game (byte-safe).</summary>
        public static GameTextImportReport ImportFromFile(ScummGameData game, string path)
        {
            var report = new GameTextImportReport();
            GameTextCodec ignored;
            Dictionary<string, string> fileTexts = GameTextManager.ParseTextFile(path, report, out ignored);
            if (fileTexts == null) return report;

            GameTextImportReport applied = Import(game, fileTexts, GameTextCodecV12.Default());
            applied.LinesParsed = report.LinesParsed;
            return applied;
        }

        // --- import (write-back) ------------------------------------------------

        private struct Edit
        {
            public int Offset;
            public int OldLen;
            public byte[] NewBytes;
            public int SizeWordOffset; // a global script chunk's own [size:u16]; -1 for names / verb code / room scripts
        }

        /// <summary>
        /// Applies edited strings (id -&gt; new text) back into a v2 game, byte-safe: object names splice in
        /// place; script/verb-code bytecode is rebuilt (jump + offset remap) via the shared RebuildCode;
        /// each size change is propagated by ScummV2Writer. Edits within one room file are applied highest
        /// offset first so a splice never invalidates a not-yet-applied lower one.
        /// </summary>
        public static GameTextImportReport Import(ScummGameData game, Dictionary<string, string> idToText, GameTextCodecV12 codec)
        {
            var report = new GameTextImportReport();
            var index = game.IndexFile as ScummV3OldBundleIndexFile;

            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(System.IO.Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                byte[] data = df.RawContent;
                string lf = roomNo.ToString("D3");
                var room = new ScummV2Room(data);

                var edits = new List<Edit>();
                CollectObjectEdits(data, room, lf, idToText, codec, edits, report);
                CollectRoomScriptEdits(df, data, room, lf, idToText, codec, edits, report);
                CollectGlobalScriptEdits(df, data, index, roomNo, lf, idToText, codec, edits, report);

                ApplyEdits(df, index, roomNo, edits, report);
            }

            return report;
        }

        private static void CollectObjectEdits(byte[] data, ScummV2Room room, string lf,
            Dictionary<string, string> idToText, GameTextCodecV12 codec, List<Edit> edits, GameTextImportReport report)
        {
            for (int i = 0; i < room.NumObjects; i++)
            {
                int objptr = room.ObjectCodeOffset(i);
                if (objptr <= 0 || objptr >= data.Length) continue;
                int nameRel = room.ObjectNameRelativeOffset(i);
                if (nameRel == 0) continue;
                int nameOffset = objptr + nameRel;
                if (nameOffset <= 0 || nameOffset >= data.Length) continue;

                string id = lf + ".OBJ" + room.ObjectId(i).ToString("D3") + ".name";
                string newText;
                if (!idToText.TryGetValue(id, out newText)) continue;
                string err;
                byte[] content = codec.Encode(newText, out err);
                if (content == null) { report.Errors.Add(id + ": " + err); continue; }
                int oldLen = ZeroTerminatedLength(data, nameOffset);
                if (SliceEquals(data, nameOffset, oldLen, content)) continue; // unchanged
                edits.Add(new Edit { Offset = nameOffset, OldLen = oldLen, NewBytes = content, SizeWordOffset = -1 });
            }
        }

        private static void CollectRoomScriptEdits(ScummV3OldBundleDataFile df, byte[] data, ScummV2Room room, string lf,
            Dictionary<string, string> idToText, GameTextCodecV12 codec, List<Edit> edits, GameTextImportReport report)
        {
            List<int> boundaries = CollectBoundaries(data, room);

            // object verb code: per-verb segments
            for (int i = 0; i < room.NumObjects; i++)
            {
                int objptr = room.ObjectCodeOffset(i);
                if (objptr <= 0 || objptr >= data.Length) continue;
                string oid = lf + ".OBJ" + room.ObjectId(i).ToString("D3");
                var verbOffsets = new List<int>();
                int p = objptr + 15;
                while (p + 1 < data.Length && data[p] != 0)
                {
                    int rel = data[p + 1];
                    if (rel != 0) verbOffsets.Add(objptr + rel);
                    p += 2;
                }
                if (verbOffsets.Count == 0) continue;
                verbOffsets.Sort();
                int objEnd = NextBoundaryAbove(boundaries, objptr, data.Length);
                for (int v = 0; v < verbOffsets.Count; v++)
                {
                    int start = verbOffsets[v];
                    int end = v + 1 < verbOffsets.Count ? verbOffsets[v + 1] : objEnd;
                    AddBytecodeEdit(df, data, start, end, -1, oid + ".v" + v.ToString("D2"), idToText, codec, edits, report);
                }
            }

            // exit / entry scripts
            int excd = room.ExitScriptOffset, encd = room.EntryScriptOffset;
            if (excd > 0 && excd < data.Length)
                AddBytecodeEdit(df, data, excd, NextBoundaryAbove(boundaries, excd, data.Length), -1, lf + ".EXCD", idToText, codec, edits, report);
            if (encd > 0 && encd < data.Length)
                AddBytecodeEdit(df, data, encd, NextBoundaryAbove(boundaries, encd, data.Length), -1, lf + ".ENCD", idToText, codec, edits, report);
        }

        private static void CollectGlobalScriptEdits(ScummV3OldBundleDataFile df, byte[] data, ScummV3OldBundleIndexFile index,
            int roomNo, string lf, Dictionary<string, string> idToText, GameTextCodecV12 codec, List<Edit> edits, GameTextImportReport report)
        {
            if (index == null || index.ScriptDirectory == null) return;
            V3OldResourceDirectory dir = index.ScriptDirectory;
            for (int s = 0; s < dir.Count; s++)
            {
                if (dir.RoomNumbers[s] != roomNo) continue;
                int off = dir.Offsets[s];
                if (off == 0xFFFF || off == 0 || off + 4 > data.Length) continue;
                int end = ScriptEnd(data, off, NextResourceOffsetInRoom(index, roomNo, off, data.Length));
                AddBytecodeEdit(df, data, off + 4, end, off, lf + ".SC" + s.ToString("D3"), idToText, codec, edits, report);
            }
        }

        /// <summary>Disassembles a bytecode slice, rebuilds it if any of its strings are edited, and records the slice edit.</summary>
        private static void AddBytecodeEdit(ScummV3OldBundleDataFile df, byte[] data, int start, int end, int sizeWordOffset,
            string idPrefix, Dictionary<string, string> idToText, GameTextCodecV12 codec, List<Edit> edits, GameTextImportReport report)
        {
            if (start < 0 || end <= start || end > data.Length) return;
            var slice = new byte[end - start];
            Array.Copy(data, start, slice, 0, slice.Length);
            ScummV6Disassembler.Result scan = ScummV12Disassembler.Disassemble(slice, 0);

            var replacements = new Dictionary<int, byte[]>();
            for (int k = 0; k < scan.Strings.Count; k++)
            {
                ScummV6Disassembler.StringRef sref = scan.Strings[k];
                string id = idPrefix + ".t" + k.ToString("D3");
                string newText;
                if (!idToText.TryGetValue(id, out newText)) continue;
                string err;
                byte[] content = codec.Encode(newText, out err);
                if (content == null) { report.Errors.Add(id + ": " + err); continue; }
                int contentLen = sref.Length - (sref.Terminated ? 1 : 0);
                if (SliceEquals(slice, sref.Offset, contentLen, content)) continue; // unchanged
                replacements[k] = content;
            }
            if (replacements.Count == 0) return;

            if (!scan.DecodedToEnd)
            {
                report.Errors.Add(idPrefix + ": bytecode does not decode to the end; left unchanged");
                return;
            }

            string rebuildError;
            byte[] rebuilt = GameTextManager.RebuildCode(df, slice, 0, scan, replacements, out rebuildError);
            if (rebuilt == null) { report.Errors.Add(idPrefix + ": " + rebuildError + "; left unchanged"); return; }
            edits.Add(new Edit { Offset = start, OldLen = slice.Length, NewBytes = rebuilt, SizeWordOffset = sizeWordOffset });
        }

        private static void ApplyEdits(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo, List<Edit> edits, GameTextImportReport report)
        {
            if (edits.Count == 0) return;
            // Dedupe shared byte regions (apply once), then apply highest-offset first so a splice never
            // moves a not-yet-applied lower edit.
            var byOffset = new Dictionary<int, Edit>();
            foreach (Edit e in edits) if (!byOffset.ContainsKey(e.Offset)) byOffset[e.Offset] = e;
            var unique = new List<Edit>(byOffset.Values);
            unique.Sort((a, b) => b.Offset.CompareTo(a.Offset));
            foreach (Edit e in unique)
            {
                ScummV2Writer.ApplyEdit(df, index, roomNo, e.Offset, e.OldLen, e.NewBytes, e.SizeWordOffset);
                report.BlocksRebuilt++;
                report.StringsChanged++;
            }
        }

        private static bool SliceEquals(byte[] data, int offset, int len, byte[] candidate)
        {
            if (candidate.Length != len) return false;
            for (int i = 0; i < len; i++) if (data[offset + i] != candidate[i]) return false;
            return true;
        }

        // --- shared -------------------------------------------------------------

        /// <summary>Disassembles a bytecode slice [start,end) and adds every translatable inline string.</summary>
        private static void AddBytecodeStrings(List<GameTextEntry> entries, byte[] data, int start, int end, string id, GameTextCodecV12 codec)
        {
            if (start < 0 || end <= start || end > data.Length) return;
            var slice = new byte[end - start];
            Array.Copy(data, start, slice, 0, slice.Length);
            ScummV6Disassembler.Result scan = ScummV12Disassembler.Disassemble(slice, 0);
            for (int k = 0; k < scan.Strings.Count; k++)
            {
                ScummV6Disassembler.StringRef sref = scan.Strings[k];
                string text = codec.Decode(slice, sref.Offset, sref.Length - (sref.Terminated ? 1 : 0));
                if (!HasContent(text)) continue;
                entries.Add(new GameTextEntry { Id = id + ".t" + k.ToString("D3"), Kind = sref.Kind, Text = text });
            }
        }

        /// <summary>Every structural offset inside a v2 room, used to bound the length-less scripts/verb code.</summary>
        private static List<int> CollectBoundaries(byte[] data, ScummV2Room room)
        {
            var b = new List<int>();
            int roomSize = data.Length >= 2 ? (data[0] | (data[1] << 8)) : data.Length;
            b.Add(roomSize > 0 && roomSize <= data.Length ? roomSize : data.Length);
            Add(b, room.ImageOffset);
            Add(b, room.ExitScriptOffset);
            Add(b, room.EntryScriptOffset);
            for (int i = 0; i < room.NumObjects; i++)
            {
                Add(b, room.ObjectImageOffset(i));
                int objptr = room.ObjectCodeOffset(i);
                Add(b, objptr);
                int nameRel = room.ObjectNameRelativeOffset(i);
                if (nameRel != 0) Add(b, objptr + nameRel);
            }
            b.Sort();
            return b;
        }

        private static void Add(List<int> list, int value) { if (value > 0) list.Add(value); }

        private static int NextBoundaryAbove(List<int> boundaries, int offset, int fallback)
        {
            int best = fallback;
            foreach (int x in boundaries) if (x > offset && x < best) best = x;
            return best;
        }

        /// <summary>End of a global script: its [size:u16] word when consistent with the next packed resource, else that resource.</summary>
        private static int ScriptEnd(byte[] data, int off, int hardEnd)
        {
            if (hardEnd <= off || hardEnd > data.Length) hardEnd = data.Length;
            int size = (off + 1 < data.Length) ? (data[off] | (data[off + 1] << 8)) : 0;
            int end = off + size;
            return (size >= 4 && end <= hardEnd) ? end : hardEnd;
        }

        private static int NextResourceOffsetInRoom(ScummV3OldBundleIndexFile index, int roomNo, int off, int fallback)
        {
            if (index == null) return fallback;
            int best = fallback;
            V3OldResourceDirectory[] dirs = { index.RoomDirectory, index.CostumeDirectory, index.ScriptDirectory, index.SoundDirectory };
            foreach (V3OldResourceDirectory dir in dirs)
            {
                if (dir == null || dir.Offsets == null) continue;
                for (int i = 0; i < dir.Count; i++)
                {
                    if (dir.RoomNumbers[i] != roomNo) continue;
                    int o = dir.Offsets[i];
                    if (o > off && o < best) best = o;
                }
            }
            return best;
        }

        private static int ZeroTerminatedLength(byte[] data, int offset)
        {
            int p = offset;
            while (p < data.Length && data[p] != 0) p++;
            return p - offset;
        }

        /// <summary>True when the decoded text has at least one printable, non-space, non-token character.</summary>
        private static bool HasContent(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '{')
                {
                    int close = text.IndexOf('}', i);
                    if (close < 0) break;
                    i = close + 1;
                    continue;
                }
                if (!char.IsWhiteSpace(text[i]) && text[i] >= 0x20) return true;
                i++;
            }
            return false;
        }
    }
}
