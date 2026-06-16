using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /// <summary>
    /// SCUMM v4 "SL" block: the room's sound list (an original LucasArts table that ScummVM does not
    /// read). The body is small (often a single count byte). Kept verbatim and written back unchanged
    /// for a byte-for-byte round trip; only a best-effort entry count is exposed for display.
    /// </summary>
    public class SoundListV4 : BlockBase
    {
        public SoundListV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType { get { return "SL"; } }

        public byte[] RawContent { get; set; }

        /// <summary>Best-effort entry count (the first body byte), 0 when empty.</summary>
        public int EntryCount { get { return (RawContent != null && RawContent.Length > 0) ? RawContent[0] : 0; } }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }
    }
}
