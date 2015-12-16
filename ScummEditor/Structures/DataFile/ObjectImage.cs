using System.Collections.Generic;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    //OBIM      - obj image
    //  IMHD    - image header
    //  IMnn    - image data (image for state nn, start with state 1 as state 0 just display nothing) 
    public class ObjectImage : BlockBase
    {
        public ObjectImage(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "OBIM"; }
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            var IMHD = new ObjectImageHeader(this);
            IMHD.LoadFromBinaryReader(binaryReader);
            Childrens.Add(IMHD);

            for (int i = 0; i < IMHD.NumImages; i++)
            {
                var IMXX = new ImageData(this, (byte)(i + 1), IMHD);
                IMXX.LoadFromBinaryReader(binaryReader);

                Childrens.Add(IMXX);
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

        public ObjectImageHeader GetIMHD()
        {
            return (ObjectImageHeader)Childrens.Single(x => x.GetType() == typeof(ObjectImageHeader));
        }

        public List<ImageData> GetIMxx()
        {
            return Childrens.OfType<ImageData>().ToList();
        }
    }
}