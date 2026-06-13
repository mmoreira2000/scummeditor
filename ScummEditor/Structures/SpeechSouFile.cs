using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures
{
    /// <summary>One speech/effect line inside a .SOU container.</summary>
    public class SpeechSouEntry
    {
        public int Index { get; set; }
        /// <summary>Offset of the VCTL tag in the file (the offset the game scripts reference).</summary>
        public long Offset { get; set; }
        /// <summary>Number of lip-sync timestamps in the VCTL block.</summary>
        public int LipSyncCount { get; set; }
        /// <summary>Offset/length of the embedded Creative VOC file.</summary>
        public long VocOffset { get; set; }
        public int VocLength { get; set; }
        public int SampleRate { get; set; }
        public double DurationSeconds { get; set; }
    }

    /*
    MONSTER.SOU / "game".SOU (FM Towns) - the speech and sound-effects container of the
    talkie editions:

      "SOU " + uint32 (always 0)
      repeated entries:
        "VCTL" + size:32be       (size includes the 8-byte header; body = lip-sync
                                   timestamps, 2 bytes each)
        Creative VOC file         ("Creative Voice File\x1A" header + typed blocks,
                                   terminated by a 0x00 block)

    The file is parsed lazily and only the block headers are read (the files reach
    hundreds of MB); the audio bytes are loaded on demand per entry.
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

                    // Most entries start with a lip-sync block: VCTL in the talkie files,
                    // VTTL in the sound-effects files. Some entries (seen in DOTT) have no
                    // lip-sync block at all and start directly with the Creative VOC data.
                    bool isLipSyncTag = (tag[0] == 'V' && (tag[1] == 'C' || tag[1] == 'T') && tag[2] == 'T' && tag[3] == 'L');
                    bool isBareVoc = (tag[0] == 'C' && tag[1] == 'r' && tag[2] == 'e' && tag[3] == 'a');

                    if (isLipSyncTag)
                    {
                        var sizeBytes = new byte[4];
                        if (stream.Read(sizeBytes, 0, 4) != 4) break;
                        int blockSize = (sizeBytes[0] << 24) | (sizeBytes[1] << 16) | (sizeBytes[2] << 8) | sizeBytes[3];
                        if (blockSize < 8 || entryOffset + blockSize > stream.Length)
                        {
                            ParseError = string.Format("Invalid lip-sync block size at offset 0x{0:X}.", entryOffset);
                            return;
                        }

                        entry.LipSyncCount = (blockSize - 8) / 2;
                        stream.Seek(entryOffset + blockSize, SeekOrigin.Begin);
                    }
                    else if (isBareVoc)
                    {
                        stream.Seek(entryOffset, SeekOrigin.Begin); // the VOC walk re-reads its header
                    }
                    else
                    {
                        ParseError = string.Format(
                            "Unexpected data at offset 0x{0:X}: expected a VCTL/VTTL block or VOC data.", entryOffset);
                        return;
                    }

                    if (!WalkVocFile(stream, entry))
                    {
                        ParseError = string.Format("Invalid VOC data at offset 0x{0:X}.", entry.VocOffset);
                        return;
                    }

                    Entries.Add(entry);
                }
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
