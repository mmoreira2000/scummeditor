using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    n = imagesize(room or object) / 8
    strip offset : 4 bytes * n (offset from this SMAP. 1 per column of 8 pix)
    stripes * n
        codec   : 1 byte
        data    : variable length
    */
    public class ImageStripTable : BlockBase
    {
        private readonly IImageSize _imageSize;
        public ImageStripTable(BlockBase blockBase, IImageSize imageSize) : base(blockBase)
        {
            _imageSize = imageSize;
        }

        public List<StripData> Strips { get; set; }

        public override string BlockType
        {
            get { return "SMAP"; }
        }


        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            uint block = 0;

            foreach (StripData stripData in Strips)
            {
                block += 4; //4 bytes for each strip, to identify its offset
                block += 1; //1 byte contains codec information
                block +=(uint)stripData.ImageData.Length; //the size of the data.
            }

            BlockSize += block;
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            int numStrips = _imageSize.Width / 8;
            Strips = new List<StripData>(numStrips);

            for (int i = 0; i < numStrips; i++)
            {
                var strip = new StripData();
                strip.OffSet = binaryReader.ReadUint32();

                Strips.Add(strip);
            }

            int stripSize;
            for (int i = 0; i < (numStrips - 1); i++)
            {
                //The strip size is the next strip position - 1 (the first byte of the next strip)
                //minus the current strip position.
                stripSize = (int)((Strips[i + 1].OffSet - Strips[i].OffSet) - 1);

                Strips[i].CodecId = binaryReader.ReadByte1();
                Strips[i].ImageData = binaryReader.ReadBytes(stripSize);
            }

            if (Strips.Count > 0) //Sam & Max has at least one ROOM that contains only palette and ZPlanes, but no images.
            {
                stripSize = (int)((BlockSize - Strips[Strips.Count - 1].OffSet) - 1);
                Strips[Strips.Count - 1].CodecId = binaryReader.ReadByte1();
                Strips[Strips.Count - 1].ImageData = binaryReader.ReadBytes(stripSize);
            }
        }



        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (var stripData in Strips)
            {
                binaryWriter.Write(stripData.OffSet);
            }

            foreach (var stripData in Strips)
            {
                binaryWriter.Write(stripData.CodecId);
                binaryWriter.Write(stripData.ImageData);
            }
        }
    }

}