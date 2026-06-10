using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /*
    SCAL - Scale slots. Lets actors be scaled as a function of their Y position in
    the room, instead of a fixed scale per box. Always 4 slots:

      slot (repeat 4 times):
        scale1 : 16le
        y1     : 16le
        scale2 : 16le
        y2     : 16le

    For a slot, the scale factor at an input y is the linear interpolation:
        s = ((scale2 - scale1) * (y - y1)) / (y2 - y1) + scale1   (clamped to 1..255)

    NOTE: this is a read-only decode. The original bytes are kept and written back
    verbatim on save, so rebuilding the game file is always byte-identical.
    */
    public class Scale : BlockBase
    {
        public Scale(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "SCAL"; }
        }

        public byte[] RawContent { get; set; }

        public List<ScaleSlot> Slots { get; set; }

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
            Slots = new List<ScaleSlot>();

            const int slotSize = 8; // 4 * uint16
            int p = 0;
            while ((p + slotSize) <= RawContent.Length)
            {
                Slots.Add(new ScaleSlot
                {
                    Scale1 = ReadUInt16(p + 0),
                    Y1 = ReadUInt16(p + 2),
                    Scale2 = ReadUInt16(p + 4),
                    Y2 = ReadUInt16(p + 6)
                });
                p += slotSize;
            }
        }

        private ushort ReadUInt16(int p)
        {
            return (ushort)(RawContent[p] | (RawContent[p + 1] << 8));
        }
    }

    public class ScaleSlot
    {
        public ushort Scale1 { get; set; }
        public ushort Y1 { get; set; }
        public ushort Scale2 { get; set; }
        public ushort Y2 { get; set; }
    }
}
