using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /// <summary>
    /// SCUMM v4 "CC" block: palette colour cycling (= v5 "CYCL", but a different, fixed format). v4 is
    /// always 16 cycles of 4 bytes each (64 bytes): [delay:BE16][start:8][end:8]. A cycle is inactive
    /// when its delay is 0. This is NOT the variable-length v5 ColorCycles layout. Read-only: the body
    /// is kept verbatim and written back unchanged, so the container round-trips byte-for-byte.
    /// </summary>
    public class ColorCyclesV4 : BlockBase
    {
        public const int CycleCount = 16;

        public ColorCyclesV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType { get { return "CC"; } }

        public byte[] RawContent { get; set; }

        /// <summary>Per-cycle delay (big-endian); 0 = inactive.</summary>
        public int[] Delays { get; private set; }
        public byte[] Starts { get; private set; }
        public byte[] Ends { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));

            Delays = new int[CycleCount];
            Starts = new byte[CycleCount];
            Ends = new byte[CycleCount];
            try { ParseForDisplay(); }
            catch { /* leave zero-filled */ }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }

        private void ParseForDisplay()
        {
            for (int i = 0; i < CycleCount && (i * 4 + 4) <= RawContent.Length; i++)
            {
                int p = i * 4;
                Delays[i] = (RawContent[p] << 8) | RawContent[p + 1]; // big-endian
                Starts[i] = RawContent[p + 2];
                Ends[i] = RawContent[p + 3];
            }
        }
    }
}
