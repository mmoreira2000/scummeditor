using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Structures
{
    /// <summary>One CD audio track inside a CDDA.SOU container.</summary>
    public class CdAudioTrack
    {
        public int Number { get; set; }
        public long Offset { get; set; }
        public long ByteLength { get; set; }
        /// <summary>Absolute start frame on the original CD (75 frames per second).</summary>
        public int StartFrame { get; set; }
        public int FrameCount { get; set; }
        public double DurationSeconds { get; set; }
    }

    /*
    CDDA.SOU - ripped CD audio container shipped by digital releases of the CD games
    (e.g. The Secret of Monkey Island CD):

      0x000  " GSA" magic + version dword
      0x010  track table, up to 98 pairs of uint32le:
               file offset of the track, absolute CD start frame (1/75 s)
             The last used pair is a sentinel pointing at the end of the file.
      0x320  raw CD audio: 44100 Hz, 16-bit signed little-endian, stereo,
             2352 bytes per CD frame.

    The track boundaries come straight from the table; each track converts to WAV by
    writing a PCM header and streaming the bytes.
    */
    public class CdAudioSouFile
    {
        private const int HeaderSize = 0x320;
        private const int BytesPerFrame = 2352;
        public const int SampleRate = 44100;

        public string FilePath { get; private set; }
        public long FileLength { get; private set; }
        public List<CdAudioTrack> Tracks { get; private set; }
        public double TotalDurationSeconds { get; private set; }
        /// <summary>Non-null when the file is not a supported CDDA.SOU variant.</summary>
        public string ParseError { get; private set; }

        private bool _parsed;

        public CdAudioSouFile(string filePath)
        {
            FilePath = filePath;
            Tracks = new List<CdAudioTrack>();
        }

        public void EnsureParsed()
        {
            if (_parsed) return;
            _parsed = true;

            try
            {
                Parse();
            }
            catch (Exception ex)
            {
                ParseError = ex.Message;
            }
        }

        private void Parse()
        {
            using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                FileLength = stream.Length;

                var header = new byte[HeaderSize];
                if (stream.Read(header, 0, HeaderSize) != HeaderSize)
                {
                    ParseError = "The file is too small to hold a CDDA.SOU header.";
                    return;
                }

                if (header[0] != ' ' || header[1] != 'G' || header[2] != 'S' || header[3] != 'A')
                {
                    ParseError = "Unsupported CDDA.SOU variant (expected the \" GSA\" table header).";
                    return;
                }

                // (file offset, CD start frame) pairs; the last one is the end-of-file sentinel.
                var offsets = new List<long>();
                var frames = new List<int>();
                for (int p = 0x10; p + 8 <= HeaderSize; p += 8)
                {
                    long offset = (uint)(header[p] | (header[p + 1] << 8) | (header[p + 2] << 16) | (header[p + 3] << 24));
                    int frame = header[p + 4] | (header[p + 5] << 8) | (header[p + 6] << 16) | (header[p + 7] << 24);
                    if (offset == 0 && offsets.Count > 0) break;
                    offsets.Add(offset);
                    frames.Add(frame);
                }

                for (int i = 0; i + 1 < offsets.Count; i++)
                {
                    var track = new CdAudioTrack
                    {
                        Number = i + 1,
                        Offset = offsets[i],
                        ByteLength = offsets[i + 1] - offsets[i],
                        StartFrame = frames[i],
                        FrameCount = frames[i + 1] - frames[i]
                    };
                    track.DurationSeconds = track.FrameCount / 75.0;
                    Tracks.Add(track);
                    TotalDurationSeconds += track.DurationSeconds;
                }

                if (Tracks.Count == 0)
                {
                    ParseError = "No tracks found in the CDDA.SOU table.";
                }
            }
        }

        /// <summary>Decodes one track to a PCM WAV file (44100 Hz, 16-bit, stereo), streaming the bytes.</summary>
        public void ExportTrackToWav(CdAudioTrack track, string outputPath)
        {
            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            {
                WriteTrackWav(track, output);
            }
        }

        /// <summary>Writes the WAV of one track into a stream (used by the in-editor player).</summary>
        public void WriteTrackWav(CdAudioTrack track, Stream output)
        {
            using (var input = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var writer = new BinaryWriter(output, Encoding.ASCII, true))
            {
                long dataLength = track.ByteLength;

                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write((uint)(36 + dataLength));
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);                          // PCM fmt chunk size
                writer.Write((short)1);                    // audio format = PCM
                writer.Write((short)2);                    // stereo
                writer.Write(SampleRate);
                writer.Write(SampleRate * 4);              // byte rate (16-bit stereo)
                writer.Write((short)4);                    // block align
                writer.Write((short)16);                   // bits per sample

                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write((uint)dataLength);

                input.Seek(track.Offset, SeekOrigin.Begin);
                var buffer = new byte[64 * 1024];
                long remaining = dataLength;
                while (remaining > 0)
                {
                    int chunk = remaining > buffer.Length ? buffer.Length : (int)remaining;
                    int read = input.Read(buffer, 0, chunk);
                    if (read <= 0) break;
                    output.Write(buffer, 0, read);
                    remaining -= read;
                }
            }
        }
    }
}
