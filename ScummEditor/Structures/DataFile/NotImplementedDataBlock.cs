using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScummEditor.Structures.DataFile
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
            Contents = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));

            //Hack for the Monkey Island 2 talkie edition (v5/v6 only).
            if (!IsSmallHeader && BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "021_")
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
