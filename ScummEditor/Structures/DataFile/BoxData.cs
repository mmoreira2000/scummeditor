using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /*
    BOXD - Box data (walk boxes used for pathfinding). SCUMM 6 layout:

      num box : 16le (ScummVM only uses the lower 8 bits)
      boxes (num box times):
        ulx   : 16le (signed)   upper-left  x
        uly   : 16le (signed)   upper-left  y
        urx   : 16le (signed)   upper-right x
        ury   : 16le (signed)   upper-right y
        lrx   : 16le (signed)   lower-right x
        lry   : 16le (signed)   lower-right y
        llx   : 16le (signed)   lower-left  x
        lly   : 16le (signed)   lower-left  y
        mask  : 8               z-plane that masks this box
        flags : 8               0x08 X flip, 0x10 Y flip, 0x20 ignore scale,
                                0x40 locked, 0x80 invisible
        scale : 16le            box scale (or scale slot if the high bit is set)

    NOTE: this is a read-only decode. The original bytes are kept and written back
    verbatim on save, so rebuilding the game file is always byte-identical even when
    the parsed view is incomplete or the layout differs between game versions.
    */
    public class BoxData : BlockBase
    {
        public BoxData(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "BOXD"; }
        }

        public byte[] RawContent { get; set; }

        public ushort NumBoxes { get; set; }
        public List<Box> Boxes { get; set; }

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
            Boxes = new List<Box>();
            if (RawContent.Length < 2) return;

            int p = 0;
            NumBoxes = ReadUInt16(p);
            p += 2;

            const int boxSize = 20; // 8 * int16 + 2 * byte + 1 * uint16
            for (int i = 0; i < NumBoxes && (p + boxSize) <= RawContent.Length; i++)
            {
                var box = new Box
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
                };
                Boxes.Add(box);
                p += boxSize;
            }
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

    public class Box
    {
        public short Ulx { get; set; }
        public short Uly { get; set; }
        public short Urx { get; set; }
        public short Ury { get; set; }
        public short Lrx { get; set; }
        public short Lry { get; set; }
        public short Llx { get; set; }
        public short Lly { get; set; }
        public byte Mask { get; set; }
        public byte Flags { get; set; }
        public ushort Scale { get; set; }
    }
}
