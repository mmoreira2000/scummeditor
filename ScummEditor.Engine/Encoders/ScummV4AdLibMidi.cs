using System.Collections.Generic;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Extracts a playable Standard MIDI File from a SCUMM v4 "AD" (AdLib) MUSIC payload, so the
    /// editor can preview the melody through the Windows MIDI synth. A v4 AD music resource is a
    /// 2-byte priority word, a 0x80 music marker, a small header, an 8x16 instrument table and then a
    /// plain MIDI event track (see ScummVM convertADResource). We keep the MIDI track and wrap it in a
    /// minimal MThd/MTrk with a tempo derived from the resource's "ticks" byte.
    ///
    /// This is a PREVIEW: the AdLib FM instrument definitions are dropped, so it plays with the synth's
    /// default (General MIDI) instruments, not the original OPL2 timbres. Export the raw resource to
    /// hear it faithfully in an OPL2 player. SFX (marker != 0x80) and WA/Roland resources are not
    /// convertible here and return null.
    /// </summary>
    public static class ScummV4AdLibMidi
    {
        private const int MusicMarkerOffset = 2;   // payload[2] == 0x80 for music
        private const int TicksOffset = 3;         // payload[3] = song "speed"
        private const int TrackOffset = 0x93;      // 2 priority + 0x11 header + 8*16 instrument table
        private const int Division = 480;          // PPQN (ScummVM uses a fixed 480)

        /// <summary>
        /// Returns a Standard MIDI File for an AD music payload (the bytes after the AD sub-block's
        /// 6-byte header), or null when the payload is not AdLib music or is too short.
        /// </summary>
        public static byte[] ToStandardMidi(byte[] adPayload)
        {
            if (adPayload == null || adPayload.Length <= TrackOffset)
            {
                return null;
            }
            if (adPayload[MusicMarkerOffset] != 0x80)
            {
                return null; // a sound effect, not a music track
            }

            int ticks = adPayload[TicksOffset];
            if (ticks == 0) ticks = 1;

            // Tempo (microseconds per quarter note), as ScummVM derives it for v4 AdLib music; the
            // 24-bit MIDI tempo field cannot hold more than 0xFFFFFF.
            int tempo = 500000 * 256 / ticks;
            if (tempo > 0xFFFFFF) tempo = 0xFFFFFF;
            if (tempo < 1) tempo = 1;

            int trackLength = adPayload.Length - TrackOffset;
            var track = new List<byte>(trackLength + 16);

            // Tempo meta event (delta 0): FF 51 03 tttttt
            track.Add(0x00);
            track.Add(0xFF);
            track.Add(0x51);
            track.Add(0x03);
            track.Add((byte)((tempo >> 16) & 0xFF));
            track.Add((byte)((tempo >> 8) & 0xFF));
            track.Add((byte)(tempo & 0xFF));

            // The AD track omits the first event's delta-time and starts straight with a status byte;
            // the engine plays it after a fixed delay of Division/3 ticks (ScummVM convertADResource).
            // Supply that delta so the first event is well-formed - without it the sequencer reads the
            // status byte as a delta and rejects the file.
            AddVariableLength(track, Division / 3);

            // The MIDI event stream, copied verbatim.
            for (int i = TrackOffset; i < adPayload.Length; i++)
            {
                track.Add(adPayload[i]);
            }

            // End-of-track meta (delta 0): FF 2F 00. If the stream already ends with one, this trails
            // it harmlessly (a sequencer stops at the first end-of-track).
            track.Add(0x00);
            track.Add(0xFF);
            track.Add(0x2F);
            track.Add(0x00);

            return BuildMidiFile(track);
        }

        private static byte[] BuildMidiFile(List<byte> track)
        {
            var midi = new List<byte>(track.Count + 22);

            // MThd: header length 6, format 0, 1 track, division (PPQN).
            AddAscii(midi, "MThd");
            AddUInt32BE(midi, 6);
            AddUInt16BE(midi, 0);          // format 0
            AddUInt16BE(midi, 1);          // one track
            AddUInt16BE(midi, Division);

            // MTrk: the event track.
            AddAscii(midi, "MTrk");
            AddUInt32BE(midi, (uint)track.Count);
            midi.AddRange(track);

            return midi.ToArray();
        }

        /// <summary>Writes a MIDI variable-length quantity (used for delta-times).</summary>
        private static void AddVariableLength(List<byte> output, int value)
        {
            uint v = (uint)value;
            var stack = new Stack<byte>();
            stack.Push((byte)(v & 0x7F));
            v >>= 7;
            while (v != 0)
            {
                stack.Push((byte)((v & 0x7F) | 0x80));
                v >>= 7;
            }
            while (stack.Count > 0) output.Add(stack.Pop());
        }

        private static void AddAscii(List<byte> output, string text)
        {
            foreach (char c in text) output.Add((byte)c);
        }

        private static void AddUInt32BE(List<byte> output, uint value)
        {
            output.Add((byte)((value >> 24) & 0xFF));
            output.Add((byte)((value >> 16) & 0xFF));
            output.Add((byte)((value >> 8) & 0xFF));
            output.Add((byte)(value & 0xFF));
        }

        private static void AddUInt16BE(List<byte> output, int value)
        {
            output.Add((byte)((value >> 8) & 0xFF));
            output.Add((byte)(value & 0xFF));
        }
    }
}
