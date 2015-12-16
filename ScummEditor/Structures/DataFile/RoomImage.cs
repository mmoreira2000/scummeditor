using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    //RMIM      -   Room Image
    //  RMIH    -   Room Image Header
    //  IM00    -   Room Image Data (background)
    public class RoomImage : BlockBase
    {
        private readonly RoomBlock _roomBlock;
        //public RoomImageHeader RMIH { get; set; }
        //public ImageData IM00 { get; set; }

        public RoomImage(BlockBase blockBase, RoomBlock roomBlock) : base(blockBase)
        {
            _roomBlock = roomBlock;
        }

        public override string BlockType
        {
            get { return "RMIM"; }
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            var RMIH = new RoomImageHeader(this);
            RMIH.LoadFromBinaryReader(binaryReader);
            Childrens.Add(RMIH);

            var IM00 = new ImageData(this, 0, _roomBlock.GetRMHD());
            IM00.LoadFromBinaryReader(binaryReader);
            Childrens.Add(IM00);
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (BlockBase children in Childrens)
            {
                children.SaveToBinaryWriter(binaryWriter);
            }
        }

        public RoomImageHeader GetRMIH()
        {
            return (RoomImageHeader) Childrens.Single(x => x.GetType() == typeof (RoomImageHeader));
        }

        public ImageData GetIM00()
        {
            return (ImageData)Childrens.Single(x => x.GetType() == typeof(ImageData));
        }
    }
}