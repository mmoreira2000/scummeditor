using System.Collections.Generic;
using System;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    //IMnn  -   Image Data
    //  SMAP - Strip Table + Plane 0
    //      or
    //  BOMP - image data
    //
    //  ZPnn - Strip Table + z planes (nn > 1) - Can have 0 or more of these
    public class ImageData : BlockBase
    {
        private readonly IImageSize _imageSize;
        public ImageData(BlockBase blockBase, byte blockTypeNumber, IImageSize imageSize)
            : base(blockBase)
        {
            BlockTypeNumber = blockTypeNumber;
            _imageSize = imageSize;
        }

        public byte BlockTypeNumber { get; set; }
        public override string BlockType
        {
            get { return "IM" + BlockTypeNumber.ToString("X").PadLeft(2, '0'); }
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            if (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "BOMP")
            {
                var BOMP = new ImageBomp(this);
                BOMP.LoadFromBinaryReader(binaryReader);
                Childrens.Add(BOMP);
            }
            else
            {
                var SMAP = new ImageStripTable(this, _imageSize);
                SMAP.LoadFromBinaryReader(binaryReader);
                Childrens.Add(SMAP);
            }

            var nextBlockName = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
            byte planeNumber = 0;
            while (nextBlockName.Substring(0, 2) == "ZP" && (int.Parse(nextBlockName.Substring(2)) < 99))
            {
                planeNumber++;

                var zPlane = new ZPlane(this, planeNumber, _imageSize);
                zPlane.LoadFromBinaryReader(binaryReader);
                Childrens.Add(zPlane);

                nextBlockName = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
            }
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
        }

        public List<ZPlane> GetZPlanes()
        {
            return Childrens.OfType<ZPlane>().ToList();
        }

        public ImageBomp GetBOMP()
        {
            return (ImageBomp)Childrens.SingleOrDefault(x => x.GetType() == typeof(ImageBomp));
        }

        public ImageStripTable GetSMAP()
        {
            return (ImageStripTable)Childrens.SingleOrDefault(x => x.GetType() == typeof(ImageStripTable));
        }
    }
}