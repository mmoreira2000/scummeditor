namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// Typed view of a SCUMM v3 "old bundle" room (the first chunk of a Loom-EGA / Indy3-EGA NN.LFL).
    /// The room is a fixed binary struct (ScummVM ScummEngine_v3old::setupRoomSubBlocks): there are no
    /// HD/BM/OI blocks - the fields sit at hard offsets from the room start, and the background image,
    /// box data and object tables are reached by the offsets stored there. The room is chunk 0 of the
    /// file, so all offsets are relative to the file start (offset 0).
    ///
    /// Layout (offsets from the room/file start):
    ///   +0  uint16  chunk size (the [size] word of the untagged chunk)
    ///   +4  uint16  width
    ///   +6  uint16  height
    ///   +0x0A uint16 IM00 offset (start of the EGA background strip table)
    ///   +0x15 uint16 BOXD offset
    ///   +0x19 uint16 EXCD (exit script) offset
    ///   +0x1B uint16 ENCD (entry script) offset
    ///   +20  uint8   number of objects
    ///   +23  uint8   number of sounds
    ///   +24  uint8   number of scripts
    ///   +29  ...     numObjects x uint16 OBIM offsets, then numObjects x uint16 OBCD offsets
    /// </summary>
    public class ScummV3OldRoom
    {
        private readonly byte[] _data;

        /// <summary>Wraps the room file bytes (the room is chunk 0 at offset 0).</summary>
        public ScummV3OldRoom(byte[] roomFileBytes)
        {
            _data = roomFileBytes;
        }

        public byte[] Data { get { return _data; } }

        public int Width { get { return ReadU16(4); } }
        public int Height { get { return ReadU16(6); } }
        public int ImageOffset { get { return ReadU16(0x0A); } }
        public int BoxOffset { get { return ReadU16(0x15); } }
        public int ExitScriptOffset { get { return ReadU16(0x19); } }
        public int EntryScriptOffset { get { return ReadU16(0x1B); } }
        // Bounds-guarded like the v2 sibling (ScummV2Room) so a truncated/corrupt room file yields 0
        // counts instead of throwing IndexOutOfRangeException up the eager tree-build / game-load path.
        public int NumObjects { get { return _data != null && _data.Length > 20 ? _data[20] : 0; } }
        public int NumSounds { get { return _data != null && _data.Length > 23 ? _data[23] : 0; } }
        public int NumScripts { get { return _data != null && _data.Length > 24 ? _data[24] : 0; } }

        /// <summary>OBIM (object image) offset for object index i (0-based), or 0 when out of range.</summary>
        public int ObjectImageOffset(int i)
        {
            int tableStart = 29;
            if (i < 0 || i >= NumObjects)
            {
                return 0;
            }
            return ReadU16(tableStart + i * 2);
        }

        /// <summary>OBCD (object code) offset for object index i (0-based), or 0 when out of range.</summary>
        public int ObjectCodeOffset(int i)
        {
            int tableStart = 29 + NumObjects * 2;
            if (i < 0 || i >= NumObjects)
            {
                return 0;
            }
            return ReadU16(tableStart + i * 2);
        }

        // Object metadata lives in the OBCD block. For GF_OLD_BUNDLE the fields are read at
        // (OBCDoffset - 2): id at +6, width byte at +11 (x8), height byte at +17 (& 0xF8).
        public int ObjectId(int i)
        {
            return ReadU16(ObjectCodeOffset(i) - 2 + 6);
        }

        public int ObjectWidth(int i)
        {
            int p = ObjectCodeOffset(i) - 2 + 11;
            return (p >= 0 && p < _data.Length) ? _data[p] * 8 : 0;
        }

        public int ObjectHeight(int i)
        {
            int p = ObjectCodeOffset(i) - 2 + 17;
            return (p >= 0 && p < _data.Length) ? (_data[p] & 0xF8) : 0;
        }

        /// <summary>
        /// The smallest structural offset strictly greater than <paramref name="offset"/> (the room
        /// size word @0, the background, box and script offsets, and every OBIM/OBCD), clamped to the
        /// room resource size. Used to bound a region (e.g. the background z-plane that sits between the
        /// background strips and the first object) without overrunning the next sub-resource.
        /// </summary>
        public int NextStructuralOffsetAbove(int offset)
        {
            int roomSize = ReadU16(0);
            int best = (roomSize > 0 && roomSize <= _data.Length) ? roomSize : _data.Length;
            Consider(ref best, offset, ImageOffset);
            Consider(ref best, offset, BoxOffset);
            Consider(ref best, offset, ExitScriptOffset);
            Consider(ref best, offset, EntryScriptOffset);
            for (int i = 0; i < NumObjects; i++)
            {
                Consider(ref best, offset, ObjectImageOffset(i));
                Consider(ref best, offset, ObjectCodeOffset(i));
            }
            return best;
        }

        private static void Consider(ref int best, int offset, int candidate)
        {
            if (candidate > offset && candidate < best) best = candidate;
        }

        private int ReadU16(int p)
        {
            if (p < 0 || p + 1 >= _data.Length)
            {
                return 0;
            }
            return _data[p] | (_data[p + 1] << 8);
        }
    }
}
