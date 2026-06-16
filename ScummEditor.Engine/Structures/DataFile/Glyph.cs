using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    public class Glyph
    {
        public int Index { get; set; }
        public bool Present { get; set; }
        public int DataOffset { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int XOffset { get; set; }
        public int YOffset { get; set; }
    }
}
