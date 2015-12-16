using System.Collections.Generic;

namespace ScummEditor.Structures.DataFile
{
     /*
     The encoding is very similar to the SMAP. Again we have an offset table followed by the encoded data. 
        However the offset are only 16 bits width and the special offset of 0 is used to code a stripe full of 0. 
        But the LEC interpreter react strangely to these strides.

     offset table : width/8 elements. Offset are relative to the start of the ZPnn block (-8 offset)
     strip offset: 2 bytes * n
     stripe * n
         data : variable Length
     */
    public class ZPlane:BlockBase
    {
        private readonly IImageSize _imageSize;

        public ZPlane(BlockBase blockBase, byte blockTypeNumber, IImageSize imageSize) : base(blockBase)
        {
            BlockTypeNumber = blockTypeNumber;
            _imageSize = imageSize;
        }

        public byte BlockTypeNumber { get; set; }
        public override string BlockType
        {
            get { return "ZP" + BlockTypeNumber.ToString().PadLeft(2, '0'); }
        }

        public List<ZPlaneStripData> Strips { get; set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            uint block = 0;

            foreach (ZPlaneStripData stripData in Strips)
            {
                block += 2; //2 bytes for each strip, to identify its offset
                block += (uint)stripData.ImageData.Length; //the size of the data.
            }

            BlockSize += block;
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            int numStrips = _imageSize.Width / 8;
            Strips = new List<ZPlaneStripData>(numStrips);

            for (int i = 0; i < numStrips; i++)
            {
                var strip = new ZPlaneStripData();
                strip.OffSet = binaryReader.ReadUint16();

                Strips.Add(strip);
            }

            int stripSize;
            for (int i = 0; i < (numStrips - 1); i++)
            {
                //O tamanho do strip é determinando pegando a posição do próximo Strip - 1 (porque o primeiro byte é destinado a informações de CODEC)
                //e subtraindo da posição do Strip atual.
                stripSize = (int)((Strips[i + 1].OffSet - Strips[i].OffSet));

                //Strips[i].CodecId = binaryReader.ReadByte1();
                Strips[i].ImageData = binaryReader.ReadBytes(stripSize);
            }

            if (Strips.Count > 0) //Sam & Max has at least one ROOM that contains only palette and ZPlanes, but no images.
            {
                stripSize = (int)((BlockSize - Strips[Strips.Count - 1].OffSet));
                //Strips[Strips.Count - 1].CodecId = binaryReader.ReadByte1();
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
                binaryWriter.Write(stripData.ImageData);
            }
        }
    }

    public class ZPlaneStripData
    {
        public ushort OffSet { get; set; }
        public byte[] ImageData { get; set; }
    }
}