using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>A v4 room container (LF). Begins with a 2-byte room number, then the room blocks.</summary>
    public class ScummV4DiskBlock : BlockBase
    {
        public ScummV4DiskBlock(BlockBase blockBase) : base(blockBase) { }

        public ushort RoomNumber { get; set; }

        public override string BlockType
        {
            get { return "LF"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            ReadBlockHeader(binaryReader);
            RoomNumber = binaryReader.ReadUint16(false);
            ScummV4Blocks.WalkChildren(this, binaryReader, BlockOffSet + BlockSize);
        }

        /// <summary>The room (RO) of this disk block; costumes resolve their palette from it.</summary>
        public ScummV4RoomBlock GetRoom()
        {
            return Childrens.OfType<ScummV4RoomBlock>().FirstOrDefault();
        }

        /// <summary>The costumes (CO) bundled in this disk block.</summary>
        public List<CostumeV4> GetCostumes()
        {
            return Childrens.OfType<CostumeV4>().ToList();
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += 2; // the room-number word that precedes the child blocks
        }

        public override void CalculateOffsets()
        {
            long nextOffSet = BlockOffSet + HeaderLength + 2;
            foreach (BlockBase child in Childrens)
            {
                nextOffSet = ConfigureAndReturnNextOffset(child, nextOffSet);
                child.CalculateOffsets();
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            WriteBlockHeader(binaryWriter);
            binaryWriter.Write(RoomNumber, false);
            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
        }
    }
}
