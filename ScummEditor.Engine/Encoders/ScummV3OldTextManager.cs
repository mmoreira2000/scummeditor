using System;
using System.Collections.Generic;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /*
    Extracts the translatable text of a SCUMM v3 "old bundle" game (Loom EGA, Indiana Jones 3 EGA).
    These games are NOT block-tree containers (the v4/v5 GameTextManager path needs typed SC/OC
    blocks); each NN.LFL room file is a verbatim byte image whose resources are reached by raw,
    file-relative offsets. So this is a dedicated read-side enumerator over ScummV3OldRoom + the
    old-bundle index, used to PROVE the offset model before any write-back is built.

    Carriers (all confirmed against scummvm engines/scumm/object.cpp + script.cpp):
      - Object names: name pointer byte at OBCD+16 (objptr = room + storedOBCDoffset); the name is the
        null-terminated string at objptr + thatByte.
      - Object verb code: the verb table at objptr+17 is [verbId:1][offset:u16 LE]* terminated by
        verbId==0; offsets are objptr-relative. The verb code spans from just after the table to the
        end of the OBCD chunk (chunk size word at storedOBCDoffset-2), minus the name if it sits there.
      - Global scripts: the index SCRIPT directory's (roomNumber, offset) entries; the script chunk
        is [size:u16][2 bytes][bytecode], so bytecode starts at offset+4 (resourceHeaderSize = 4).
      - Local scripts: the room-header local-script table [id:1][offset:u16 LE]* (id 0 terminates).

    Strings are decoded with the same GameTextCodec the v4/v5 export uses; the disassembler is
    ScummV3Disassembler with IsOldBundle = true (so 0xFE is treated as a string escape).
    */
    public static class ScummV3OldTextManager
    {
        /// <summary>Exports all translatable text of a v3 old-bundle game to a flat .txt (GUI entry point).</summary>
        public static int ExportToFile(ScummGameData game, string path, GameTextCodec codec, string gameLabel)
        {
            List<GameTextEntry> entries = Extract(game, codec);
            GameTextManager.WriteEntriesFile(entries, path, codec, gameLabel);
            return entries.Count;
        }

        /// <summary>Imports an edited .txt back into a v3 old-bundle game (GUI entry point).</summary>
        public static GameTextImportReport ImportFromFile(ScummGameData game, string path)
        {
            var report = new GameTextImportReport();
            GameTextCodec codec;
            System.Collections.Generic.Dictionary<string, string> fileTexts = GameTextManager.ParseTextFile(path, report, out codec);
            if (fileTexts == null) return report;

            GameTextImportReport applied = Import(game, fileTexts, codec);
            applied.LinesParsed = report.LinesParsed;
            return applied;
        }

        public static List<GameTextEntry> Extract(ScummGameData game, GameTextCodec codec)
        {
            codec.FeEscape = true; // v3 old-bundle: 0xFE is a string escape (matches ScummV3Disassembler IsOldBundle)
            var entries = new List<GameTextEntry>();
            bool isIndy3 = game.LoadedGameInfo != null && game.LoadedGameInfo.LoadedGame == ScummGame.IndianaJones3;
            var index = game.IndexFile as ScummV3OldBundleIndexFile;

            foreach (DataDisk disk in game.DataDisks)
            {
                var dataFile = disk.Tree as ScummV3OldBundleDataFile;
                if (dataFile == null || dataFile.RawContent == null) continue;

                byte[] data = dataFile.RawContent;
                var room = new ScummV3OldRoom(data);
                int roomNo = RoomNumberFromPath(disk.FilePath);
                string lf = "LF" + roomNo.ToString("D3");
                List<V3OldChunk> chunks = dataFile.Chunks;

                AddObjects(entries, data, room, chunks, lf, isIndy3, codec);
                AddLocalScripts(entries, data, room, chunks, lf, isIndy3, codec);
                AddGlobalScripts(entries, data, index, roomNo, lf, isIndy3, codec);
            }
            return entries;
        }

        // --- import (write-back) ---------------------------------------------

        /// <summary>
        /// Writes edited OBJECT NAMES back into a v3 old-bundle game, re-pointing every shifted offset
        /// via ScummV3OldWriter. Only ids present in <paramref name="idToText"/> are touched; an
        /// unchanged name is skipped. (Script / verb-code bytecode import is layered on next.)
        /// </summary>
        public static GameTextImportReport ImportNames(ScummGameData game, System.Collections.Generic.Dictionary<string, string> idToText, GameTextCodec codec)
        {
            codec.FeEscape = true; // v3 old-bundle: 0xFE is a string escape
            var report = new GameTextImportReport();
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            var matched = new HashSet<string>();

            foreach (DataDisk disk in game.DataDisks)
            {
                var dataFile = disk.Tree as ScummV3OldBundleDataFile;
                if (dataFile == null || dataFile.RawContent == null) continue;
                int roomNo = RoomNumberFromPath(disk.FilePath);
                string lf = "LF" + roomNo.ToString("D3");

                // Collect every name edit for this file FROM THE ORIGINAL bytes, then apply them in
                // descending offset order so each splice never invalidates a not-yet-applied (lower) one.
                byte[] data = dataFile.RawContent;
                var room = new ScummV3OldRoom(data);
                List<V3OldChunk> chunks = dataFile.Chunks;
                var edits = new List<NameEdit>();
                var usedLabels = new HashSet<string>();

                for (int i = 0; i < room.NumObjects; i++)
                {
                    int objptr = room.ObjectCodeOffset(i);
                    if (objptr <= 2 || objptr >= data.Length) continue;

                    int objId = ReadU16(data, objptr + 4);
                    string obj = "OBJ" + objId.ToString("D5");
                    string baseObj = obj;
                    for (int dup = 2; !usedLabels.Add(obj); dup++) obj = baseObj + "x" + dup;

                    int nameRel = objptr + 16 < data.Length ? data[objptr + 16] : 0;
                    if (nameRel == 0) continue;
                    int nameOffset = objptr + nameRel;
                    int chunkEnd = ChunkEndContaining(chunks, objptr, data.Length);
                    if (nameOffset <= 0 || nameOffset >= chunkEnd) continue;

                    string id = lf + "." + obj + ".name";
                    string newText;
                    if (!idToText.TryGetValue(id, out newText)) continue;
                    matched.Add(id);

                    string encodeError;
                    byte[] newName = codec.Encode(newText, out encodeError);
                    if (newName == null) { report.Errors.Add(id + ": " + encodeError); continue; }

                    int oldLen = ZeroTerminatedLength(data, nameOffset, chunkEnd);
                    if (SliceEquals(data, nameOffset, oldLen, newName)) continue; // unchanged
                    edits.Add(new NameEdit { Id = id, Offset = nameOffset, OldLen = oldLen, NewBytes = newName });
                }

                // Several objects can share ONE name string (e.g. four "door" objects point at the same
                // bytes). Apply each byte region only once - re-editing a shared region with a stale
                // offset would corrupt the file. Conflicting values for a shared region are reported.
                var appliedAt = new Dictionary<int, NameEdit>();
                var unique = new List<NameEdit>();
                foreach (NameEdit e in edits)
                {
                    NameEdit prior;
                    if (appliedAt.TryGetValue(e.Offset, out prior))
                    {
                        if (!SliceEquals(prior.NewBytes, 0, prior.NewBytes.Length, e.NewBytes))
                            report.Warnings.Add(e.Id + ": shares its name with " + prior.Id + " (same bytes); kept '" + prior.Id + "'");
                        continue;
                    }
                    appliedAt[e.Offset] = e;
                    unique.Add(e);
                }

                unique.Sort((a, b) => b.Offset.CompareTo(a.Offset)); // descending, so a splice never moves a lower not-yet-applied edit
                foreach (NameEdit e in unique)
                {
                    ScummV3OldWriter.ApplyEdit(dataFile, index, roomNo, e.Offset, e.OldLen, e.NewBytes);
                    report.StringsChanged++;
                    report.BlocksRebuilt++;
                }
            }

            report.EntriesMatched = matched.Count;
            foreach (var kv in idToText)
                if (!matched.Contains(kv.Key) && report.Warnings.Count < 50)
                    report.Warnings.Add("ID not found (or not an object name) in the game: " + kv.Key);
            return report;
        }

        private class NameEdit
        {
            public string Id;
            public int Offset;
            public int OldLen;
            public byte[] NewBytes;
            public int SizeWordOffset = -1; // a script chunk's own [size:u16] word; -1 for names / verb code
        }

        /// <summary>
        /// Writes edited text (object names AND script / verb-code dialogue) back into a v3 old-bundle
        /// game. Bytecode carriers are rebuilt with the shared jump-remapping rebuilder; every shifted
        /// offset is re-pointed by ScummV3OldWriter. Edits within one room file are applied in
        /// descending offset order so a splice never invalidates a not-yet-applied lower edit.
        /// </summary>
        public static GameTextImportReport Import(ScummGameData game, System.Collections.Generic.Dictionary<string, string> idToText, GameTextCodec codec)
        {
            codec.FeEscape = true; // v3 old-bundle: 0xFE is a string escape
            var report = new GameTextImportReport();
            bool isIndy3 = game.LoadedGameInfo != null && game.LoadedGameInfo.LoadedGame == ScummGame.IndianaJones3;
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            var matched = new HashSet<string>();

            foreach (DataDisk disk in game.DataDisks)
            {
                var dataFile = disk.Tree as ScummV3OldBundleDataFile;
                if (dataFile == null || dataFile.RawContent == null) continue;
                int roomNo = RoomNumberFromPath(disk.FilePath);
                string lf = "LF" + roomNo.ToString("D3");

                // Collect every slice edit for this file FROM THE ORIGINAL bytes.
                var edits = new List<NameEdit>();
                CollectObjectEdits(dataFile, idToText, codec, lf, isIndy3, edits, matched, report);
                CollectScriptEdits(dataFile, index, idToText, codec, lf, roomNo, isIndy3, edits, matched, report);

                // Dedupe shared byte regions (apply once), then apply highest-offset first.
                var appliedAt = new Dictionary<int, NameEdit>();
                var unique = new List<NameEdit>();
                foreach (NameEdit e in edits)
                {
                    if (appliedAt.ContainsKey(e.Offset)) continue;
                    appliedAt[e.Offset] = e;
                    unique.Add(e);
                }
                unique.Sort((a, b) => b.Offset.CompareTo(a.Offset));
                foreach (NameEdit e in unique)
                {
                    ScummV3OldWriter.ApplyEdit(dataFile, index, roomNo, e.Offset, e.OldLen, e.NewBytes, e.SizeWordOffset);
                    report.StringsChanged++;
                    report.BlocksRebuilt++;
                }
            }

            report.EntriesMatched = matched.Count;
            foreach (var kv in idToText)
                if (!matched.Contains(kv.Key) && report.Warnings.Count < 50)
                    report.Warnings.Add("ID not found in the game: " + kv.Key);
            return report;
        }

        /// <summary>Collects object-name and object-verb-code slice edits (mirrors AddObjects' id scheme).</summary>
        private static void CollectObjectEdits(ScummV3OldBundleDataFile dataFile, System.Collections.Generic.Dictionary<string, string> idToText,
            GameTextCodec codec, string lf, bool isIndy3, List<NameEdit> edits, HashSet<string> matched, GameTextImportReport report)
        {
            byte[] data = dataFile.RawContent;
            var room = new ScummV3OldRoom(data);
            List<V3OldChunk> chunks = dataFile.Chunks;
            List<int> boundaries = CollectStructuralBoundaries(data, room);
            var usedLabels = new HashSet<string>();

            for (int i = 0; i < room.NumObjects; i++)
            {
                int objptr = room.ObjectCodeOffset(i);
                if (objptr <= 2 || objptr >= data.Length) continue;
                int chunkEnd = ChunkEndContaining(chunks, objptr, data.Length);

                int objId = ReadU16(data, objptr + 4);
                string obj = "OBJ" + objId.ToString("D5");
                string baseObj = obj;
                for (int dup = 2; !usedLabels.Add(obj); dup++) obj = baseObj + "x" + dup;

                // name
                int nameRel = objptr + 16 < data.Length ? data[objptr + 16] : 0;
                int nameOffset = nameRel != 0 ? objptr + nameRel : 0;
                if (nameOffset > 0 && nameOffset < chunkEnd)
                {
                    string id = lf + "." + obj + ".name";
                    string newText;
                    if (idToText.TryGetValue(id, out newText))
                    {
                        matched.Add(id);
                        string err;
                        byte[] newName = codec.Encode(newText, out err);
                        int oldLen = ZeroTerminatedLength(data, nameOffset, chunkEnd);
                        if (newName == null) report.Errors.Add(id + ": " + err);
                        else if (!SliceEquals(data, nameOffset, oldLen, newName))
                            edits.Add(new NameEdit { Id = id, Offset = nameOffset, OldLen = oldLen, NewBytes = newName });
                    }
                }

                // verb code segments
                int verbTable = objptr + 17;
                var codeStarts = new List<int>();
                int p = verbTable;
                while (p + 2 < chunkEnd && data[p] != 0)
                {
                    int abs = objptr + ReadU16(data, p + 1);
                    if (abs > verbTable && abs < chunkEnd) codeStarts.Add(abs);
                    p += 3;
                }
                codeStarts.Sort();
                for (int v = 0; v < codeStarts.Count; v++)
                {
                    int segStart = codeStarts[v];
                    int segEnd = NextBoundaryAbove(boundaries, segStart, chunkEnd);
                    if (v + 1 < codeStarts.Count && codeStarts[v + 1] < segEnd) segEnd = codeStarts[v + 1];
                    // Verb code has no own size word (-1): it lives in the room resource, sized by @0.
                    AddBytecodeEdit(dataFile, data, segStart, segEnd, -1, lf + "." + obj + ".v" + v.ToString("D2"), isIndy3, idToText, codec, edits, matched, report);
                }
            }
        }

        /// <summary>Collects global-script and local-script slice edits (mirrors AddGlobalScripts/AddLocalScripts).</summary>
        private static void CollectScriptEdits(ScummV3OldBundleDataFile dataFile, ScummV3OldBundleIndexFile index,
            System.Collections.Generic.Dictionary<string, string> idToText, GameTextCodec codec, string lf, int roomNo,
            bool isIndy3, List<NameEdit> edits, HashSet<string> matched, GameTextImportReport report)
        {
            byte[] data = dataFile.RawContent;
            var room = new ScummV3OldRoom(data);
            List<int> boundaries = CollectStructuralBoundaries(data, room);

            // Global scripts are index-loaded resources: [size:u16][2][bytecode], so the size word at
            // their offset IS the length, and editing one grows that word - BUT some old-bundle size
            // words are unreliable (e.g. Loom EGA SC055's word is 34559), so the slice is also clamped
            // to the next packed resource (NextResourceOffsetInRoom) to avoid over-reading into it.
            if (index != null && index.ScriptDirectory != null)
            {
                V3OldResourceDirectory dir = index.ScriptDirectory;
                for (int s = 0; s < dir.Count; s++)
                {
                    if (dir.RoomNumbers[s] != roomNo) continue;
                    int off = dir.Offsets[s];
                    if (off == 0xFFFF || off == 0 || off + 4 > data.Length) continue;
                    int end = ScriptEnd(data, off, NextResourceOffsetInRoom(index, roomNo, off, data.Length));
                    AddBytecodeEdit(dataFile, data, off + 4, end, off, lf + ".SC" + s.ToString("D3"), isIndy3, idToText, codec, edits, matched, report);
                }
            }

            // Local scripts: the table offset points DIRECTLY at the bytecode (scummvm runScript uses
            // _localScriptOffsets[...] with no header add); the 4-byte resource header sits before it.
            // So the bytecode is [off, nextElement) - start at off, not off+4 (mirrors AddLocalScripts).
            int p = 29 + room.NumObjects * 4 + room.NumSounds + room.NumScripts;
            while (p + 3 <= data.Length && data[p] != 0)
            {
                int id = data[p];
                int off = ReadU16(data, p + 1);
                p += 3;
                if (off <= 0 || off >= data.Length) continue;
                int end = NextBoundaryAbove(boundaries, off, data.Length);
                AddBytecodeEdit(dataFile, data, off, end, -1, lf + ".LS" + id.ToString("D3"), isIndy3, idToText, codec, edits, matched, report);
            }
        }

        /// <summary>
        /// End of a global script resource. Its own [size:u16] word at <paramref name="off"/> is normally
        /// its length, but a few old-bundle size words are garbage (Loom EGA SC055 reads 34559), which
        /// used to make the slice over-read to end-of-file and desync. So the size word is trusted only
        /// when it is consistent with <paramref name="hardEnd"/> (the next packed resource); otherwise the
        /// resource is bounded by hardEnd.
        /// </summary>
        private static int ScriptEnd(byte[] data, int off, int hardEnd)
        {
            if (hardEnd <= off || hardEnd > data.Length) hardEnd = data.Length;
            int size = ReadU16(data, off);
            int end = off + size;
            return (size >= 4 && end <= hardEnd) ? end : hardEnd;
        }

        /// <summary>
        /// The lowest resource offset in this room file strictly above <paramref name="off"/>, scanning the
        /// room/costume/script/sound index directories for this room. v3 old-bundle resources are packed
        /// back-to-back, so this is the hard upper bound of a resource whose own size word is unreliable.
        /// </summary>
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

        /// <summary>
        /// Disassembles a bytecode slice [start,end), and if any of its strings are edited, rebuilds the
        /// slice (jump-remapped + verified by the shared rebuilder) and records the resulting slice edit.
        /// </summary>
        private static void AddBytecodeEdit(ScummV3OldBundleDataFile dataFile, byte[] data, int start, int end,
            int sizeWordOffset, string idPrefix, bool isIndy3, System.Collections.Generic.Dictionary<string, string> idToText, GameTextCodec codec,
            List<NameEdit> edits, HashSet<string> matched, GameTextImportReport report)
        {
            if (start < 0 || end <= start || end > data.Length) return;

            var slice = new byte[end - start];
            System.Array.Copy(data, start, slice, 0, slice.Length);
            ScummV6Disassembler.Result scan = ScummV3Disassembler.Disassemble(slice, 0, null, isIndy3, true);

            var replacements = new Dictionary<int, byte[]>();
            for (int k = 0; k < scan.Strings.Count; k++)
            {
                ScummV6Disassembler.StringRef sref = scan.Strings[k];
                if (sref.Kind == "actorName" || sref.Kind == "file") continue;
                string id = idPrefix + ".t" + k.ToString("D3");
                string newText;
                if (!idToText.TryGetValue(id, out newText)) continue;
                matched.Add(id);

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
                report.Errors.Add(idPrefix + ": bytecode does not decode to the end; left unchanged"
                    + GameTextManager.DecodeFailureDetail(scan, slice, 0));
                return;
            }

            string rebuildError;
            byte[] rebuilt = GameTextManager.RebuildCode(dataFile, slice, 0, scan, replacements, out rebuildError);
            if (rebuilt == null) { report.Errors.Add(idPrefix + ": " + rebuildError + "; left unchanged"); return; }

            edits.Add(new NameEdit { Id = idPrefix, Offset = start, OldLen = slice.Length, NewBytes = rebuilt, SizeWordOffset = sizeWordOffset });
        }

        private static bool SliceEquals(byte[] buf, int offset, int length, byte[] other)
        {
            if (other.Length != length) return false;
            for (int i = 0; i < length; i++)
                if (buf[offset + i] != other[i]) return false;
            return true;
        }

        // --- objects: names + verb code --------------------------------------

        private static void AddObjects(List<GameTextEntry> entries, byte[] data, ScummV3OldRoom room,
            List<V3OldChunk> chunks, string lf, bool isIndy3, GameTextCodec codec)
        {
            // Verb code has no stored length; a block ends where the next structural element begins.
            // Collect every structural offset in the room (the room-header pointers, every object's
            // image/code/name, the local scripts, and the room-resource end) so each verb block can be
            // bounded by the nearest one above it - otherwise the last verb over-reads into its
            // neighbours and yields other objects' dialogue under the wrong id.
            List<int> boundaries = CollectStructuralBoundaries(data, room);

            var usedLabels = new HashSet<string>();
            for (int i = 0; i < room.NumObjects; i++)
            {
                int objptr = room.ObjectCodeOffset(i); // = room + storedOBCDoffset
                if (objptr <= 2 || objptr >= data.Length) continue;

                // The OBCD is a chunk in the file's chunk chain; its end bounds the verb code + name
                // reliably (the stored-2 size word is not a dependable length).
                int chunkEnd = ChunkEndContaining(chunks, objptr, data.Length);

                int objId = ReadU16(data, objptr + 4);
                string obj = "OBJ" + objId.ToString("D5");
                string baseObj = obj;
                for (int dup = 2; !usedLabels.Add(obj); dup++) obj = baseObj + "x" + dup;

                // Object name: byte at objptr+16 is the name's objptr-relative offset (0 = no name).
                int nameRel = objptr + 16 < data.Length ? data[objptr + 16] : 0;
                int nameOffset = nameRel != 0 ? objptr + nameRel : 0;
                if (nameOffset > 0 && nameOffset < chunkEnd)
                {
                    int nameLen = ZeroTerminatedLength(data, nameOffset, chunkEnd);
                    string name = codec.Decode(data, nameOffset, nameLen);
                    if (HasContent(name))
                        entries.Add(new GameTextEntry { Id = lf + "." + obj + ".name", Kind = "objectName", Text = name });
                }

                // Verb code: the table at objptr+17 is [verbId:1][offset:u16 LE]* terminated by
                // verbId==0; each offset is objptr-relative and points at that verb's bytecode. The
                // verb code blocks sit (in offset order) right after the table; the object name sits
                // after the last one. Disassembling each verb block as its own [thisOffset, nextOffset)
                // slice bounds it precisely - disassembling the whole region over-reads past a verb's
                // stopObjectCode into the next block / the name and yields garbage strings.
                int verbTable = objptr + 17;
                var codeStarts = new List<int>();
                int p = verbTable;
                while (p + 2 < chunkEnd && data[p] != 0)
                {
                    int rel = ReadU16(data, p + 1);
                    int abs = objptr + rel;
                    if (abs > verbTable && abs < chunkEnd) codeStarts.Add(abs);
                    p += 3;
                }

                if (codeStarts.Count > 0)
                {
                    codeStarts.Sort();
                    for (int v = 0; v < codeStarts.Count; v++)
                    {
                        int segStart = codeStarts[v];
                        // End at the nearest of: the next verb of this object, or the next structural
                        // element in the room (object/name/image/script/room-end). This bounds the last
                        // verb tightly instead of running to the room-resource end.
                        int segEnd = NextBoundaryAbove(boundaries, segStart, chunkEnd);
                        if (v + 1 < codeStarts.Count && codeStarts[v + 1] < segEnd) segEnd = codeStarts[v + 1];
                        AddBytecodeStrings(entries, data, segStart, segEnd, lf + "." + obj + ".v" + v.ToString("D2"), isIndy3, codec);
                    }
                }
            }
        }

        // --- scripts ----------------------------------------------------------

        private static void AddLocalScripts(List<GameTextEntry> entries, byte[] data, ScummV3OldRoom room,
            List<V3OldChunk> chunks, string lf, bool isIndy3, GameTextCodec codec)
        {
            // Local scripts: the room-header table stores [id:1][offset:u16]*, and that offset points
            // DIRECTLY at the bytecode the engine runs (scummvm script.cpp runScript: local scripts use
            // _localScriptOffsets[...] verbatim, with NO resource-header add - unlike global scripts,
            // which add _resourceHeaderSize). The 4-byte resource header sits just BEFORE the offset, so
            // the bytecode is [offset, nextElement) and we must start the disassembly at offset, not
            // offset+4 (the +4 desynced any string-bearing local script - e.g. Loom EGA LS200/LS216).
            List<int> boundaries = CollectStructuralBoundaries(data, room);
            int p = 29 + room.NumObjects * 4 + room.NumSounds + room.NumScripts;
            while (p + 3 <= data.Length)
            {
                int id = data[p];
                if (id == 0) break; // terminator
                int offset = ReadU16(data, p + 1);
                p += 3;
                if (offset <= 0 || offset >= data.Length) continue;
                int end = NextBoundaryAbove(boundaries, offset, data.Length);
                AddBytecodeStrings(entries, data, offset, end, lf + ".LS" + id.ToString("D3"), isIndy3, codec);
            }
        }

        private static void AddGlobalScripts(List<GameTextEntry> entries, byte[] data, ScummV3OldBundleIndexFile index,
            int roomNo, string lf, bool isIndy3, GameTextCodec codec)
        {
            if (index == null || index.ScriptDirectory == null) return;
            V3OldResourceDirectory dir = index.ScriptDirectory;
            for (int s = 0; s < dir.Count; s++)
            {
                if (dir.RoomNumbers[s] != roomNo) continue;
                int offset = dir.Offsets[s];
                if (offset == 0xFFFF || offset == 0) continue; // absent
                int hardEnd = NextResourceOffsetInRoom(index, roomNo, offset, data.Length);
                AddScriptChunk(entries, data, offset, hardEnd, lf + ".SC" + s.ToString("D3"), isIndy3, codec);
            }
        }

        /// <summary>Disassembles a global script chunk ([size:u16][2 bytes][bytecode]) and adds its strings.</summary>
        private static void AddScriptChunk(List<GameTextEntry> entries, byte[] data,
            int chunkStart, int hardEnd, string id, bool isIndy3, GameTextCodec codec)
        {
            if (chunkStart < 0 || chunkStart + 4 > data.Length) return;
            // A script is a [size:u16][2][bytecode] resource, so its own size word is its length - unless
            // that word is garbage (some old-bundle scripts), in which case ScriptEnd clamps to hardEnd
            // (the next packed resource) instead of over-reading into the following script.
            int chunkEnd = ScriptEnd(data, chunkStart, hardEnd);
            AddBytecodeStrings(entries, data, chunkStart + 4, chunkEnd, id, isIndy3, codec);
        }

        /// <summary>
        /// Every structural offset inside the room resource: the room-header sub-block pointers, each
        /// object's image/code/name, the local-script entries, and the room-resource end (@0). Used to
        /// bound a verb-code block at the nearest element after it (verb code carries no length).
        /// </summary>
        private static List<int> CollectStructuralBoundaries(byte[] data, ScummV3OldRoom room)
        {
            var b = new List<int>();
            int roomSize = ReadU16(data, 0);
            b.Add(roomSize > 0 ? roomSize : data.Length);
            AddBoundary(b, room.ImageOffset);
            AddBoundary(b, room.BoxOffset);
            AddBoundary(b, room.ExitScriptOffset);
            AddBoundary(b, room.EntryScriptOffset);

            for (int i = 0; i < room.NumObjects; i++)
            {
                AddBoundary(b, room.ObjectImageOffset(i));
                int objptr = room.ObjectCodeOffset(i);
                AddBoundary(b, objptr);
                int nameRel = objptr > 0 && objptr + 16 < data.Length ? data[objptr + 16] : 0;
                if (nameRel != 0) AddBoundary(b, objptr + nameRel);
            }

            int p = 29 + room.NumObjects * 4 + room.NumSounds + room.NumScripts;
            while (p + 3 <= data.Length && data[p] != 0)
            {
                AddBoundary(b, ReadU16(data, p + 1));
                p += 3;
            }

            b.Sort();
            return b;
        }

        private static void AddBoundary(List<int> list, int value)
        {
            if (value > 0) list.Add(value);
        }

        /// <summary>The smallest boundary strictly greater than <paramref name="offset"/>, or <paramref name="fallback"/>.</summary>
        private static int NextBoundaryAbove(List<int> boundaries, int offset, int fallback)
        {
            int best = fallback;
            foreach (int b in boundaries)
                if (b > offset && b < best) best = b;
            return best;
        }

        /// <summary>End offset of the chunk-chain chunk that contains <paramref name="offset"/>, or the fallback.</summary>
        private static int ChunkEndContaining(List<V3OldChunk> chunks, int offset, int fallback)
        {
            if (chunks != null)
            {
                foreach (V3OldChunk chunk in chunks)
                {
                    if (offset >= chunk.Offset && offset < chunk.Offset + chunk.Size)
                        return chunk.Offset + chunk.Size;
                }
            }
            return fallback;
        }

        /// <summary>Slices [start,end) out of the room, disassembles it, and adds every translatable string.</summary>
        private static void AddBytecodeStrings(List<GameTextEntry> entries, byte[] data, int start, int end,
            string id, bool isIndy3, GameTextCodec codec)
        {
            if (start < 0 || end <= start || end > data.Length) return;

            var slice = new byte[end - start];
            Array.Copy(data, start, slice, 0, slice.Length);
            ScummV6Disassembler.Result scan = ScummV3Disassembler.Disassemble(slice, 0, null, isIndy3, true);

            for (int k = 0; k < scan.Strings.Count; k++)
            {
                ScummV6Disassembler.StringRef sref = scan.Strings[k];
                if (sref.Kind == "actorName" || sref.Kind == "file") continue; // never translated
                string text = codec.Decode(slice, sref.Offset, sref.Length - (sref.Terminated ? 1 : 0));
                if (!HasContent(text)) continue;
                entries.Add(new GameTextEntry { Id = id + ".t" + k.ToString("D3"), Kind = sref.Kind, Text = text });
            }
        }

        // --- helpers ----------------------------------------------------------

        private static int ZeroTerminatedLength(byte[] data, int offset, int limit)
        {
            int n = 0;
            while (offset + n < limit && data[offset + n] != 0) n++;
            return n;
        }

        /// <summary>
        /// True when the decoded text is genuine translatable content. Rejects empty/whitespace-only
        /// strings and binary noise: a string that carries a raw low-control glyph ({0x00}..{0x1F}) is
        /// not real text (legitimate control codes arrive as escape tokens like {br}/{wait}, never as a
        /// raw byte), so it is a stray/over-read fragment at a code-segment edge and is skipped.
        /// </summary>
        private static bool HasContent(string text)
        {
            bool anyVisible = false;
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close < 0) { anyVisible = true; i++; continue; }
                    string token = text.Substring(i + 1, close - i - 1);
                    if (token.StartsWith("0x") && token.Length == 4)
                    {
                        int v;
                        if (int.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out v) && v < 0x20)
                            return false; // raw control byte -> binary noise, not text
                        anyVisible = true; // a real high glyph (accent etc.)
                    }
                    i = close + 1;
                    continue;
                }
                if (!char.IsWhiteSpace(text[i]) && text[i] != '}') anyVisible = true;
                i++;
            }
            return anyVisible;
        }

        private static int ReadU16(byte[] data, int p)
        {
            if (p < 0 || p + 1 >= data.Length) return 0;
            return data[p] | (data[p + 1] << 8);
        }

        private static int RoomNumberFromPath(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            int n;
            return int.TryParse(name, out n) ? n : 0;
        }
    }
}
