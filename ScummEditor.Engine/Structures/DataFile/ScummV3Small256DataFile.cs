using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    SCUMM v3 "GF_OLD256" room container - one NN.LFL file per room (Indy3 VGA, Zak FM-Towns).
    Unlike v4 (a DISKnn.LEC packing many rooms behind an LE/FO/LF wrapper), a v3 room file is just
    a bare sequence of v4-style small-header blocks starting at offset 0:

        RO            the room (= v5/v6 ROOM); same HD/BX/PA/BM/OI/OC children the editor's v4 parser
                      already produces
        SC / CO / SO  the room's global scripts, costumes and sounds, as top-level siblings of RO

    There is NO file-level header, NO LE/FO wrapper and NO 2-byte room-number word - the room number
    is the file name (NN). So this container has no on-disk header of its own: it loads, sizes and
    writes its children straight from/at offset 0. The blocks themselves are byte-identical to v4, so
    the whole v4 block machinery (ScummV4Blocks, ScummV4RoomBlock, ObjectCode, CostumeV4, SoundBlockV4)
    is reused unchanged; the index gives resource positions as file-absolute offsets (RO at 0).
    */
    public class ScummV3Small256DataFile : ScummDataFile, IScummRoomContainer
    {
        public ScummV3Small256DataFile(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            // No file-level header: the first block (RO) sits at offset 0.
            BlockOffSet = binaryReader.Position;
            ScummV4Blocks.WalkChildren(this, binaryReader, binaryReader.Length);
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
            binaryWriter.Flush();
        }

        /// <summary>The whole-file size = sum of the child blocks (the container has no header).</summary>
        public override void CalculateBlockSize()
        {
            uint total = 0;
            Childrens.ForEach(b => b.CalculateBlockSize());
            Childrens.ForEach(b => total += b.BlockSize);
            BlockSize = total;
        }

        /// <summary>Children are laid out from offset 0 (no file-level header to skip).</summary>
        public override void CalculateOffsets()
        {
            long nextOffSet = BlockOffSet;
            foreach (BlockBase child in Childrens)
            {
                nextOffSet = ConfigureAndReturnNextOffset(child, nextOffSet);
                child.CalculateOffsets();
            }
        }

        /// <summary>The room block (RO) of this file, or null if the file has none.</summary>
        public ScummV4RoomBlock GetRoom()
        {
            return Childrens.OfType<ScummV4RoomBlock>().FirstOrDefault();
        }

        /// <summary>The costumes (CO) that sit alongside the room as top-level siblings of RO.</summary>
        public List<CostumeV4> GetCostumes()
        {
            return Childrens.OfType<CostumeV4>().ToList();
        }
    }
}
