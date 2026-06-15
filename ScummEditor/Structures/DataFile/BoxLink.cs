using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    public class BoxLink
    {
        public byte Start { get; set; }
        public byte End { get; set; }
        public byte Box { get; set; }
    }
}
