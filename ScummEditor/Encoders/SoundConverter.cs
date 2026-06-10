using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Helpers to turn a SCUMM sound leaf payload into something exportable / playable:
    ///   - Standard MIDI (located by its "MThd" signature) for MIDI/GMD streams.
    ///   - WAV (decoded from a Creative VOC) for digital sound effects.
    /// Anything else is exported as a raw .bin (e.g. AdLib / Roland data, which need an
    /// external OPL2 / MT-32 emulator to play).
    /// </summary>
    public static class SoundConverter
    {
        public enum SoundKind
        {
            Unknown,
            StandardMidi,
            Voc
        }

        private static readonly byte[] MThd = { (byte)'M', (byte)'T', (byte)'h', (byte)'d' };
        private const string VocSignature = "Creative Voice File\x1A";

        public static SoundKind Classify(byte[] data)
        {
            if (data == null || data.Length == 0) return SoundKind.Unknown;
            if (FindSignature(data, MThd) >= 0) return SoundKind.StandardMidi;
            if (StartsWith(data, VocSignature)) return SoundKind.Voc;
            return SoundKind.Unknown;
        }

        public static string SuggestExtension(byte[] data)
        {
            switch (Classify(data))
            {
                case SoundKind.StandardMidi: return ".mid";
                case SoundKind.Voc: return ".voc";
                default: return ".bin";
            }
        }

        /// <summary>Returns the embedded Standard MIDI bytes (from "MThd"), or null.</summary>
        public static byte[] ExtractMidi(byte[] data)
        {
            int start = FindSignature(data, MThd);
            if (start < 0) return null;

            var midi = new byte[data.Length - start];
            Array.Copy(data, start, midi, 0, midi.Length);
            return midi;
        }

        /// <summary>
        /// Decodes a Creative VOC payload to a 8-bit/16-bit PCM WAV. Returns null if the
        /// data is not a supported VOC (e.g. ADPCM-compressed), in which case the caller
        /// should fall back to exporting the raw bytes.
        /// </summary>
        public static byte[] VocToWav(byte[] data)
        {
            if (!StartsWith(data, VocSignature) || data.Length < 26) return null;

            int dataBlockOffset = data[20] | (data[21] << 8); // header size / offset to first block
            if (dataBlockOffset <= 0 || dataBlockOffset >= data.Length) dataBlockOffset = 26;

            var pcm = new List<byte>();
            int sampleRate = 11025;
            int bitsPerSample = 8;
            int channels = 1;
            bool gotAudio = false;

            int p = dataBlockOffset;
            while (p < data.Length)
            {
                byte blockType = data[p++];
                if (blockType == 0x00) break; // terminator

                if (p + 3 > data.Length) break;
                int blockLength = data[p] | (data[p + 1] << 8) | (data[p + 2] << 16);
                p += 3;
                if (p + blockLength > data.Length) break;

                int bodyStart = p;

                switch (blockType)
                {
                    case 1: // sound data
                    {
                        if (blockLength < 2) return null;
                        byte timeConstant = data[bodyStart];
                        byte codec = data[bodyStart + 1];
                        if (codec != 0) return null; // only unsigned 8-bit PCM is supported
                        sampleRate = 1000000 / (256 - timeConstant);
                        bitsPerSample = 8;
                        channels = 1;
                        for (int i = bodyStart + 2; i < bodyStart + blockLength; i++) pcm.Add(data[i]);
                        gotAudio = true;
                        break;
                    }
                    case 9: // new sound data (VOC 1.20+)
                    {
                        if (blockLength < 12) return null;
                        sampleRate = data[bodyStart] | (data[bodyStart + 1] << 8) | (data[bodyStart + 2] << 16) | (data[bodyStart + 3] << 24);
                        bitsPerSample = data[bodyStart + 4];
                        channels = data[bodyStart + 5];
                        int codec = data[bodyStart + 6] | (data[bodyStart + 7] << 8);
                        if (codec != 0 || (bitsPerSample != 8 && bitsPerSample != 16)) return null;
                        for (int i = bodyStart + 12; i < bodyStart + blockLength; i++) pcm.Add(data[i]);
                        gotAudio = true;
                        break;
                    }
                    case 2: // continuation of previous sound data
                        for (int i = bodyStart; i < bodyStart + blockLength; i++) pcm.Add(data[i]);
                        break;
                    // blocks 3 (silence), 4 (marker), 5 (text), 6/7 (repeat), 8 (extra info) are ignored
                }

                p += blockLength;
            }

            if (!gotAudio || pcm.Count == 0) return null;
            return BuildWav(pcm.ToArray(), sampleRate, bitsPerSample, channels);
        }

        private static byte[] BuildWav(byte[] pcm, int sampleRate, int bitsPerSample, int channels)
        {
            int blockAlign = channels * (bitsPerSample / 8);
            int byteRate = sampleRate * blockAlign;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + pcm.Length);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));

                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);                       // PCM fmt chunk size
                w.Write((short)1);                 // audio format = PCM
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(byteRate);
                w.Write((short)blockAlign);
                w.Write((short)bitsPerSample);

                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(pcm.Length);
                w.Write(pcm);

                w.Flush();
                return ms.ToArray();
            }
        }

        private static bool StartsWith(byte[] data, string signature)
        {
            if (data.Length < signature.Length) return false;
            for (int i = 0; i < signature.Length; i++)
            {
                if (data[i] != (byte)signature[i]) return false;
            }
            return true;
        }

        private static int FindSignature(byte[] data, byte[] signature)
        {
            // SCUMM MIDI usually starts within the first bytes; scan a bounded window.
            int limit = Math.Min(data.Length - signature.Length, 256);
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < signature.Length; j++)
                {
                    if (data[i + j] != signature[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
