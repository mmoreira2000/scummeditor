using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Engine.Structures.DataFile
{

    public class VerbEntry
    {
        public byte Id { get; set; }
        /// <summary>Bytecode offset relative to the VERB tag position (as used by the engine).</summary>
        public int Offset { get; set; }
    }
}
