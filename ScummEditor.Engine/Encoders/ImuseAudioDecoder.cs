using System.IO;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes a SCUMM v7 in-container iMUSE digital-audio resource (the body of a v7 SOUN block in The Dig)
    /// to a PCM WAV, for the sound viewer's preview/export. Layout (verified vs ScummVM imuse_digi):
    ///   iMUS -> 'MAP ' { 'FRMT' [+8 dataOffset][+12 endian][+16 wordSize][+20 sampleRate][+24 channels], all
    ///   big-endian uint32; plus REGN/JUMP/SYNC/TEXT markers } -> 'DATA' [size:uint32 BE] + raw PCM.
    /// The in-container DATA is RAW PCM (no VIMA/ADPCM - that only wraps iMUS inside the external .BUN
    /// bundles). wordSize is 8 (unsigned) or 12 (packed) for The Dig; 16-bit is handled defensively.
    /// The export uses the natural full-scale conversion, not ScummVM's internal (volume-normalised) mixer.
    /// </summary>
    public static class ImuseAudioDecoder
    {
        /// <summary>Format info read from the iMUS FRMT chunk (null when there is no FRMT/iMUS).</summary>
        public class ImuseInfo
        {
            public int WordSize;
            public int SampleRate;
            public int Channels;
            public int DataLength;
        }

        /// <summary>True when the bytes are (or contain near the start) an iMUS resource.</summary>
        public static bool IsImus(byte[] sounBytes)
        {
            if (sounBytes == null) return false;
            int p = Find(sounBytes, "iMUS", 0, 16);
            return p >= 0;
        }

        /// <summary>Reads the iMUS FRMT format fields, or null when the resource has no FRMT.</summary>
        public static ImuseInfo GetInfo(byte[] sounBytes)
        {
            int frmt = Find(sounBytes, "FRMT", 0, sounBytes != null ? sounBytes.Length : 0);
            if (frmt < 0 || frmt + 28 > sounBytes.Length) return null;

            var info = new ImuseInfo
            {
                WordSize = ReadBE(sounBytes, frmt + 16),
                SampleRate = ReadBE(sounBytes, frmt + 20),
                Channels = ReadBE(sounBytes, frmt + 24),
            };
            int data = Find(sounBytes, "DATA", 0, sounBytes.Length);
            info.DataLength = data >= 0 && data + 8 <= sounBytes.Length ? ReadBE(sounBytes, data + 4) : 0;
            return info;
        }

        /// <summary>
        /// Decodes the iMUS resource to a PCM WAV (8-bit unsigned, or 12/16-bit -> 16-bit signed), or null
        /// when it is not a decodable iMUS (no FRMT/DATA, or an unsupported word size).
        /// </summary>
        public static byte[] ToWav(byte[] sounBytes)
        {
            ImuseInfo info = GetInfo(sounBytes);
            if (info == null || info.Channels < 1 || info.Channels > 2 || info.SampleRate <= 0) return null;

            int data = Find(sounBytes, "DATA", 0, sounBytes.Length);
            if (data < 0 || data + 8 > sounBytes.Length) return null;
            int dataSize = ReadBE(sounBytes, data + 4);
            int dataStart = data + 8;
            if (dataSize <= 0 || dataStart >= sounBytes.Length) return null;
            if (dataStart + dataSize > sounBytes.Length) dataSize = sounBytes.Length - dataStart;

            byte[] pcm;
            int bits;
            if (info.WordSize == 8)
            {
                pcm = new byte[dataSize];
                System.Array.Copy(sounBytes, dataStart, pcm, 0, dataSize); // iMUSE 8-bit is unsigned = WAV 8-bit
                bits = 8;
            }
            else if (info.WordSize == 12)
            {
                pcm = Decode12BitTo16(sounBytes, dataStart, dataSize);
                bits = 16;
            }
            else if (info.WordSize == 16)
            {
                pcm = new byte[dataSize & ~1];
                System.Array.Copy(sounBytes, dataStart, pcm, 0, pcm.Length); // assume signed 16-bit LE
                bits = 16;
            }
            else
            {
                return null;
            }

            return BuildWav(pcm, info.SampleRate, info.Channels, bits);
        }

        /// <summary>
        /// Unpacks iMUSE 12-bit PCM (2 samples per 3 bytes) to signed 16-bit LE. Each 12-bit value is
        /// unsigned 0..4095 centred at 2048 (matching dimuse_internalmixer mixBits12Mono's index math); the
        /// natural full-scale 16-bit sample is (v - 2048) &lt;&lt; 4.
        /// </summary>
        private static byte[] Decode12BitTo16(byte[] src, int offset, int length)
        {
            int triples = length / 3;
            var outBytes = new byte[triples * 4]; // 2 samples * 2 bytes per 3 input bytes
            int o = 0;
            for (int i = 0; i < triples; i++)
            {
                int b0 = src[offset + i * 3];
                int b1 = src[offset + i * 3 + 1];
                int b2 = src[offset + i * 3 + 2];

                int v0 = b0 | ((b1 & 0x0F) << 8);
                int v1 = b2 | ((b1 & 0xF0) << 4);

                short s0 = (short)((v0 - 2048) << 4);
                short s1 = (short)((v1 - 2048) << 4);

                outBytes[o++] = (byte)(s0 & 0xFF);
                outBytes[o++] = (byte)((s0 >> 8) & 0xFF);
                outBytes[o++] = (byte)(s1 & 0xFF);
                outBytes[o++] = (byte)((s1 >> 8) & 0xFF);
            }
            return outBytes;
        }

        /// <summary>Wraps raw PCM in a canonical RIFF/WAVE header (little-endian).</summary>
        public static byte[] BuildWav(byte[] pcm, int sampleRate, int channels, int bitsPerSample)
        {
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;

            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + pcm.Length);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);                          // fmt chunk size
                w.Write((short)1);                    // PCM
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(byteRate);
                w.Write((short)blockAlign);
                w.Write((short)bitsPerSample);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(pcm.Length);
                w.Write(pcm);
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Index of the first occurrence of a 4-char tag in [start, start+limit), or -1.</summary>
        private static int Find(byte[] data, string tag, int start, int limit)
        {
            if (data == null) return -1;
            int end = System.Math.Min(data.Length, start + limit);
            for (int i = start; i + 4 <= end; i++)
            {
                if (data[i] == tag[0] && data[i + 1] == tag[1] && data[i + 2] == tag[2] && data[i + 3] == tag[3])
                {
                    return i;
                }
            }
            return -1;
        }

        private static int ReadBE(byte[] b, int o)
        {
            return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        }
    }
}
