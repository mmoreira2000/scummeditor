using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    SCUMM v4 "SO" sound block (a child of the LF disk block, after the scripts/costumes). The body is
    a small-header container of WA (Roland/waveform) and AD (AdLib) sub-blocks, and SO can nest (an SO
    body may contain further SO sub-blocks). Each sub-block uses the v4 small header [size:4 LE][tag:2].

    IMPORTANT round-trip invariant: the SO block consumes EXACTLY its declared size. The ~20 KB of raw
    AdLib stream that follows a room's SO blocks is NOT part of any SO - it is a separate, header-less
    LF child that the container walk already absorbs into a RawDataBlock. So this class reads exactly
    (BlockSize - HeaderLength) bytes and writes them back verbatim; the raw-tail handling is untouched,
    keeping the container byte-for-byte identical.

    The sub-block parse is display-only and fully guarded (empty list on any malformed/garbage body, as
    for the other v4 read-only blocks). Playback is out of scope: AD/WA payloads are raw OPL2 register /
    Roland streams, not Standard MIDI or VOC, so the existing players cannot render them.
    */
    public class SoundBlockV4 : BlockBase
    {
        private const int MaxDepth = 4;

        public SoundBlockV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType { get { return "SO"; } }

        public byte[] RawContent { get; set; }
        public List<SoundSubBlockV4> SubBlocks { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));

            SubBlocks = new List<SoundSubBlockV4>();
            try { SubBlocks = ParseSubBlocks(0, RawContent.Length, 0); }
            catch { SubBlocks = new List<SoundSubBlockV4>(); }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent); // verbatim - read-only view, round-trips byte-for-byte
        }

        /// <summary>
        /// The sub-block's payload bytes: everything after its 6-byte small header. Returns an empty
        /// array if the sub-block's offset/size fall outside RawContent (malformed body).
        /// </summary>
        public byte[] GetPayload(SoundSubBlockV4 sub)
        {
            int start = sub.Offset + 6;
            int length = sub.Size - 6;
            if (start < 0 || length <= 0 || start + length > RawContent.Length) return new byte[0];

            var payload = new byte[length];
            System.Array.Copy(RawContent, start, payload, 0, length);
            return payload;
        }

        private List<SoundSubBlockV4> ParseSubBlocks(int start, int end, int depth)
        {
            var list = new List<SoundSubBlockV4>();
            if (depth > MaxDepth) return list;

            int p = start;
            while (p + 6 <= end)
            {
                int size = RawContent[p] | (RawContent[p + 1] << 8) | (RawContent[p + 2] << 16) | (RawContent[p + 3] << 24);
                string tag = Encoding.ASCII.GetString(RawContent, p + 4, 2);
                if (size < 6 || p + size > end) break; // not a plausible sub-block; stop

                var sub = new SoundSubBlockV4 { Tag = tag, Offset = p, Size = size, Kind = KindOf(tag, p) };
                if (tag == "SO")
                {
                    sub.Children = ParseSubBlocks(p + 6, p + size, depth + 1);
                }
                list.Add(sub);
                p += size;
            }
            return list;
        }

        private string KindOf(string tag, int blockStart)
        {
            if (tag == "AD")
            {
                // The payload begins with a 2-byte priority word; byte [2] is the type marker
                // (0x80 = music, anything else = a sound effect). Mirrors ScummVM convertADResource.
                int marker = blockStart + 6 + 2;
                if (marker < RawContent.Length) return RawContent[marker] == 0x80 ? "AdLib music" : "AdLib SFX";
                return "AdLib";
            }
            if (tag == "WA") return "Roland/waveform";
            if (tag == "SO") return "nested sound";
            return tag;
        }
    }
}
