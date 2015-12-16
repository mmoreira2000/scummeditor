using System.Collections.Generic;

namespace ScummEditor.Encoders
{
    public class SegmentInformation
    {
        public bool RepeatSameColor { get; set; }
        public List<byte> Colors { get; set; }

        public SegmentInformation()
        {
            Colors = new List<byte>();
        }
    }
}