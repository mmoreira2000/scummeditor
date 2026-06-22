using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Applies a size-changing edit to a SCUMM v2 room file (Maniac Mansion / Zak McKracken) and re-points
    /// every offset the change shifts, so the result stays a valid, loadable game. It is the v2 analogue
    /// of ScummV3OldWriter: same container model (the ROOM resource at [0, size@0) holds the header, the
    /// background and all object image/code reached by file-relative offsets, followed by the room's
    /// script/sound/costume sub-resources located by the index), but the v2 room header is shifted one
    /// byte from v3old and the verb-table offsets are a single byte:
    ///   numObjects @20, numSounds @22, numScripts @23; IM00 @0x0A; BOXD @0x15 (a BYTE); EXCD @0x18;
    ///   ENCD @0x1A; OBIM/OBCD tables at +28; object name pointer @objptr+14; verb table @objptr+15 as
    ///   [verbId:1][offset:1]*; and there is NO room-local-script table.
    /// </summary>
    public static class ScummV2Writer
    {
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

            if (delta == 0)
            {
                dataFile.RawContent = result;
                dataFile.ReparseChunks();
                return;
            }

            // Do every room-resource fix-up on the local buffer FIRST: FixUpRoomResource can throw when an
            // edit grows past a 1-byte offset's range, and only after it succeeds do we publish the new
            // bytes - so a rejected edit leaves the data file untouched and the caller can report it.
            int oldRoomSize = ReadU16(old, 0);
            FixUpRoomResource(result, editOffset, delta, growRoomSize: editOffset < oldRoomSize);

            if (sizeWordOffset >= 0 && sizeWordOffset + 1 < result.Length)
                WriteU16(result, sizeWordOffset, ReadU16(result, sizeWordOffset) + delta);

            dataFile.RawContent = result;
            FixUpIndex(index, roomNo, editOffset, delta);
            dataFile.ReparseChunks();
        }

        /// <summary>
        /// Replaces the ENTIRE room resource ([0, sizeWord@0)) of an old-bundle file with an already
        /// self-consistent <paramref name="newRoom"/> (its own internal offsets + size word are correct), then
        /// relocates the index entries of the costume / script / sound sub-resources packed after the room by
        /// the size change. Unlike ApplyEdit, it does NOT run FixUpRoomResource - the caller (the v1 compact
        /// room rebuilder) has already laid out the room's internal offsets, so re-shifting them would corrupt it.
        /// </summary>
        public static void ReplaceRoomResource(ScummV3OldBundleDataFile dataFile, ScummV3OldBundleIndexFile index,
            int roomNo, byte[] newRoom)
        {
            byte[] old = dataFile.RawContent;
            int oldRoomSize = ReadU16(old, 0);
            if (oldRoomSize <= 0 || oldRoomSize > old.Length) oldRoomSize = old.Length;
            int delta = newRoom.Length - oldRoomSize;

            var result = new byte[old.Length + delta];
            System.Array.Copy(newRoom, 0, result, 0, newRoom.Length);
            System.Array.Copy(old, oldRoomSize, result, newRoom.Length, old.Length - oldRoomSize);

            dataFile.RawContent = result;
            // The room stays at file offset 0; only the sub-resources after it (offset >= oldRoomSize) move.
            FixUpIndex(index, roomNo, oldRoomSize - 1, delta);
            dataFile.ReparseChunks();
        }

        private static void FixUpRoomResource(byte[] buf, int editOffset, int delta, bool growRoomSize)
        {
            int numObjects = buf.Length > 20 ? buf[20] : 0;
            int obim = 28;
            int obcd = 28 + numObjects * 2;

            // Object-internal pointers (name @objptr+14, verb table @objptr+15) are objptr-relative; fix
            // them while the OBCD table still holds the OLD objptr values. Only objects whose own start is
            // at/before the edit but whose target moved need adjusting; later objects move whole.
            for (int i = 0; i < numObjects; i++)
            {
                int objptr = ReadU16(buf, obcd + i * 2);
                if (objptr <= 0 || objptr > editOffset) continue;

                // name pointer (1 byte)
                if (objptr + 14 < buf.Length)
                {
                    int nameByte = buf[objptr + 14];
                    if (nameByte != 0 && objptr + nameByte > editOffset)
                    {
                        int updated = nameByte + delta;
                        if (updated < 0 || updated > 0xFF)
                            throw new Exceptions.ImageEncodeException("v2 object name moved out of the 1-byte pointer range; shorten the edit");
                        buf[objptr + 14] = (byte)updated;
                    }
                }

                // verb table: [verbId:1][offset:1]* until verbId==0 (BYTE offsets in v2)
                int vp = objptr + 15;
                while (vp + 1 < buf.Length && buf[vp] != 0)
                {
                    int off = buf[vp + 1];
                    if (off != 0 && objptr + off > editOffset)
                    {
                        int updated = off + delta;
                        if (updated < 0 || updated > 0xFF)
                            throw new Exceptions.ImageEncodeException("v2 verb-code offset moved out of the 1-byte range; shorten the edit");
                        buf[vp + 1] = (byte)updated;
                    }
                    vp += 2;
                }
            }

            if (growRoomSize) WriteU16(buf, 0, ReadU16(buf, 0) + delta);

            ShiftIfAfter(buf, 0x0A, editOffset, delta); // IM00 (background)
            ShiftIfAfter(buf, 0x18, editOffset, delta); // EXCD (exit script)
            ShiftIfAfter(buf, 0x1A, editOffset, delta); // ENCD (entry script)

            // BOXD is a single byte in v2; shift only if the box data sits after the edit (rare - boxes
            // are early in the room, before the scripts/object code that translation edits).
            if (buf.Length > 0x15)
            {
                int box = buf[0x15];
                if (box != 0 && box > editOffset)
                {
                    int updated = box + delta;
                    if (updated < 0 || updated > 0xFF)
                        throw new Exceptions.ImageEncodeException("v2 box-data offset moved out of the 1-byte range; shorten the edit");
                    buf[0x15] = (byte)updated;
                }
            }

            for (int i = 0; i < numObjects; i++)
            {
                ShiftIfAfter(buf, obim + i * 2, editOffset, delta);
                ShiftIfAfter(buf, obcd + i * 2, editOffset, delta);
            }
            // v2 has no room-local-script table.
        }

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
                if (off == 0xFFFF || off <= editOffset) continue;
                int updated = off + delta;
                dir.Offsets[i] = updated;
                WriteU16(raw, dir.OffsetArrayPosition + i * 2, updated);
            }
        }

        private static void ShiftIfAfter(byte[] buf, int fieldPos, int editOffset, int delta)
        {
            if (fieldPos + 1 >= buf.Length) return;
            int value = ReadU16(buf, fieldPos);
            if (value > editOffset) WriteU16(buf, fieldPos, value + delta);
        }

        private static int ReadU16(byte[] data, int p) { return data[p] | (data[p + 1] << 8); }

        private static void WriteU16(byte[] data, int p, int value)
        {
            data[p] = (byte)(value & 0xFF);
            data[p + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
