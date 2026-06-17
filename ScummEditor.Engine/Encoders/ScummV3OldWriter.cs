using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Applies a size-changing edit to a SCUMM v3 "old bundle" room file and fixes up every offset the
    /// change shifts, so the result stays a valid, loadable game. This is the byte-safe write-back core
    /// for v3old editing (object names now; script / verb-code bytecode reuse it once their slice is
    /// rebuilt).
    ///
    /// A v3old room file is: the ROOM resource at [0, size@0) - which holds the room header, the EGA
    /// background, the box data and ALL the object images/code, reached by file-relative offsets - then
    /// that room's script/sound/costume sub-resources, each a [size:u16][payload] chunk located by the
    /// index. (Confirmed against scummvm resource.cpp loadResource + the real game bytes: size@0 is the
    /// room resource length, e.g. Loom 65.LFL @0=1580 in a 1775-byte file.)
    ///
    /// So splicing delta bytes at position P shifts everything after P. What must be re-pointed:
    ///  - if P is inside the room resource (P &lt; size@0): the room size word @0; the room-header
    ///    offsets IM00@0x0A / BOXD@0x15 / EXCD@0x19 / ENCD@0x1B; the OBIM and OBCD offset tables at +29;
    ///    the local-script table; and (because every sub-resource now sits delta later) every index
    ///    Script/Sound/Costume offset for this room.
    ///  - if P is inside a sub-resource: that chunk's own [size:u16] word, and the index offsets of the
    ///    sub-resources that follow it in this room.
    /// Object-name pointers (OBCD+16) and intra-OBCD verb tables are objptr-relative and the object's
    /// own start never moves relative to its name/code on a name edit, so they are left unchanged.
    /// </summary>
    public static class ScummV3OldWriter
    {
        /// <summary>
        /// Replaces <paramref name="oldLen"/> bytes at <paramref name="editOffset"/> of the room file
        /// with <paramref name="newBytes"/>, and re-points every offset (in the room file and the index)
        /// that the size change shifts. delta == 0 leaves the bytes byte-identical.
        /// </summary>
        public static void ApplyEdit(ScummV3OldBundleDataFile dataFile, ScummV3OldBundleIndexFile index,
            int roomNo, int editOffset, int oldLen, byte[] newBytes, int sizeWordOffset = -1)
        {
            byte[] old = dataFile.RawContent;
            int delta = newBytes.Length - oldLen;

            var result = new byte[old.Length + delta];
            System.Array.Copy(old, 0, result, 0, editOffset);
            System.Array.Copy(newBytes, 0, result, editOffset, newBytes.Length);
            int tailStart = editOffset + oldLen;
            System.Array.Copy(old, tailStart, result, editOffset + newBytes.Length, old.Length - tailStart);
            dataFile.RawContent = result;

            if (delta == 0)
            {
                dataFile.ReparseChunks();
                return;
            }

            // The room-header / object-table / local-script-table re-pointing is a no-op for an edit in
            // a sub-resource (every room value sits before it), so it can run unconditionally; the room
            // size word @0 only grows when the edit is actually inside the room resource.
            int oldRoomSize = ReadU16(old, 0);
            FixUpRoomResource(result, editOffset, delta, growRoomSize: editOffset < oldRoomSize);

            // The edited resource's OWN [size:u16] word (a script chunk's; -1 for object names / verb
            // code, which carry no length and live inside the room resource sized by @0).
            if (sizeWordOffset >= 0 && sizeWordOffset + 1 < result.Length)
                WriteU16(result, sizeWordOffset, ReadU16(result, sizeWordOffset) + delta);

            FixUpIndex(index, roomNo, editOffset, delta);
            dataFile.ReparseChunks();
        }

        /// <summary>Re-points the room header / object tables / intra-object pointers shifted by an edit.</summary>
        private static void FixUpRoomResource(byte[] buf, int editOffset, int delta, bool growRoomSize)
        {
            int numObjects = buf[20];
            int numSounds = buf[23];
            int numScripts = buf[24];
            int obim = 29;
            int obcd = 29 + numObjects * 2;

            // FIRST, while the OBCD table still holds the OLD objptr values, fix the OBJECT-INTERNAL
            // pointers (name pointer @objptr+16 and the verb-table offsets @objptr+17). These are
            // objptr-RELATIVE: if an object's own start sits at/before the edit but a target (its name
            // or a verb's code) sits after it, that target moved by delta while the object start did
            // not, so the relative pointer must grow. Objects whose start is after the edit move whole,
            // so their relative pointers are unchanged (handled by the OBCD-table shift below).
            for (int i = 0; i < numObjects; i++)
            {
                int objptr = ReadU16(buf, obcd + i * 2);
                if (objptr <= 0 || objptr > editOffset) continue; // moves whole, or invalid

                // name pointer (1 byte, objptr-relative)
                if (objptr + 16 < buf.Length)
                {
                    int nameByte = buf[objptr + 16];
                    if (nameByte != 0 && objptr + nameByte > editOffset)
                    {
                        int updated = nameByte + delta;
                        if (updated < 0 || updated > 0xFF)
                            throw new Exceptions.ImageEncodeException("object name moved out of the 1-byte pointer range; shorten the edit");
                        buf[objptr + 16] = (byte)updated;
                    }
                }

                // verb table: [verbId:1][offset:u16]* until verbId==0
                int vp = objptr + 17;
                while (vp + 2 < buf.Length && buf[vp] != 0)
                {
                    int off = ReadU16(buf, vp + 1);
                    if (objptr + off > editOffset) WriteU16(buf, vp + 1, off + delta);
                    vp += 3;
                }
            }

            // Room resource size word (@0) grows only when the edit is inside the room resource.
            if (growRoomSize) ShiftIfAfter(buf, 0, editOffset, delta, alwaysIfSizeWord: true);

            // Header sub-block pointers (file-relative offsets into the room resource).
            ShiftIfAfter(buf, 0x0A, editOffset, delta); // IM00 (background)
            ShiftIfAfter(buf, 0x15, editOffset, delta); // BOXD
            ShiftIfAfter(buf, 0x19, editOffset, delta); // EXCD (exit script)
            ShiftIfAfter(buf, 0x1B, editOffset, delta); // ENCD (entry script)

            // OBIM then OBCD offset tables (numObjects u16 each) at +29.
            for (int i = 0; i < numObjects; i++)
            {
                ShiftIfAfter(buf, obim + i * 2, editOffset, delta);
                ShiftIfAfter(buf, obcd + i * 2, editOffset, delta);
            }

            // Local-script table: [id:1][offset:u16]* terminated by id==0.
            int p = 29 + numObjects * 4 + numSounds + numScripts;
            while (p + 3 <= buf.Length && buf[p] != 0)
            {
                ShiftIfAfter(buf, p + 1, editOffset, delta);
                p += 3;
            }
        }

        /// <summary>Re-points the index Script/Sound/Costume offsets of this room's shifted sub-resources.</summary>
        private static void FixUpIndex(ScummV3OldBundleIndexFile index, int roomNo, int editOffset, int delta)
        {
            if (index == null) return;
            FixUpDirectory(index, index.ScriptDirectory, roomNo, editOffset, delta);
            FixUpDirectory(index, index.SoundDirectory, roomNo, editOffset, delta);
            FixUpDirectory(index, index.CostumeDirectory, roomNo, editOffset, delta);
        }

        private static void FixUpDirectory(ScummV3OldBundleIndexFile index, V3OldResourceDirectory dir,
            int roomNo, int editOffset, int delta)
        {
            if (dir == null) return;
            byte[] raw = index.RawContent;
            for (int i = 0; i < dir.Count; i++)
            {
                if (dir.RoomNumbers[i] != roomNo) continue;
                int off = dir.Offsets[i];
                if (off == 0xFFFF) continue;     // absent resource
                if (off <= editOffset) continue; // before the edit - unaffected
                int updated = off + delta;
                dir.Offsets[i] = updated; // keep the parsed overlay in sync with the bytes
                WriteU16(raw, dir.OffsetArrayPosition + i * 2, updated);
            }
        }

        /// <summary>
        /// If the u16 at <paramref name="fieldPos"/> points strictly past <paramref name="editOffset"/>
        /// (or is the room size word), add <paramref name="delta"/> to it. The field position itself is
        /// always before the edit (it lives in the room header), so it does not move.
        /// </summary>
        private static void ShiftIfAfter(byte[] buf, int fieldPos, int editOffset, int delta, bool alwaysIfSizeWord = false)
        {
            if (fieldPos + 1 >= buf.Length) return;
            int value = ReadU16(buf, fieldPos);
            if (alwaysIfSizeWord || value > editOffset)
            {
                WriteU16(buf, fieldPos, value + delta);
            }
        }

        private static int ReadU16(byte[] data, int p)
        {
            return data[p] | (data[p + 1] << 8);
        }

        private static void WriteU16(byte[] data, int p, int value)
        {
            data[p] = (byte)(value & 0xFF);
            data[p + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
