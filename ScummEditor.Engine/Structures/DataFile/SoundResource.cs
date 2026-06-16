using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Structures.DataFile
{
    public class SoundResource
    {
        /// <summary>Leaf block tag, trimmed (e.g. "ADL", "GMD", "MIDI", "SBL", "AUdt").</summary>
        public string Type { get; set; }

        /// <summary>Path from the SOUN root, e.g. "SOU/ADL".</summary>
        public string Path { get; set; }

        /// <summary>Offset of the payload inside the SOUN block content (debug aid).</summary>
        public int Offset { get; set; }

        /// <summary>The leaf payload bytes (without the 8-byte type+size header).</summary>
        public byte[] Data { get; set; }
    }
}
