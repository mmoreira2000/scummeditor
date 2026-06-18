namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    A SCUMM v3 "old bundle" sound (Loom EGA, Indy3 EGA), located by the index SOUND directory's
    (roomNumber, offset). Unlike v4 (a tagged SO/WA/AD tree) the resource is TAGLESS, mirroring ScummVM
    readSoundResourceSmallHeader's GF_OLD_BUNDLE branch (sound.cpp:2078-2088):

        offset            wa_size : uint16 LE (the Roland/waveform chunk, incl. this size word)
        offset + wa_size  ad_size : uint16 LE (the AdLib chunk, incl. its own size word)
        ad_offs + 4       AdLib payload : 2-byte priority word, then [+2] the 0x80 music marker, then the
                          instrument table + note stream - exactly the bytes ScummV4AdLibMidi.ToStandardMidi
                          expects (the on-disk resource header is 4 bytes).

    Read-only view over the room bytes; the AdLib payload is exposed for MIDI conversion/export, and the
    whole resource for a raw dump.
    */
    public class ScummV3OldSound
    {
        private readonly byte[] _data;
        private readonly int _offset;

        public ScummV3OldSound(byte[] roomData, int offset)
        {
            _data = roomData;
            _offset = offset;
            Parse();
        }

        /// <summary>Total resource length (WA + AD), in bytes.</summary>
        public int TotalSize { get; private set; }
        /// <summary>Offset of the AdLib (AD) chunk within the room bytes, or -1 when there is none.</summary>
        public int AdLibOffset { get; private set; } = -1;
        /// <summary>AdLib chunk length (incl. its size word).</summary>
        public int AdLibSize { get; private set; }
        /// <summary>True when the AdLib payload is a music track (marker byte 0x80), not a sound effect.</summary>
        public bool IsMusic { get; private set; }

        private void Parse()
        {
            if (_offset < 0 || _offset + 2 > _data.Length) return;
            int waSize = ReadU16(_offset);
            if (waSize < 2 || _offset + waSize + 2 > _data.Length) { TotalSize = waSize; return; }

            int adOffs = _offset + waSize;
            int adSize = ReadU16(adOffs);
            if (adSize >= 4 && adOffs + adSize <= _data.Length)
            {
                AdLibOffset = adOffs;
                AdLibSize = adSize;
                int marker = adOffs + 6; // priority word @+4..+5, marker @+6
                IsMusic = marker < _data.Length && _data[marker] == 0x80;
            }
            TotalSize = waSize + (AdLibOffset >= 0 ? adSize : 0);
        }

        /// <summary>The AdLib payload bytes (priority word onward) for ScummV4AdLibMidi.ToStandardMidi, or null.</summary>
        public byte[] GetAdLibPayload()
        {
            if (AdLibOffset < 0) return null;
            int start = AdLibOffset + 4;
            int length = AdLibSize - 4;
            if (start < 0 || length <= 0 || start + length > _data.Length) return null;
            var payload = new byte[length];
            System.Array.Copy(_data, start, payload, 0, length);
            return payload;
        }

        private int ReadU16(int p)
        {
            return (p >= 0 && p + 1 < _data.Length) ? _data[p] | (_data[p + 1] << 8) : 0;
        }
    }
}
