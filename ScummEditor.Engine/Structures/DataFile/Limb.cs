using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Exceptions;

namespace ScummEditor.Engine.Structures.DataFile
{

    public class Limb
    {
        public Limb()
        {
            ImageOffsets = new List<ushort>();
        }
        public ushort OffSet { get; set; }
        public ushort Size { get; set; }
        public List<ushort> ImageOffsets { get; set; }
    }

}
