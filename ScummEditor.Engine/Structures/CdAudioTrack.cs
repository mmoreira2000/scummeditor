using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Engine.Structures
{
    /// <summary>One CD audio track inside a CDDA.SOU container.</summary>
    public class CdAudioTrack
    {
        public int Number { get; set; }
        public long Offset { get; set; }
        public long ByteLength { get; set; }
        /// <summary>Absolute start frame on the original CD (75 frames per second).</summary>
        public int StartFrame { get; set; }
        public int FrameCount { get; set; }
        public double DurationSeconds { get; set; }
    }
}
