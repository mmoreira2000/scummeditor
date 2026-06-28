using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    SCUMM v4 data container (DISKnn.LEC, one file per floppy disk; XOR 0x69).

    Layout (small header: [size:4 LE][tag:2 ascii], size includes the 6-byte header):
        LE                      whole-file wrapper (= v5/v6 LECF)
          FO                    room offset table for this disk (= v5/v6 LOFF)
          LF x N                one per room (= v5/v6 LFLF); a 2-byte room number follows the header
            RO                  the room (= v5/v6 ROOM)
            SC x n              room/global scripts
            SO                  sound

    For now every block is kept byte-for-byte (RawContent) and only the structural containers
    (LE / LF / RO) are walked, so the file round-trips identically. Typed parsing of the inner
    blocks is layered on top in later steps.
    */
    public class ScummV4DataFile : ScummDataFile
    {
        public ScummV4DataFile(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override string BlockType
        {
            get { return "LE"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            ReadBlockHeader(binaryReader);
            ScummV4Blocks.WalkChildren(this, binaryReader, BlockOffSet + BlockSize);
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            WriteBlockHeader(binaryWriter);
            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
            binaryWriter.Flush();
        }
    }
}
