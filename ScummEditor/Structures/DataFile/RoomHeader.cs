namespace ScummEditor.Structures.DataFile
{
    /*
    RMHD - Room Header
    width    : 2 bytes
    height   : 2 bytes
    num objs : 2 bytes
    */

    public class RoomHeader : BlockBase, IImageSize
    {
        public RoomHeader(BlockBase blockBase) : base(blockBase) { }

        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public ushort NumObjects { get; set; }

        public override string BlockType
        {
            get { return "RMHD"; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            BlockSize += 6;
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Width = binaryReader.ReadUint16();
            Height = binaryReader.ReadUint16();
            NumObjects = binaryReader.ReadUint16();
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Write(Width);
            binaryWriter.Write(Height);
            binaryWriter.Write(NumObjects);
        }
    }
}