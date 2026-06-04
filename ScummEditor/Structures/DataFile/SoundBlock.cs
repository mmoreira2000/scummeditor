using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Structures.DataFile
{
    /*
    SOUN - Sound resource. SCUMM 6 stores iMUSE music as several device-specific
    versions of the same track, wrapped in nested type+size(BE)+data blocks:

      SOUN
        MIDI            general MIDI / MPU-401 stream (may appear directly)
        SOU             container for the device versions
          ROL           Roland MT-32 data
          ADL           AdLib (OPL2 FM) data
          GMD           General MIDI data
          MIDI          MIDI data
          SPK           PC speaker
          SBL           digital sound effect (Creative VOC), itself a container
            AUhd / AUdt

    This is a read-only decode: the original bytes are kept and written back
    verbatim on save, so rebuilding the game file is always byte-identical. The
    sub-resources are flattened into a list for the viewer/export/playback code.
    */
    public class SoundBlock : BlockBase
    {
        // The device-version tags that mark the start of a playable sub-resource inside a SOUN.
        // We locate these by signature rather than by the declared block sizes: iMUSE sound
        // sub-blocks do not tile cleanly (a ROL/ADL block's size excludes the trailing MIDI
        // end-of-track bytes), so trusting the sizes leaves the rest of the block unparsed.
        private static readonly string[] DeviceTags =
            { "ROL ", "ADL ", "GMD ", "MIDI", "SPK ", "SBL ", "MAC " };

        public SoundBlock(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "SOUN"; }
        }

        public byte[] RawContent { get; set; }

        public List<SoundResource> Resources { get; set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - 8));
            ParseForDisplay();
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }

        private void ParseForDisplay()
        {
            Resources = new List<SoundResource>();

            // Collect the offset of every device tag, in order of appearance.
            var starts = new List<int>();
            for (int i = 0; i + 8 <= RawContent.Length; i++)
            {
                if (IsDeviceTagAt(i)) starts.Add(i);
            }

            if (starts.Count == 0)
            {
                // No recognised device block - treat the whole thing as one resource so the
                // viewer/player can still classify it (e.g. a bare MIDI stream).
                AddResource("(raw)", 0, RawContent.Length);
                return;
            }

            // Each device spans from its tag (payload after the 8-byte header) to the next tag.
            for (int i = 0; i < starts.Count; i++)
            {
                int tagStart = starts[i];
                int regionEnd = (i + 1 < starts.Count) ? starts[i + 1] : RawContent.Length;
                int bodyStart = tagStart + 8;
                if (bodyStart > regionEnd) bodyStart = regionEnd;

                AddResource(ReadType(tagStart).Trim(), bodyStart, regionEnd - bodyStart);
            }
        }

        private bool IsDeviceTagAt(int p)
        {
            string tag = ReadType(p);
            foreach (string deviceTag in DeviceTags)
            {
                if (tag == deviceTag) return true;
            }
            return false;
        }

        private void AddResource(string type, int bodyStart, int bodyLength)
        {
            if (bodyLength < 0) bodyLength = 0;
            var data = new byte[bodyLength];
            System.Array.Copy(RawContent, bodyStart, data, 0, bodyLength);

            Resources.Add(new SoundResource
            {
                Type = type,
                Path = type,
                Offset = bodyStart,
                Data = data
            });
        }

        private string ReadType(int p)
        {
            return Encoding.ASCII.GetString(RawContent, p, 4);
        }
    }

    public class SoundResource
    {
        /// <summary>Leaf block tag, trimmed (e.g. "ADL", "GMD", "MIDI", "SBL", "AUdt").</summary>
        public string Type { get; set; }

        /// <summary>Path from the SOUN root, e.g. "SOU/ADL".</summary>
        public string Path { get; set; }

        /// <summary>Offset of the payload inside the SOUN block content (debug aid).</summary>
        public int Offset { get; set; }

        /// <summary>The leaf payload bytes (without the 8-byte type+size header).</summary>
        public byte[] Data { get; set; }
    }
}
