using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.IndexFile
{
    public struct DirectoryObject
    {
        public byte Owner { get; set; }
        public byte State { get; set; }
        public uint ClassData { get; set; }
    }

}
