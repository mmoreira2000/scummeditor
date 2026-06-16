using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures
{
    /// <summary>One speech/effect line inside a .SOU container.</summary>
    public class SpeechSouEntry
    {
        public int Index { get; set; }
        /// <summary>Offset of the VCTL tag in the file (the offset the game scripts reference).</summary>
        public long Offset { get; set; }
        /// <summary>Number of lip-sync timestamps in the VCTL block.</summary>
        public int LipSyncCount { get; set; }
        /// <summary>Offset/length of the embedded Creative VOC file.</summary>
        public long VocOffset { get; set; }
        public int VocLength { get; set; }
        public int SampleRate { get; set; }
        public double DurationSeconds { get; set; }
    }
}
