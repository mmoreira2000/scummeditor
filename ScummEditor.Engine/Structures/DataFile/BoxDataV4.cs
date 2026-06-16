using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /// <summary>
    /// SCUMM v4 "BX" block: walk boxes (= v5 "BOXD") plus the box matrix appended in the same block
    /// (v5/v6 split these into separate BOXD and BOXM). Layout: [numBoxes:1][numBoxes x 20-byte Box]
    /// [box-matrix bytes...]. Each box is 8 x int16 corners + mask:8 + flags:8 + scale:16le. Read-only:
    /// the body is kept verbatim and written back unchanged, so the container round-trips byte-for-byte.
    /// </summary>
    public class BoxDataV4 : BlockBase
    {
        public BoxDataV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType { get { return "BX"; } }

        public byte[] RawContent { get; set; }
        public int NumBoxes { get; private set; }
        public List<Box> Boxes { get; private set; }
        /// <summary>Length of the box-matrix region that follows the boxes (the v5 BOXM, appended here).</summary>
        public int MatrixLength { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));

            Boxes = new List<Box>();
            try { ParseForDisplay(); }
            catch { Boxes = new List<Box>(); }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }

        private void ParseForDisplay()
        {
            if (RawContent.Length < 1) return;

            NumBoxes = RawContent[0]; // v4 uses a single count byte (v5 uses LE16)
            int p = 1;
            const int boxSize = 20;
            for (int i = 0; i < NumBoxes && (p + boxSize) <= RawContent.Length; i++)
            {
                Boxes.Add(new Box
                {
                    Ulx = ReadInt16(p + 0),
                    Uly = ReadInt16(p + 2),
                    Urx = ReadInt16(p + 4),
                    Ury = ReadInt16(p + 6),
                    Lrx = ReadInt16(p + 8),
                    Lry = ReadInt16(p + 10),
                    Llx = ReadInt16(p + 12),
                    Lly = ReadInt16(p + 14),
                    Mask = RawContent[p + 16],
                    Flags = RawContent[p + 17],
                    Scale = ReadUInt16(p + 18)
                });
                p += boxSize;
            }

            MatrixLength = RawContent.Length - p; // the appended box-matrix region
        }

        private short ReadInt16(int p)
        {
            return (short)(RawContent[p] | (RawContent[p + 1] << 8));
        }

        private ushort ReadUInt16(int p)
        {
            return (ushort)(RawContent[p] | (RawContent[p + 1] << 8));
        }
    }
}
