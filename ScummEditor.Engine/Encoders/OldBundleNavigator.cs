using System;
using System.Collections.Generic;
using System.Text;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Read-side navigation overlay for the v2 / v3-old (GF_OLD_BUNDLE) room files, used by the GUI tree
    /// to list a room's objects and scripts and to render a script disassembly / object code / room
    /// properties / index directory on demand. It produces only positions and text - never rebuilt bytes
    /// - so it cannot corrupt a game; editing stays in ScummV2TextManager / ScummV3OldTextManager.
    ///
    /// The structural bounding (where a length-less script / verb-code block ends) mirrors those two text
    /// managers exactly; it is duplicated here on purpose so this navigator is self-contained and the
    /// tested text export/import pipeline is left untouched. The disassembler is chosen by version:
    /// ScummV12Disassembler for v1/v2, ScummV3Disassembler (old-bundle mode) for v3.
    /// </summary>
    public static class OldBundleNavigator
    {
        // -----------------------------------------------------------------
        // Model build (one room) - drives the tree nodes
        // -----------------------------------------------------------------

        public static OldBundleRoomModel BuildRoomModel(ScummGameData game, ScummV3OldBundleDataFile df, int roomNo)
        {
            bool isV2 = game.LoadedGameInfo != null && game.LoadedGameInfo.ScummVersion <= 2;
            bool isV1 = game.LoadedGameInfo != null && game.LoadedGameInfo.ScummVersion == 1;
            bool isIndy3 = game.LoadedGameInfo != null && game.LoadedGameInfo.LoadedGame == ScummGame.IndianaJones3;
            bool isManiac = game.LoadedGameInfo != null && game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var index = game.IndexFile as ScummV3OldBundleIndexFile;

            var model = new OldBundleRoomModel { RoomNo = roomNo, IsV2 = isV2, IsV1 = isV1, IsIndy3 = isIndy3 };
            byte[] data = df != null ? df.RawContent : null;
            if (data == null) return model;

            if (isV2) BuildV2(model, data, index, roomNo, isV1, isManiac);
            else BuildV3(model, data, index, roomNo);
            return model;
        }

        private static void BuildV2(OldBundleRoomModel model, byte[] data, ScummV3OldBundleIndexFile index, int roomNo, bool isV1, bool isManiac)
        {
            // v1 (Maniac/Zak classic) and v2 (Enhanced) share the room object/script/verb layout; only the
            // image codec differs (v1 = GdiV1 tilemap, v2 = GdiV2 vertical RLE) and v1 reads width/height as
            // CHAR-unit bytes. A ScummV1Room is-a ScummV2Room, so the object/script accessors are reused.
            ScummV2Room room = isV1 ? new ScummV1Room(data) : new ScummV2Room(data);
            model.Width = room.Width; model.Height = room.Height;
            model.NumObjects = room.NumObjects; model.NumSounds = room.NumSounds; model.NumScripts = room.NumScripts;

            Func<int, bool> hasObjectImage;
            Func<int, bool> hasObjectZPlane;
            if (isV1)
            {
                var room1 = (ScummV1Room)room;
                var dec = new ScummV1ImageDecoder(isManiac);
                using (var bg = dec.DecodeBackground(room1)) model.HasBackground = bg != null;
                using (var zp = dec.DecodeBackgroundZPlane(room1)) model.HasBackgroundZPlane = zp != null;
                hasObjectImage = i => { using (var b = dec.DecodeObject(room1, i)) { return b != null; } };
                hasObjectZPlane = i => { using (var z = dec.DecodeObjectZPlane(room1, i)) { return z != null; } };
            }
            else
            {
                var dec = new ScummV2ImageDecoder();
                using (var bg = dec.DecodeBackground(room)) model.HasBackground = bg != null;
                using (var zp = dec.DecodeBackgroundZPlane(room)) model.HasBackgroundZPlane = zp != null;
                hasObjectImage = i => { using (var b = dec.DecodeObject(room, i)) { return b != null; } };
                hasObjectZPlane = i => { using (var z = dec.DecodeObjectZPlane(room, i)) { return z != null; } };
            }

            List<int> boundaries = CollectBoundariesV2(data, room);

            for (int i = 0; i < room.NumObjects; i++)
            {
                int objptr = room.ObjectCodeOffset(i);
                // Skip phantom objects with no real OBCD offset, matching ScummV2TextManager (objptr<=0):
                // listing them would surface room-header bytes as a fake id/size via the unguarded accessors.
                if (objptr <= 0 || objptr >= data.Length) continue;

                var info = new OldBundleObjectInfo
                {
                    Index = i,
                    Id = room.ObjectId(i),
                    Width = room.ObjectWidth(i),
                    Height = room.ObjectHeight(i)
                };
                info.HasImage = hasObjectImage(i);
                info.HasZPlane = hasObjectZPlane(i);

                int nameRel = room.ObjectNameRelativeOffset(i);
                if (objptr > 0 && nameRel != 0)
                {
                    int nameOffset = objptr + nameRel;
                    if (nameOffset > 0 && nameOffset < data.Length)
                        info.Name = DecodePrintable(data, nameOffset, ZeroTerminatedLength(data, nameOffset, data.Length));
                }

                if (objptr > 0 && objptr < data.Length)
                {
                    int objEnd = NextBoundaryAbove(boundaries, objptr, data.Length);
                    // v2 verb table: [verbId:1][offset:1]* at objptr+15, terminated by verbId==0.
                    var verbs = new List<int[]>(); // {verbId, absOffset}
                    int p = objptr + 15;
                    while (p + 1 < data.Length && data[p] != 0)
                    {
                        int rel = data[p + 1];
                        if (rel != 0) verbs.Add(new[] { data[p], objptr + rel });
                        p += 2;
                    }
                    info.VerbCode = BuildVerbRanges(verbs, objEnd);
                }
                model.Objects.Add(info);
            }

            AddRoomScript(model, data, boundaries, room.EntryScriptOffset, OldBundleCodeKind.EntryScript, "Entry script");
            AddRoomScript(model, data, boundaries, room.ExitScriptOffset, OldBundleCodeKind.ExitScript, "Exit script");
            AddGlobalScripts(model, data, index, roomNo);
        }

        private static void BuildV3(OldBundleRoomModel model, byte[] data, ScummV3OldBundleIndexFile index, int roomNo)
        {
            var room = new ScummV3OldRoom(data);
            var dec = new ScummV3OldImageDecoder();
            model.Width = room.Width; model.Height = room.Height;
            model.NumObjects = room.NumObjects; model.NumSounds = room.NumSounds; model.NumScripts = room.NumScripts;
            using (var bg = dec.DecodeBackground(room)) model.HasBackground = bg != null;
            model.HasBackgroundZPlane = dec.CountBackgroundZPlanes(room) > 0;
            List<int> boundaries = CollectBoundariesV3(data, room);

            for (int i = 0; i < room.NumObjects; i++)
            {
                int objptr = room.ObjectCodeOffset(i);
                // Skip phantom objects, matching ScummV3OldTextManager (objptr<=2: the v3 OBCD header base
                // is objptr-2, so <=2 also guards that read). Different threshold from v2 on purpose.
                if (objptr <= 2 || objptr >= data.Length) continue;

                var info = new OldBundleObjectInfo
                {
                    Index = i,
                    Id = room.ObjectId(i),
                    Width = room.ObjectWidth(i),
                    Height = room.ObjectHeight(i)
                };
                using (var bmp = dec.DecodeObject(room, i)) info.HasImage = bmp != null;
                info.HasZPlane = dec.CountObjectZPlanes(room, i) > 0;

                int nameRel = (objptr > 0 && objptr + 16 < data.Length) ? data[objptr + 16] : 0;
                if (objptr > 0 && nameRel != 0)
                {
                    int nameOffset = objptr + nameRel;
                    if (nameOffset > 0 && nameOffset < data.Length)
                        info.Name = DecodePrintable(data, nameOffset, ZeroTerminatedLength(data, nameOffset, data.Length));
                }

                if (objptr > 0 && objptr < data.Length)
                {
                    int objEnd = NextBoundaryAbove(boundaries, objptr, data.Length);
                    // v3 verb table: [verbId:1][offset:u16 LE]* at objptr+17, terminated by verbId==0.
                    var verbs = new List<int[]>();
                    int p = objptr + 17;
                    while (p + 2 < data.Length && data[p] != 0)
                    {
                        int rel = ReadU16(data, p + 1);
                        if (rel != 0) verbs.Add(new[] { data[p], objptr + rel });
                        p += 3;
                    }
                    info.VerbCode = BuildVerbRanges(verbs, objEnd);
                }
                model.Objects.Add(info);
            }

            AddRoomScript(model, data, boundaries, room.EntryScriptOffset, OldBundleCodeKind.EntryScript, "Entry script");
            AddRoomScript(model, data, boundaries, room.ExitScriptOffset, OldBundleCodeKind.ExitScript, "Exit script");

            // Local scripts: room-header table [id:1][offset:u16]*, the offset points directly at bytecode.
            int q = 29 + room.NumObjects * 4 + room.NumSounds + room.NumScripts;
            while (q + 3 <= data.Length && data[q] != 0)
            {
                int id = data[q];
                int off = ReadU16(data, q + 1);
                q += 3;
                if (off <= 0 || off >= data.Length) continue;
                model.Scripts.Add(new OldBundleCodeRange
                {
                    Kind = OldBundleCodeKind.LocalScript,
                    Label = "Local script " + id,
                    Number = id,
                    Start = off,
                    End = NextBoundaryAbove(boundaries, off, data.Length)
                });
            }

            AddGlobalScripts(model, data, index, roomNo);
        }

        /// <summary>
        /// Turns the (verbId, offset) pairs into code ranges. Several verbs can point at the SAME offset
        /// (they share one body, common in v2), so the ranges are keyed by DISTINCT offset - each labelled
        /// with all the verb ids that map to it - and bounded by the next distinct offset or the object
        /// end. A verb that points at/after the object end has no body of its own (Start == End).
        /// </summary>
        private static List<OldBundleCodeRange> BuildVerbRanges(List<int[]> verbs, int objEnd)
        {
            var ranges = new List<OldBundleCodeRange>();
            if (verbs.Count == 0) return ranges;
            verbs.Sort((a, b) => a[1].CompareTo(b[1]));

            var distinct = new List<int>();
            foreach (int[] v in verbs)
                if (distinct.Count == 0 || distinct[distinct.Count - 1] != v[1]) distinct.Add(v[1]);

            for (int d = 0; d < distinct.Count; d++)
            {
                int start = distinct[d];
                int end = d + 1 < distinct.Count ? distinct[d + 1] : objEnd;
                if (end < start) end = start; // verb points past the object: no body

                var ids = new List<int>();
                foreach (int[] v in verbs) if (v[1] == start) ids.Add(v[0]);

                ranges.Add(new OldBundleCodeRange
                {
                    Kind = OldBundleCodeKind.ObjectVerb,
                    Label = ids.Count == 1 ? "verb " + ids[0] : "verb " + string.Join(", ", ids),
                    Number = ids[0],
                    Start = start,
                    End = end
                });
            }
            return ranges;
        }

        private static void AddRoomScript(OldBundleRoomModel model, byte[] data, List<int> boundaries,
            int offset, OldBundleCodeKind kind, string label)
        {
            if (offset <= 0 || offset >= data.Length) return;
            model.Scripts.Add(new OldBundleCodeRange
            {
                Kind = kind,
                Label = label,
                Number = -1,
                Start = offset,
                End = NextBoundaryAbove(boundaries, offset, data.Length)
            });
        }

        private static void AddGlobalScripts(OldBundleRoomModel model, byte[] data, ScummV3OldBundleIndexFile index, int roomNo)
        {
            if (index == null || index.ScriptDirectory == null) return;
            V3OldResourceDirectory dir = index.ScriptDirectory;
            for (int s = 0; s < dir.Count; s++)
            {
                if (dir.RoomNumbers[s] != roomNo) continue;
                int off = dir.Offsets[s];
                if (off == 0xFFFF || off == 0 || off + 4 > data.Length) continue;
                int end = ScriptEnd(data, off, NextResourceOffsetInRoom(index, roomNo, off, data.Length));
                model.Scripts.Add(new OldBundleCodeRange
                {
                    Kind = OldBundleCodeKind.GlobalScript,
                    Label = "Global script " + s,
                    Number = s,
                    Start = off + 4, // resource header [size:u16][2] precedes the bytecode
                    End = end
                });
            }
        }

        // -----------------------------------------------------------------
        // Disassembly (one resource) - the GUI viewers format the result
        // -----------------------------------------------------------------

        /// <summary>
        /// Disassembles a bytecode range to the raw disassembler Result (Listing / DecodedToEnd /
        /// BytesDecoded). Returns null when the range is empty/invalid. The right engine is chosen by
        /// version (v1/v2 byte language vs v3 old-bundle). Display formatting is the GUI's job.
        /// </summary>
        public static ScummV6Disassembler.Result DisassembleRange(byte[] data, int start, int end, bool isV2, bool isIndy3, bool isV1)
        {
            if (data == null || start < 0 || end <= start || end > data.Length) return null;
            var slice = new byte[end - start];
            Array.Copy(data, start, slice, 0, slice.Length);
            // v1 and v2 both use the byte-oriented ScummV12 language; isV1 selects the one stream-affecting
            // difference (actorOps Color reads no extra byte on v1). v3 old-bundle uses ScummV3Disassembler.
            return isV2
                ? ScummV12Disassembler.Disassemble(slice, 0, null, isV1)
                : ScummV3Disassembler.Disassemble(slice, 0, null, isIndy3, true);
        }

        // -----------------------------------------------------------------
        // Structural bounding (mirrors ScummV2TextManager / ScummV3OldTextManager)
        // -----------------------------------------------------------------

        private static List<int> CollectBoundariesV2(byte[] data, ScummV2Room room)
        {
            var b = new List<int>();
            int roomSize = data.Length >= 2 ? (data[0] | (data[1] << 8)) : data.Length;
            b.Add(roomSize > 0 && roomSize <= data.Length ? roomSize : data.Length);
            AddBoundary(b, room.ImageOffset);
            AddBoundary(b, room.BoxOffset); // box data is structural (ScummV2Room.NextStructuralOffsetAbove), align with v3
            AddBoundary(b, room.ExitScriptOffset);
            AddBoundary(b, room.EntryScriptOffset);
            for (int i = 0; i < room.NumObjects; i++)
            {
                AddBoundary(b, room.ObjectImageOffset(i));
                int objptr = room.ObjectCodeOffset(i);
                AddBoundary(b, objptr);
                int nameRel = room.ObjectNameRelativeOffset(i);
                if (nameRel != 0) AddBoundary(b, objptr + nameRel);
            }
            b.Sort();
            return b;
        }

        private static List<int> CollectBoundariesV3(byte[] data, ScummV3OldRoom room)
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

        private static void AddBoundary(List<int> list, int value) { if (value > 0) list.Add(value); }

        private static int NextBoundaryAbove(List<int> boundaries, int offset, int fallback)
        {
            int best = fallback;
            foreach (int x in boundaries) if (x > offset && x < best) best = x;
            return best;
        }

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

        private static int ZeroTerminatedLength(byte[] data, int offset, int limit)
        {
            int n = 0;
            while (offset + n < limit && data[offset + n] != 0) n++;
            return n;
        }

        /// <summary>Decodes a stored name as printable ASCII for display; non-printable bytes become {0xNN}.</summary>
        private static string DecodePrintable(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder(length);
            for (int i = 0; i < length && offset + i < data.Length; i++)
            {
                byte c = data[offset + i];
                if (c >= 0x20 && c <= 0x7E) sb.Append((char)c);
                else sb.Append("{0x" + c.ToString("X2") + "}");
            }
            return sb.ToString();
        }

        private static int ReadU16(byte[] data, int p)
        {
            if (p < 0 || p + 1 >= data.Length) return 0;
            return data[p] | (data[p + 1] << 8);
        }
    }
}
