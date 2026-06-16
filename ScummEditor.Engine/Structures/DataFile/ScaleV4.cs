using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// SCUMM v4 "SA" block: actor scale slots (= v5 "SCAL"). Each slot is 8 bytes
    /// (scale1, y1, scale2, y2 - all LE16); a room can have an empty SA block. Read-only: the body is
    /// kept verbatim and written back unchanged, so the container round-trips byte-for-byte.
    /// </summary>
    public class ScaleV4 : BlockBase
    {
        public ScaleV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType { get { return "SA"; } }

        public byte[] RawContent { get; set; }
        public List<ScaleSlot> Slots { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));

            Slots = new List<ScaleSlot>();
            try { ParseForDisplay(); }
            catch { Slots = new List<ScaleSlot>(); }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }

        private void ParseForDisplay()
        {
            const int slotSize = 8; // scale1, y1, scale2, y2 (each LE16)
            for (int p = 0; p + slotSize <= RawContent.Length; p += slotSize)
            {
                Slots.Add(new ScaleSlot
                {
                    Scale1 = ReadUInt16(p + 0),
                    Y1 = ReadUInt16(p + 2),
                    Scale2 = ReadUInt16(p + 4),
                    Y2 = ReadUInt16(p + 6)
                });
            }
        }

        private ushort ReadUInt16(int p)
        {
            return (ushort)(RawContent[p] | (RawContent[p + 1] << 8));
        }
    }
}
