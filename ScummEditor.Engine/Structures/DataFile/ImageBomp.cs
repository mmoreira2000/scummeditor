namespace ScummEditor.Structures.DataFile
{
    public class ImageBomp : BlockBase
    {
        //unk     : 16
        //width   : 16le
        //height  : 16le
        //padding : 2*16
        //data    : the encoded image

        public ushort Unknown { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public ushort[] Padding { get; set; }
        public byte[] Data { get; set; }

        public ImageBomp(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "BOMP"; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            uint block = 10; //2 unknown + 2 width + 2 height + 4 padding
            block += (uint)Data.Length;

            BlockSize += block;
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Unknown = binaryReader.ReadUint16();
            Width = binaryReader.ReadUint16();
            Height = binaryReader.ReadUint16();

            Padding = new ushort[2];
            Padding[0] = binaryReader.ReadUint16();
            Padding[1] = binaryReader.ReadUint16();

            Data = binaryReader.ReadBytes((int)(BlockSize - 18));
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Write(Unknown);
            binaryWriter.Write(Width);
            binaryWriter.Write(Height);
            foreach (ushort pad in Padding)
            {
                binaryWriter.Write(pad);
            }
            binaryWriter.Write(Data);
        }
    }
}