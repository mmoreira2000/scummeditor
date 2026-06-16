namespace ScummEditor.Structures.DataFile
{
    /*
    val     : 1 byte
    padding : 1 byte
    */
    public class ValuePaddingBlock:BlockBase
    {
        private readonly string _blockType;

        public byte Value { get; set; }
        public byte Padding { get; set; }

        public ValuePaddingBlock(BlockBase blockBase, string blockType) : base(blockBase)
        {
            _blockType = blockType;
        }

        public override string BlockType
        {
            get { return _blockType; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += 2;
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Value = binaryReader.ReadByte1();
            Padding = binaryReader.ReadByte1();
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.WriteByte(Value);
            binaryWriter.WriteByte(Padding);
        }
    }
}