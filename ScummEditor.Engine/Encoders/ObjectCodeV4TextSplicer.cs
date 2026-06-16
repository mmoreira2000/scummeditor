using System;
using System.Collections.Generic;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Rewrites the object name or verb bytecode inside a SCUMM v4 "OC" block when a translated string
    /// changes its length. Unlike v5/v6 (where VERB and OBNA are independent tag/size sub-blocks), a v4
    /// OC body is one flat region: a 12-byte header, a 1-byte name pointer (body+12), a verb table
    /// (body+13: [verbId:8][offset:16le] entries, 0x00-terminated), then the name string and the verb
    /// bytecode. The name pointer and verb-table offsets are all relative to the OC BLOCK start - 6
    /// bytes before RawContent - so a RawContent index is the stored offset minus HeaderLength.
    ///
    /// Growing/shrinking the name shifts everything after it (the verb code) and every verb-table offset
    /// that points past it; growing/shrinking the verb code shifts the verb-table offsets via the same
    /// old->new position map the script rebuild uses. The name pointer itself never moves (the name sits
    /// at a fixed position right after the verb table), so it is left untouched. After splicing, the
    /// block is re-parsed and sanity-checked; on any inconsistency the caller leaves the block unchanged.
    /// </summary>
    public static class ObjectCodeV4TextSplicer
    {
        public static bool RebuildObjectName(ObjectCode obcd, byte[] newName, out string error)
        {
            error = null;
            byte[] raw = obcd.RawContent;
            int headerLength = (int)obcd.HeaderLength;

            int nameStart = obcd.ObnaBodyOffset;        // RawContent index of the name
            int oldNameLen = obcd.ObnaBodyLength;        // name bytes, excluding the 0x00 terminator
            if (nameStart < 0)
            {
                error = "object has no name to rewrite";
                return false;
            }
            if (nameStart + oldNameLen >= raw.Length || raw[nameStart + oldNameLen] != 0x00)
            {
                error = "object name is not null-terminated";
                return false;
            }

            int afterName = nameStart + oldNameLen + 1; // first byte after the terminator
            int delta = newName.Length - oldNameLen;

            var rebuilt = new byte[raw.Length + delta];
            Array.Copy(raw, 0, rebuilt, 0, nameStart);                                  // header + name pointer + verb table
            Array.Copy(newName, 0, rebuilt, nameStart, newName.Length);                 // new name
            rebuilt[nameStart + newName.Length] = 0x00;                                 // terminator
            Array.Copy(raw, afterName, rebuilt, nameStart + newName.Length + 1, raw.Length - afterName); // tail (verb code)

            // Shift every verb-table offset that points after the name; refuse if one points into it.
            if (!RemapVerbTable(obcd, rebuilt, headerLength,
                    delegate(int rawIndex)
                    {
                        if (rawIndex >= afterName) return rawIndex + delta;
                        if (rawIndex >= nameStart) return -1; // points inside the name region - unsafe
                        return rawIndex;
                    }, out error))
            {
                return false;
            }

            obcd.RawContent = rebuilt;
            obcd.Reparse();
            if (obcd.ObnaBodyOffset != nameStart || obcd.ObnaBodyLength != newName.Length)
            {
                error = "the rebuilt object name does not re-parse";
                return false;
            }
            return true;
        }

        public static bool ReplaceVerbCode(ObjectCode obcd, byte[] newCode, ScummV6Disassembler.Result scan,
                                           Dictionary<int, byte[]> replacements, out string error)
        {
            error = null;
            byte[] raw = obcd.RawContent;
            int headerLength = (int)obcd.HeaderLength;
            int oldVerbStart = obcd.VerbCodeOffset; // RawContent index of the verb bytecode
            int oldVerbLen = obcd.VerbCodeLength;

            // Old->new position map within the verb-code region (same scheme as GameTextManager.RebuildCode).
            var newLengths = new int[scan.Strings.Count];
            for (int k = 0; k < scan.Strings.Count; k++)
            {
                byte[] content;
                newLengths[k] = replacements.TryGetValue(k, out content)
                    ? content.Length + (scan.Strings[k].Terminated ? 1 : 0)
                    : scan.Strings[k].Length;
            }
            Func<int, int> mapInSlice = delegate(int pos)
            {
                int d = 0;
                for (int k = 0; k < scan.Strings.Count; k++)
                {
                    ScummV6Disassembler.StringRef s = scan.Strings[k];
                    if (s.Offset + s.Length <= pos) d += newLengths[k] - s.Length;
                    else break;
                }
                return pos + d;
            };

            var rebuilt = new byte[raw.Length - oldVerbLen + newCode.Length];
            Array.Copy(raw, 0, rebuilt, 0, oldVerbStart);                                   // header + table + name
            Array.Copy(newCode, 0, rebuilt, oldVerbStart, newCode.Length);                  // new verb bytecode
            int oldTail = oldVerbStart + oldVerbLen;
            Array.Copy(raw, oldTail, rebuilt, oldVerbStart + newCode.Length, raw.Length - oldTail); // anything after

            // Remap each verb-table offset that points into the verb code through the splice map.
            if (!RemapVerbTable(obcd, rebuilt, headerLength,
                    delegate(int rawIndex)
                    {
                        int sliceRel = rawIndex - oldVerbStart;
                        if (sliceRel < 0 || sliceRel > oldVerbLen) return rawIndex; // outside the verb code: unchanged
                        return oldVerbStart + mapInSlice(sliceRel);
                    }, out error))
            {
                return false;
            }

            obcd.RawContent = rebuilt;
            obcd.Reparse();
            if (obcd.VerbCodeLength != newCode.Length)
            {
                error = "the rebuilt verb block does not re-parse";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Rewrites the verb-table offsets in <paramref name="rebuilt"/> (which already holds the
        /// verbatim verb table at obcd.VerbTablePos). <paramref name="mapRawIndex"/> maps a verb's old
        /// RawContent index to its new one (or returns -1 to signal an unsafe layout). The stored
        /// offsets are block-relative, so they are (rawIndex + HeaderLength).
        /// </summary>
        private static bool RemapVerbTable(ObjectCode obcd, byte[] rebuilt, int headerLength,
                                           Func<int, int> mapRawIndex, out string error)
        {
            error = null;
            List<VerbEntry> entries = obcd.VerbEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                int oldOffset = entries[i].Offset;            // block-relative
                int oldRawIndex = oldOffset - headerLength;
                int newRawIndex = mapRawIndex(oldRawIndex);
                if (newRawIndex < 0)
                {
                    error = "verb " + entries[i].Id + " offset points inside a rewritten string";
                    return false;
                }
                int newOffset = newRawIndex + headerLength;   // back to block-relative
                if (newOffset > 0xFFFF)
                {
                    error = "verb " + entries[i].Id + " offset exceeds 0xFFFF after the translation";
                    return false;
                }
                int p = obcd.VerbTablePos + i * 3 + 1;        // [verbId:8][offset:16le]
                rebuilt[p] = (byte)(newOffset & 0xFF);
                rebuilt[p + 1] = (byte)((newOffset >> 8) & 0xFF);
            }
            return true;
        }
    }
}
