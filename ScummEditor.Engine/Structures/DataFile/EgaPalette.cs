using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /*
    EPAL - EGA palette (SCUMM v5 rooms only).

    256 bytes, one per VGA palette index: each byte packs the two EGA colors used to
    dither that index when the game runs in EGA mode (low nibble / high nibble).

    Read-only decode: the original bytes are kept and written back verbatim on save.
    */
    public class EgaPalette : BlockBase
    {
        public EgaPalette(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "EPAL"; }
        }

        public byte[] RawContent { get; set; }

        public int EntryCount
        {
            get { return RawContent == null ? 0 : RawContent.Length; }
        }

        /// <summary>The two packed EGA colors for one VGA palette index.</summary>
        public void GetEntry(int index, out int lowColor, out int highColor)
        {
            byte value = RawContent[index];
            lowColor = value & 0x0F;
            highColor = (value >> 4) & 0x0F;
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - 8));
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }
    }
}
