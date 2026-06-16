using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.IndexFile
{
    public struct DirectoryArray
    {
        public ushort VariableNumber { get; set; }
        public ushort XSize { get; set; }
        public ushort YSize { get; set; }
        public ushort Type { get; set; }
    }

}
