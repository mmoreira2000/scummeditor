namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// Typed view of a SCUMM v2 room (the first chunk of a Maniac Mansion / Zak McKracken NN.LFL). Like
    /// the v3 old-bundle room it is a fixed binary struct (ScummVM ScummEngine_v2 room layout), but the
    /// field offsets are shifted by one byte from v3old: the object table is at room+28 (not +29), the
    /// exit/entry scripts at +0x18/+0x1A (not +0x19/+0x1B), and the sound/script counts at +22/+23 (not
    /// +23/+24). v2 has NO room-local-script table (the room references global scripts by id); its only
    /// in-room scripts are the exit (EXCD) and entry (ENCD) scripts plus each object's verb code.
    ///
    /// Layout (offsets from the room/file start; the room is chunk 0 at offset 0):
    ///   +0    uint16  chunk size
    ///   +4    uint16  width (pixels)
    ///   +6    uint16  height (pixels)
    ///   +0x0A uint16  IM00 offset (background image)
    ///   +0x15 uint8   box-data offset (a single byte in v2)
    ///   +0x18 uint16  EXCD (exit script) offset
    ///   +0x1A uint16  ENCD (entry script) offset
    ///   +20   uint8   number of objects
    ///   +22   uint8   number of sounds
    ///   +23   uint8   number of scripts
    ///   +28   ...     numObjects x uint16 OBIM offsets, then numObjects x uint16 OBCD offsets
    /// Object code header (base = OBCDoffset - 2): obj_nr u16 @base+6, x @base+9 (x8), width @base+11 (x8),
    /// height @base+15 (&0xF8). Object NAME pointer byte @OBCDoffset+14; verb table @OBCDoffset+15 as
    /// [verbId:u8][offset:u8]* terminated by verbId==0 (offsets are OBCD-relative, a single byte).
    /// </summary>
    public class ScummV2Room
    {
        private readonly byte[] _data;

        public ScummV2Room(byte[] roomFileBytes)
        {
            _data = roomFileBytes;
        }

        public byte[] Data { get { return _data; } }

        public int Width { get { return ReadU16(4); } }
        public int Height { get { return ReadU16(6); } }
        public int ImageOffset { get { return ReadU16(0x0A); } }
        public int BoxOffset { get { return _data.Length > 0x15 ? _data[0x15] : 0; } }
        public int ExitScriptOffset { get { return ReadU16(0x18); } }
        public int EntryScriptOffset { get { return ReadU16(0x1A); } }
        public int NumObjects { get { return _data.Length > 20 ? _data[20] : 0; } }
        public int NumSounds { get { return _data.Length > 22 ? _data[22] : 0; } }
        public int NumScripts { get { return _data.Length > 23 ? _data[23] : 0; } }

        /// <summary>OBIM (object image) offset for object index i, or 0 when out of range.</summary>
        public int ObjectImageOffset(int i)
        {
            if (i < 0 || i >= NumObjects) return 0;
            return ReadU16(28 + i * 2);
        }

        /// <summary>OBCD (object code) offset for object index i, or 0 when out of range.</summary>
        public int ObjectCodeOffset(int i)
        {
            if (i < 0 || i >= NumObjects) return 0;
            return ReadU16(28 + NumObjects * 2 + i * 2);
        }

        // Object metadata in the OBCD block (base = OBCDoffset - 2 for GF_OLD_BUNDLE).
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
            int p = ObjectCodeOffset(i) - 2 + 15;
            return (p >= 0 && p < _data.Length) ? (_data[p] & 0xF8) : 0;
        }

        /// <summary>The OBCD-relative byte that points at the object's name string (@OBCDoffset+14), or 0.</summary>
        public int ObjectNameRelativeOffset(int i)
        {
            int objptr = ObjectCodeOffset(i);
            int p = objptr + 14;
            return (objptr > 0 && p < _data.Length) ? _data[p] : 0;
        }

        private int ReadU16(int p)
        {
            if (p < 0 || p + 1 >= _data.Length) return 0;
            return _data[p] | (_data[p + 1] << 8);
        }
    }
}
