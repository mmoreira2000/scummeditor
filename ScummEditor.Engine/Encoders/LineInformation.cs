using System.Collections.Generic;

namespace ScummEditor.Encoders
{
    public class LineInformation
    {
        public bool RepeatSameLine { get; set; }
        public List<byte> Lines { get; set; }

        public LineInformation()
        {
            Lines = new List<byte>();
        }
    }
}