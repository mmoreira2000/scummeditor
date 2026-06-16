using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures.DataFile
{
    public class Box
    {
        public short Ulx { get; set; }
        public short Uly { get; set; }
        public short Urx { get; set; }
        public short Ury { get; set; }
        public short Lrx { get; set; }
        public short Lry { get; set; }
        public short Llx { get; set; }
        public short Lly { get; set; }
        public byte Mask { get; set; }
        public byte Flags { get; set; }
        public ushort Scale { get; set; }
    }
}
