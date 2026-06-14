using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    /// <summary>A v4 room (RO): a sequence of room sub-blocks (HD, CC, BX, PA, BM, OI, OC, EX, EN, LS, ...).</summary>
    public class ScummV4RoomBlock : BlockBase
    {
        public ScummV4RoomBlock(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "RO"; }
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
        }

        /// <summary>The room header (HD = v5/v6 RMHD), carrying the room width/height/object count.</summary>
        public RoomHeader GetHD()
        {
            return Childrens.OfType<RoomHeader>().FirstOrDefault();
        }

        /// <summary>The room palette (PA = v5/v6 CLUT). Null for EGA rooms, which store no palette.</summary>
        public PaletteData GetPA()
        {
            return Childrens.OfType<PaletteData>().FirstOrDefault();
        }

        /// <summary>The room background image (BM).</summary>
        public ScummV4ImageBlock GetBM()
        {
            return Childrens.OfType<ScummV4ImageBlock>().FirstOrDefault(b => b.BlockType == "BM");
        }

        /// <summary>The object images (OI), in file order.</summary>
        public List<ScummV4ImageBlock> GetObjectImages()
        {
            return Childrens.OfType<ScummV4ImageBlock>().Where(b => b.BlockType == "OI").ToList();
        }

        /// <summary>The object code/metadata blocks (OC), in file order.</summary>
        public List<ObjectCode> GetObjectCodes()
        {
            return Childrens.OfType<ObjectCode>().ToList();
        }

        /// <summary>True for the 16-color EGA edition, whose image codec differs from VGA.</summary>
        public bool IsEga
        {
            get { return GameInfo != null && GameInfo.Edition == GameEdition.FloppyEga; }
        }
    }
}
