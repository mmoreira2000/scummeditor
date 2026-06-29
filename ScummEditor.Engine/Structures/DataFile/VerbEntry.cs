using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Engine.Structures.DataFile
{

    public class VerbEntry
    {
        /// <summary>Verb id. 8-bit for v4-v7; 32-bit for v8 (which also uses 0xFFFFFFFF as the fallback id).</summary>
        public int Id { get; set; }
        /// <summary>Bytecode offset relative to the verb-table base: the VERB tag for v4-v7, the VERB body
        /// (tag + 8) for v8. Add ObjectCode.VerbEntryBase to get the RawContent index.</summary>
        public int Offset { get; set; }
    }
}
