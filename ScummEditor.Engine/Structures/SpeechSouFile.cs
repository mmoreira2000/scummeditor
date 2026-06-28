using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures
{
    /*
    MONSTER.SOU / "game".SOU (FM Towns) - the speech and sound-effects container of the
    talkie editions. Verified vs ScummVM sound.cpp:572-588:

      "SOU " + uint32 (always 0)
      repeated entries:
        [optional] "VCTL" + size:32be   (lip-sync block; VTTL in the effects files; size
                                          includes the 8-byte header; body = lip-sync
                                          timestamps, 2 bytes each)
        [optional] "VTLK" + size:32be   (a wrapper around the VOC; size includes the 8-byte
                                          header; Full Throttle uses it, the older talkie
                                          editions do not)
        Creative VOC file               ("Creative Voice File\x1A" header + typed blocks,
                                          terminated by a 0x00 block)

    So an entry is, in order: an optional lip-sync block, an optional VTLK wrapper, then the
    VOC. Some entries (DOTT) start directly with the VOC. The file is parsed lazily and only
    the block headers are read (the files reach hundreds of MB); the audio bytes are loaded
    on demand per entry.
    */
    public class SpeechSouFile
    {
        // The trailing \x1A EOF marker is not checked: one entry of the Sam & Max floppy
        // effects file has it replaced by \x00.
        private const string VocSignature = "Creative Voice File";

        public string FilePath { get; private set; }
        public long FileLength { get; private set; }
        public List<SpeechSouEntry> Entries { get; private set; }
        /// <summary>Non-null when the walk stopped before the end of the file.</summary>
        public string ParseError { get; private set; }

        private bool _parsed;

        public SpeechSouFile(string filePath)
        {
            FilePath = filePath;
            Entries = new List<SpeechSouEntry>();
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

                var header = new byte[8];
                if (stream.Read(header, 0, 8) != 8 || header[0] != 'S' || header[1] != 'O' || header[2] != 'U')
                {
                    ParseError = "The file does not start with the expected \"SOU \" header.";
                    return;
                }

                while (stream.Position + 4 <= stream.Length)
                {
                    long entryOffset = stream.Position;

                    var tag = new byte[4];
                    if (stream.Read(tag, 0, 4) != 4) break;

                    var entry = new SpeechSouEntry
                    {
                        Index = Entries.Count,
                        Offset = entryOffset
                    };

                    // An entry optionally starts with a lip-sync block (VCTL in the talkie files,
                    // VTTL in the effects files). Some entries (DOTT) have none and start directly
                    // with the VOC data.
                    bool isLipSyncTag = (tag[0] == 'V' && (tag[1] == 'C' || tag[1] == 'T') && tag[2] == 'T' && tag[3] == 'L');

                    if (isLipSyncTag)
                    {
                        var sizeBytes = new byte[4];
                        if (stream.Read(sizeBytes, 0, 4) != 4) break;
                        int blockSize = ReadBE32(sizeBytes);
                        if (blockSize < 8 || entryOffset + blockSize > stream.Length)
                        {
                            ParseError = string.Format("Invalid lip-sync block size at offset 0x{0:X}.", entryOffset);
                            return;
                        }

                        entry.LipSyncCount = (blockSize - 8) / 2;
                        stream.Seek(entryOffset + blockSize, SeekOrigin.Begin);

                        // read the tag of the block that follows the lip-sync block (a VTLK wrapper or the VOC)
                        if (stream.Position + 4 > stream.Length || stream.Read(tag, 0, 4) != 4) break;
                    }

                    // The VOC may be wrapped in a VTLK block (Full Throttle) or follow directly (the older
                    // talkie editions / DOTT).
                    bool isVtlk = (tag[0] == 'V' && tag[1] == 'T' && tag[2] == 'L' && tag[3] == 'K');
                    bool isBareVoc = (tag[0] == 'C' && tag[1] == 'r' && tag[2] == 'e' && tag[3] == 'a');

                    if (isVtlk)
                    {
                        long vtlkOffset = stream.Position - 4;
                        var sizeBytes = new byte[4];
                        if (stream.Read(sizeBytes, 0, 4) != 4) break;
                        int blockSize = ReadBE32(sizeBytes);
                        if (blockSize < 8 || vtlkOffset + blockSize > stream.Length)
                        {
                            ParseError = string.Format("Invalid VTLK block size at offset 0x{0:X}.", vtlkOffset);
                            return;
                        }

                        // The VTLK size is the authoritative entry boundary - ScummVM seeks by the offsets it
                        // is given rather than walking the VOC, and Full Throttle's VOC bytes do not always
                        // walk cleanly to a 0x00 terminator. The Creative VOC begins right after the 8-byte
                        // VTLK header; the sample rate / duration are read best-effort and never abort.
                        long vtlkEnd = vtlkOffset + blockSize;
                        entry.VocOffset = stream.Position;
                        entry.VocLength = (int)(vtlkEnd - entry.VocOffset);
                        ReadVocMetadata(stream, entry, vtlkEnd);
                        stream.Seek(vtlkEnd, SeekOrigin.Begin);
                    }
                    else if (isBareVoc)
                    {
                        stream.Seek(stream.Position - 4, SeekOrigin.Begin); // rewind so the VOC walk re-reads its header
                        if (!WalkVocFile(stream, entry))
                        {
                            ParseError = string.Format("Invalid VOC data at offset 0x{0:X}.", entry.VocOffset);
                            return;
                        }
                    }
                    else
                    {
                        ParseError = string.Format(
                            "Unexpected data at offset 0x{0:X}: expected a VCTL/VTTL/VTLK block or VOC data.", stream.Position - 4);
                        return;
                    }

                    Entries.Add(entry);
                }
            }
        }

        private static int ReadBE32(byte[] b)
        {
            return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
        }

        /// <summary>
        /// Best-effort read of a VTLK-wrapped VOC's sample rate and (approximate) duration, bounded by the
        /// VTLK block's end. Never fails the parse: the VTLK size already delimits the entry, so this only
        /// fills the UI hint columns. It reads the sample rate from the first sound block (reliable) and
        /// sums PCM bytes up to the first VOC terminator or the VTLK end (whichever comes first), so a VOC
        /// whose trailing bytes do not walk to a clean terminator still yields a usable rate and duration.
        /// </summary>
        private static void ReadVocMetadata(FileStream stream, SpeechSouEntry entry, long limit)
        {
            long start = entry.VocOffset;
            var vocHeader = new byte[26];
            stream.Seek(start, SeekOrigin.Begin);
            if (stream.Read(vocHeader, 0, 26) != 26) return;
            for (int i = 0; i < VocSignature.Length; i++)
            {
                if (vocHeader[i] != (byte)VocSignature[i]) return;
            }

            int firstBlockOffset = vocHeader[20] | (vocHeader[21] << 8);
            if (firstBlockOffset < 26) firstBlockOffset = 26;
            long pos = start + firstBlockOffset;

            long pcmBytes = 0;
            var blockHeader = new byte[4];
            while (pos + 4 <= limit)
            {
                stream.Seek(pos, SeekOrigin.Begin);
                if (stream.Read(blockHeader, 0, 1) != 1) break;
                byte blockType = blockHeader[0];
                if (blockType == 0x00 || blockType > 9) break; // terminator, or no longer a valid VOC block

                if (stream.Read(blockHeader, 1, 3) != 3) break;
                int blockLength = blockHeader[1] | (blockHeader[2] << 8) | (blockHeader[3] << 16);
                long bodyStart = pos + 4;
                if (bodyStart + blockLength > limit) break;

                if (blockType == 1 && blockLength >= 2) // sound data: timeConstant + codec + samples
                {
                    int timeConstant = stream.ReadByte();
                    if (entry.SampleRate == 0 && timeConstant >= 0 && timeConstant < 256)
                    {
                        entry.SampleRate = 1000000 / (256 - timeConstant);
                    }
                    pcmBytes += blockLength - 2;
                }
                else if (blockType == 2) // continuation of the previous sound data
                {
                    pcmBytes += blockLength;
                }

                pos = bodyStart + blockLength;
            }

            if (entry.SampleRate > 0)
            {
                entry.DurationSeconds = pcmBytes / (double)entry.SampleRate;
            }
        }

        /// <summary>
        /// Walks the VOC block headers (without reading the audio bytes) to find where the
        /// embedded VOC file ends, collecting the sample rate and the duration on the way.
        /// </summary>
        private static bool WalkVocFile(FileStream stream, SpeechSouEntry entry)
        {
            entry.VocOffset = stream.Position;

            var vocHeader = new byte[26];
            if (stream.Read(vocHeader, 0, 26) != 26) return false;
            for (int i = 0; i < VocSignature.Length; i++)
            {
                if (vocHeader[i] != (byte)VocSignature[i]) return false;
            }

            int firstBlockOffset = vocHeader[20] | (vocHeader[21] << 8);
            if (firstBlockOffset < 26) firstBlockOffset = 26;
            stream.Seek(entry.VocOffset + firstBlockOffset, SeekOrigin.Begin);

            long pcmBytes = 0;
            var blockHeader = new byte[4];
            while (true)
            {
                int read = stream.Read(blockHeader, 0, 1);
                if (read != 1) return false;

                byte blockType = blockHeader[0];
                if (blockType == 0x00) break; // VOC terminator

                if (stream.Read(blockHeader, 1, 3) != 3) return false;
                int blockLength = blockHeader[1] | (blockHeader[2] << 8) | (blockHeader[3] << 16);
                long bodyStart = stream.Position;
                if (bodyStart + blockLength > stream.Length) return false;

                if (blockType == 1 && blockLength >= 2) // sound data: timeConstant + codec + samples
                {
                    int timeConstant = stream.ReadByte();
                    if (entry.SampleRate == 0 && timeConstant >= 0 && timeConstant < 256)
                    {
                        entry.SampleRate = 1000000 / (256 - timeConstant);
                    }
                    pcmBytes += blockLength - 2;
                }
                else if (blockType == 2) // continuation of the previous sound data
                {
                    pcmBytes += blockLength;
                }

                stream.Seek(bodyStart + blockLength, SeekOrigin.Begin);
            }

            entry.VocLength = (int)(stream.Position - entry.VocOffset);
            if (entry.SampleRate > 0)
            {
                entry.DurationSeconds = pcmBytes / (double)entry.SampleRate;
            }
            return true;
        }

        /// <summary>Reads the raw Creative VOC bytes of one entry (loaded on demand).</summary>
        public byte[] ReadVocBytes(SpeechSouEntry entry)
        {
            using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var data = new byte[entry.VocLength];
                stream.Seek(entry.VocOffset, SeekOrigin.Begin);
                int total = 0;
                while (total < data.Length)
                {
                    int read = stream.Read(data, total, data.Length - total);
                    if (read <= 0) break;
                    total += read;
                }
                return data;
            }
        }
    }
}
