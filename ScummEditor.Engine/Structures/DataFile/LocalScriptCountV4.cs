using System.IO;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// SCUMM v4 "LC" block: the number of local scripts in the room (= v5 "NLSC"). The body is a
    /// single LE16 count matching the number of LS blocks that follow. Read-only: the body is kept
    /// verbatim and written back unchanged, so the container round-trips byte-for-byte.
    /// </summary>
    public class LocalScriptCountV4 : BlockBase
    {
        public LocalScriptCountV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType { get { return "LC"; } }

        public byte[] RawContent { get; set; }

        /// <summary>Number of local scripts declared (LE16); -1 when the body is too short.</summary>
        public int Count
        {
            get { return (RawContent != null && RawContent.Length >= 2) ? (RawContent[0] | (RawContent[1] << 8)) : -1; }
        }

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
