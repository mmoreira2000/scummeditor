using System;

namespace ScummEditor.Structures.DataFile
{
    //PALS - Pallete data
    //  WRAP - Dummy wrapper
    public class PalettesData : BlockBase
    {
        public PalettesData(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "PALS"; }
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            var WRAP = new PalettesWrapper(this);
            WRAP.LoadFromBinaryReader(binaryReader);
            Childrens.Add(WRAP);
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (BlockBase children in Childrens)
            {
                children.SaveToBinaryWriter(binaryWriter);
            }
        }

        public PalettesWrapper GetWRAP()
        {
            return (PalettesWrapper)Childrens[0];
        }
    }
}