using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ScummEditor.Engine.Structures.DataFile
{

    /*
    IDs 	        Method      	Rendering Direction 	Transparent 	Param Subtraction 	Remarks
    0x01 	        Uncompressed 	Horizontal          	No 	            - 	-
    0x0E .. 0x12 	1st method   	Vertical            	No           	0x0A
    0x18 .. 0x1C 	1st method  	Horizontal          	No          	0x14
    0x22 .. 0x26 	1st method  	Vertical 	            Yes 	        0x1E
    0x2C .. 0x30 	1st method  	Horizontal           	Yes          	0x28

    0x40 .. 0x44 	2nd method  	Horizontal 	            No 	            0x3C 	            //Not sure these two lines are right.
    0x54 .. 0x58 	2nd method  	Horizontal          	Yes         	0x50 	            //Not sure these two lines are right.
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

}
