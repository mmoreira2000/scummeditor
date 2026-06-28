using System.IO;

namespace ScummEditor.Engine.Structures.IndexFile
{
    /// <summary>
    /// A SCUMM index block kept verbatim (tag + raw body). Used for the v7 index blocks that carry no
    /// data-file offsets and so need no interpretation for editing: RNAM (room names), MAXS (engine
    /// maximums + version strings), DOBJ (global object owner/class table), AARY (arrays) and the
    /// v7-only ANAM (audio resource names). They are read and written back byte-for-byte; only the
    /// resource directories (DROO/DSCR/DSOU/DCOS/DCHR), whose offsets move when blocks are edited, are
    /// parsed into typed objects.
    /// </summary>
    public class RawIndexBlock : BlockBase, IRawContentBlock
    {
        private readonly string _blockType;
        public byte[] Contents { get; set; }

        public RawIndexBlock(GameInfo gameInfo, string blockType) : base(null, gameInfo)
        {
            _blockType = blockType;
        }

        public override string BlockType
        {
            get { return _blockType; }
        }

        public override void CalculateBlockSize()
        {
            BlockSize = (uint)(HeaderLength + Contents.Length);
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader); // reads & validates the [tag][size] header

            int bodyLength = (int)BlockSize - HeaderLength;
            if (bodyLength < 0)
            {
                bodyLength = 0;
            }
            Contents = binaryReader.ReadBytes(bodyLength);
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(Contents);
        }
    }
}
