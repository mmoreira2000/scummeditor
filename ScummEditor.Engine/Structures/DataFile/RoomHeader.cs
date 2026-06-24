namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    RMHD - Room Header
    v4/v5/v6:                 v7 (The Dig, Full Throttle):
      width    : 2 bytes        version  : 4 bytes (LE)
      height   : 2 bytes        width    : 2 bytes
      num objs : 2 bytes        height   : 2 bytes
                                num objs : 2 bytes
    The v7 body is 10 bytes (a leading version word was added); v8 widens the fields further and is
    not handled here.
    */

    public class RoomHeader : BlockBase, IImageSize
    {
        public RoomHeader(BlockBase blockBase) : base(blockBase) { }

        /// <summary>v7-only leading version word (0 on v4/v5/v6, which have no such field).</summary>
        public uint Version { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public ushort NumObjects { get; set; }

        private bool IsV7
        {
            get { return _gameInfo != null && _gameInfo.ScummVersion == 7; }
        }

        // v4 calls this block "HD"; v5/v6/v7 call it "RMHD". The body (width/height/numObjects) is the
        // same, except v7 prefixes a 4-byte version word.
        public override string BlockType
        {
            get { return IsSmallHeader ? "HD" : "RMHD"; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            BlockSize += IsV7 ? (uint)10 : 6;
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            if (IsV7)
            {
                Version = binaryReader.ReadUint32();
            }
            Width = binaryReader.ReadUint16();
            Height = binaryReader.ReadUint16();
            NumObjects = binaryReader.ReadUint16();
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            if (IsV7)
            {
                binaryWriter.Write(Version);
            }
            binaryWriter.Write(Width);
            binaryWriter.Write(Height);
            binaryWriter.Write(NumObjects);
        }
    }
}