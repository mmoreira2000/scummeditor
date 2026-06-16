using System.Collections.Generic;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// A descriptor of one sub-block inside a v4 "SO" sound block (WA, AD, or a nested SO), for the
    /// read-only display tree. Offset/Size index into the parent SoundBlockV4.RawContent.
    /// </summary>
    public class SoundSubBlockV4
    {
        public string Tag { get; set; }
        public int Offset { get; set; }   // RawContent-relative position of this sub-block's header
        public int Size { get; set; }     // total sub-block size (6-byte small header + payload)
        public string Kind { get; set; }  // human label (AdLib music / AdLib SFX / Roland-waveform / nested)
        public List<SoundSubBlockV4> Children { get; set; }
    }
}
