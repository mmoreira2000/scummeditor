using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /// <summary>
    /// SCUMM v4 "NL" block: the room's local-object/name list (an original LucasArts table that
    /// ScummVM does not read). The body is a count byte followed by that many entry bytes. Kept
    /// verbatim and written back unchanged for a byte-for-byte round trip; a best-effort entry count
    /// and the raw entry bytes are exposed for display.
    /// </summary>
    public class LocalObjectListV4 : BlockBase
    {
        public LocalObjectListV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType { get { return "NL"; } }

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
