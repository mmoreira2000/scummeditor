using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    public class MatrixRow
    {
        public MatrixRow()
        {
            Links = new List<BoxLink>();
        }

        public List<BoxLink> Links { get; set; }
    }
}
