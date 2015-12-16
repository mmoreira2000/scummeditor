using System.Collections.Generic;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    //WRAP - Dummy wrapper
    //  OFFS    - Palette Index (size is blockheader + 2 bytes per palette) ??
    //  APAL*n  - 256 entries RGB palette
    //
    //  NOTE: Note that the APAL blocks are constructed recursively. 
    //        If we have for example 3 palettes, the first APAL block contains its palette followed by 2 other APAL blocks, etc.
    public class PalettesWrapper : BlockBase
    {
        public PalettesWrapper(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "WRAP"; }
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            var OFFS = new PaletteOffset(this);
            OFFS.LoadFromBinaryReader(binaryReader);
            Childrens.Add(OFFS);

            for (int i = 0; i < OFFS.Offsets.Count; i++)
            {
                var APAL = new PaletteData(this, "APAL");
                APAL.LoadFromBinaryReader(binaryReader);

                Childrens.Add(APAL);
            }

        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (BlockBase children in Childrens)
            {
                children.SaveToBinaryWriter(binaryWriter);
            }
        }

        public PaletteOffset GetOFFS()
        {
            return (PaletteOffset)Childrens.First(x => x.GetType() == typeof(PaletteOffset));
        }

        public List<PaletteData> GetAPALs()
        {
            return Childrens.OfType<PaletteData>().ToList();
        }
    }
}