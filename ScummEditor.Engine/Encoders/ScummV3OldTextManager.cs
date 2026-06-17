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
        public static List<GameTextEntry> Extract(ScummGameData game, GameTextCodec codec)
        {
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
                AddGlobalScripts(entries, data, index, chunks, roomNo, lf, isIndy3, codec);
            }
            return entries;
        }

        // --- objects: names + verb code --------------------------------------

        private static void AddObjects(List<GameTextEntry> entries, byte[] data, ScummV3OldRoom room,
            List<V3OldChunk> chunks, string lf, bool isIndy3, GameTextCodec codec)
        {
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
                    // The block after the last verb is the object name (when it follows the code) or
                    // the OBCD chunk end - whichever comes first bounds the final verb.
                    int finalBound = (nameOffset > codeStarts[codeStarts.Count - 1] && nameOffset < chunkEnd)
                        ? nameOffset : chunkEnd;
                    for (int v = 0; v < codeStarts.Count; v++)
                    {
                        int segStart = codeStarts[v];
                        int segEnd = v + 1 < codeStarts.Count ? codeStarts[v + 1] : finalBound;
                        AddBytecodeStrings(entries, data, segStart, segEnd, lf + "." + obj + ".v" + v.ToString("D2"), isIndy3, codec);
                    }
                }
            }
        }

        // --- scripts ----------------------------------------------------------

        private static void AddLocalScripts(List<GameTextEntry> entries, byte[] data, ScummV3OldRoom room,
            List<V3OldChunk> chunks, string lf, bool isIndy3, GameTextCodec codec)
        {
            // The local-script table follows the object tables + the sound/script id lists.
            int p = 29 + room.NumObjects * 4 + room.NumSounds + room.NumScripts;
            while (p + 3 <= data.Length)
            {
                int id = data[p];
                if (id == 0) break; // terminator
                int offset = ReadU16(data, p + 1);
                p += 3;
                AddScriptChunk(entries, data, chunks, offset, lf + ".LS" + id.ToString("D3"), isIndy3, codec);
            }
        }

        private static void AddGlobalScripts(List<GameTextEntry> entries, byte[] data, ScummV3OldBundleIndexFile index,
            List<V3OldChunk> chunks, int roomNo, string lf, bool isIndy3, GameTextCodec codec)
        {
            if (index == null || index.ScriptDirectory == null) return;
            V3OldResourceDirectory dir = index.ScriptDirectory;
            for (int s = 0; s < dir.Count; s++)
            {
                if (dir.RoomNumbers[s] != roomNo) continue;
                int offset = dir.Offsets[s];
                if (offset == 0xFFFF || offset == 0) continue; // absent
                AddScriptChunk(entries, data, chunks, offset, lf + ".SC" + s.ToString("D3"), isIndy3, codec);
            }
        }

        /// <summary>Disassembles a script chunk ([size:u16][2 bytes][bytecode]) and adds its strings.</summary>
        private static void AddScriptChunk(List<GameTextEntry> entries, byte[] data, List<V3OldChunk> chunks,
            int chunkStart, string id, bool isIndy3, GameTextCodec codec)
        {
            if (chunkStart < 0 || chunkStart + 4 > data.Length) return;
            // Bound the bytecode by the chunk chain (the resource's own chunk), not the leading size
            // word, which is not a dependable length for every resource.
            int chunkEnd = ChunkEndContaining(chunks, chunkStart, data.Length);
            AddBytecodeStrings(entries, data, chunkStart + 4, chunkEnd, id, isIndy3, codec);
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
