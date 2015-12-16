using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ScummEditor.Structures.DataFile
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
                //O tamanho do strip é determinando pegando a posição do próximo Strip - 1 (pois o primeiro byte tem as informações do CODEC)
                //e subtraindo da posição do Strip atual.
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

    /*
    IDs 	        Method      	Rendering Direction 	Transparent 	Param Subtraction 	Remarks
    0x01 	        Uncompressed 	Horizontal          	No 	            - 	-
    0x0E .. 0x12 	1st method   	Vertical            	No           	0x0A 	
    0x18 .. 0x1C 	1st method  	Horizontal          	No          	0x14 	
    0x22 .. 0x26 	1st method  	Vertical 	            Yes 	        0x1E 	
    0x2C .. 0x30 	1st method  	Horizontal           	Yes          	0x28 	
            
    0x40 .. 0x44 	2nd method  	Horizontal 	            No 	            0x3C 	            //Não sei se essas duas linhas estão certas, pois nenhuma imagem no DOTT ou SAM&MAX utiliza essas compressões
    0x54 .. 0x58 	2nd method  	Horizontal          	Yes         	0x50 	            //Não sei se essas duas linhas estão certas, pois nenhuma umagem no DOTT ou SAM&MAX utiliza essas compressões
    0x68 .. 0x6C 	2nd method  	Horizontal           	No  	        0x64 	            Same as 0x54 .. 0x58 //Eu inverti essas 2 transparencias, estava errado no site.
    0x7C .. 0x80 	2nd method  	Horizontal 	            Yes	            0x78 	            Same as 0x40 .. 0x44 //Eu inverti essas 2 transparencias, estava errado no site.
     */
    public class StripData
    {
        public uint OffSet { get; set; }
        private byte _codecId;
        public byte CodecId
        {
            get { return _codecId; }
            set
            {
                _codecId = value;
                SetCompressionInformation();
            }
        }


        public byte[] ImageData { get; set; }

        public CompressionTypes CompressionType { get; private set; }
        public RenderingDirections RenderdingDirection { get; private set; }
        public bool Transparent { get; private set; }
        public int ParamSubtraction { get; private set; }

        private void SetCompressionInformation()
        {
            if (CodecId == 0x01)
            {
                CompressionType = CompressionTypes.Uncompressed;
                RenderdingDirection = RenderingDirections.Horizontal;
                Transparent = false;
                ParamSubtraction = -1;
            }
            else if (CodecId >= 0x0E && CodecId <= 0x12)
            {
                CompressionType = CompressionTypes.Method1;
                RenderdingDirection = RenderingDirections.Vertical;
                Transparent = false;
                ParamSubtraction = 0x0A;
            }
            else if (CodecId >= 0x18 && CodecId <= 0x1C)
            {
                CompressionType = CompressionTypes.Method1;
                RenderdingDirection = RenderingDirections.Horizontal;
                Transparent = false;
                ParamSubtraction = 0x14;
            }
            else if (CodecId >= 0x22 && CodecId <= 0x26)
            {
                CompressionType = CompressionTypes.Method1;
                RenderdingDirection = RenderingDirections.Vertical;
                Transparent = true;
                ParamSubtraction = 0x1E;
            }
            else if (CodecId >= 0x2C && CodecId <= 0x30)
            {
                CompressionType = CompressionTypes.Method1;
                RenderdingDirection = RenderingDirections.Horizontal;
                Transparent = true;
                ParamSubtraction = 0x28;
            }
            else if (CodecId >= 0x40 && CodecId <= 0x44)
            {
                //Debugger.Break();
                CompressionType = CompressionTypes.Method2;
                RenderdingDirection = RenderingDirections.Horizontal;
                Transparent = false;
                ParamSubtraction = 0x3C;
            }
            else if (CodecId >= 0x54 && CodecId <= 0x58)
            {
                //Debugger.Break();
                CompressionType = CompressionTypes.Method2;
                RenderdingDirection = RenderingDirections.Horizontal;
                Transparent = true;
                ParamSubtraction = 0x50;
            }
            else if (CodecId >= 0x68 && CodecId <= 0x6C)
            {
                CompressionType = CompressionTypes.Method2;
                RenderdingDirection = RenderingDirections.Horizontal;
                Transparent = false;
                ParamSubtraction = 0x64;
            }
            else if (CodecId >= 0x7C && CodecId <= 0x80)
            {
                CompressionType = CompressionTypes.Method2;
                RenderdingDirection = RenderingDirections.Horizontal;
                Transparent = true;
                ParamSubtraction = 0x78;
            }
            else
            {
                CompressionType = CompressionTypes.Unknow;
                RenderdingDirection = RenderingDirections.Unknow;
                Transparent = false;
                ParamSubtraction = -2;
            }
        }

    }

    public enum CompressionTypes
    {
        Uncompressed = 0,
        Method1 = 1,
        Method2 = 2,
        Unknow = 3
    }

    public enum RenderingDirections
    {
        Horizontal = 0,
        Vertical = 1,
        Unknow = 3
    }

}