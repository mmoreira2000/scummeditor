namespace ScummEditor.Structures.DataFile
{
    //RMIH  -   Room Image Header
    //  num z buf: 2 bytes (number of ZPnn in the IM00 block)
    //
    //Contains the number of z-buffers
    public class RoomImageHeader : BlockBase
    {
        public RoomImageHeader(BlockBase blockBase) : base(blockBase) { }

        public ushort NumberOfZBuffers { get; set; }

        public override string BlockType
        {
            get { return "RMIH"; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            BlockSize += 2;
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            NumberOfZBuffers = binaryReader.ReadUint16();
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Write(NumberOfZBuffers);
        }
    }
}