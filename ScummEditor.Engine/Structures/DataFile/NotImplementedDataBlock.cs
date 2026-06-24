using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScummEditor.Engine.Structures.DataFile
{
    public class NotImplementedDataBlock : BlockBase
    {
        public byte[] Contents { get; set; }

        private readonly string _blockType;
        public NotImplementedDataBlock(BlockBase blockBase, string blockType)
            : base(blockBase)
        {
            _blockType = blockType;
        }

        /// <summary>For top-level blocks that have no parent (e.g. the v4 index blocks).</summary>
        public NotImplementedDataBlock(BlockBase blockBase, string blockType, GameInfo gameInfo)
            : base(blockBase, gameInfo)
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

            BlockSize += (uint)Contents.Length;

        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            int bodyLen = (int)(BlockSize - HeaderLength);
            if (bodyLen < 0) bodyLen = 0; // a corrupt/misaligned size must not throw OverflowException on new byte[neg]
            Contents = binaryReader.ReadBytes(bodyLen);

            // Hack for the "021_" stray block the Monkey Island 2 ULTIMATE TALKIE packer leaves behind (8
            // orphan bytes after a generic block). Gate it to talkie games: a non-talkie game (e.g. MI2
            // Floppy) never carries that stray, and content-sniffing "021_" on EVERY generic block made an
            // edited floppy game's shifted bytes false-match here, absorb 8 bytes, desync the block stream
            // and overflow when the editor RE-OPENED its own (otherwise valid) output. (v5/v6 only.)
            if (!IsSmallHeader && _gameInfo != null && _gameInfo.IsTalkie
                && BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "021_")
            {
                var lstBytes = new List<byte>(Contents);
                lstBytes.AddRange(binaryReader.ReadBytes(8));

                Contents = lstBytes.ToArray();
            }
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.WriteBytes(Contents);
        }
    }
}
