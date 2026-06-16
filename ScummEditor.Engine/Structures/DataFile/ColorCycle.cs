using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures.DataFile
{
    public class ColorCycle
    {
        public byte Index { get; set; }
        public ushort Unkown { get; set; }
        public ushort Freq { get; set; }
        public ushort Flags { get; set; }
        public byte Start { get; set; }
        public byte End { get; set; }
    }

}