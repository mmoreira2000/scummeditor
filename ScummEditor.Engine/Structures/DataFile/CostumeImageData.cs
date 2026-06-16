using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Exceptions;

namespace ScummEditor.Engine.Structures.DataFile
{

    /*
    picts
        width            : 16le
        height           : 16le
        rel_x            : s16le
        rel_y            : s16le
        move_x           : s16le
        move_y           : s16le
        redir_limb       : 8 only present if((format & 0x7E) == 0x60)
        redir_pict       : 8 only present if((format & 0x7E) == 0x60)
        rle data

     */
    public class CostumeImageData
    {
        //The properties below are computed by the reader to help extract the data and regenerate it
        //later, updating the limb position information.
        public int ImageDataSize { get; set; }
        public ushort ImageStartOffSet { get; set; }
        public bool HasRedirInfo { get; set; }

        //Dados extraidos do binario
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public short RelX { get; set; }
        public short RelY { get; set; }
        public short MoveX { get; set; }
        public short MoveY { get; set; }
        public byte RedirLimb { get; set; }
        public byte RedirPict { get; set; }
        public byte[] ImageData { get; set; }

        public ushort GetSize()
        {
            ushort size = 2; //Width;
            size += 2; //Height
            size += 2; //RelX
            size += 2; //RelY
            size += 2; //MoveX
            size += 2; //MoveY
            if (HasRedirInfo)
            {
                size += 1; //RedirLimb
                size += 1; //RedirPict
            }
            size += (ushort)ImageData.Length; //Size of ImageData

            return size;
        }
    }

}
