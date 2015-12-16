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
            Contents = binaryReader.ReadBytes((int)(BlockSize - 8));

            //Hack para o monkey island 2 com vozes.
            if (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "021_")
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
