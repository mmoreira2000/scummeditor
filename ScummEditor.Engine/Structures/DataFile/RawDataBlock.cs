using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// A run of raw bytes inside a v4 container that is not a self-describing block (e.g. the
    /// trailing sound data of a room). It has no header of its own and is kept verbatim.
    /// </summary>
    public class RawDataBlock : BlockBase, IRawContentBlock
    {
        private readonly int _length;
        public byte[] Contents { get; set; }

        public RawDataBlock(BlockBase blockBase, int length) : base(blockBase)
        {
            _length = length;
        }

        public override string BlockType
        {
            get { return "(raw)"; }
        }

        public override void CalculateBlockSize()
        {
            BlockSize = (uint)Contents.Length; // pure data: no header
        }

        public override void CalculateOffsets()
        {
            // no children
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            BlockOffSet = binaryReader.Position;
            Contents = binaryReader.ReadBytes(_length);
            BlockSize = (uint)Contents.Length; // header-less: the block size is just its length
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            binaryWriter.WriteBytes(Contents);
        }
    }
}
