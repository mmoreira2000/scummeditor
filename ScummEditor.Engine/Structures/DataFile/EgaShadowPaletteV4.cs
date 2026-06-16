using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /// <summary>
    /// SCUMM v4 "SP" block: the EGA / shadow palette (= v5 "EPAL"). Always 256 bytes, each a pair of
    /// nibble-packed EGA colour indices (low nibble + high nibble). Read-only: the body is kept
    /// verbatim and written back unchanged, so the container round-trips byte-for-byte.
    /// </summary>
    public class EgaShadowPaletteV4 : BlockBase
    {
        public EgaShadowPaletteV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType { get { return "SP"; } }

        public byte[] RawContent { get; set; }

        /// <summary>Number of palette bytes (normally 256).</summary>
        public int EntryCount { get { return RawContent != null ? RawContent.Length : 0; } }

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

        /// <summary>The two EGA colour indices packed into byte <paramref name="index"/>.</summary>
        public void GetEntry(int index, out int low, out int high)
        {
            byte b = RawContent[index];
            low = b & 0x0F;
            high = (b >> 4) & 0x0F;
        }
    }
}
