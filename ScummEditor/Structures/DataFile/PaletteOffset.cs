using System.Collections.Generic;

namespace ScummEditor.Structures.DataFile
{
    /*
    offset table : block size imply the number of palette. One offset per palette. ?? 
      *offset     : 4 bytes (offset starting from this OFFS block)
    */
    public class PaletteOffset : BlockBase
    {
        public PaletteOffset(BlockBase blockBase) : base(blockBase) { }

        public List<uint> Offsets { get; set; }

        public override string BlockType
        {
            get { return "OFFS"; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += ((uint)Offsets.Count * 4);
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Offsets = new List<uint>();

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) != "APAL")
            {
                Offsets.Add(binaryReader.ReadUint32());
            }
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (uint offset in Offsets)
            {
                binaryWriter.Write(offset);
            }
        }
    }
}