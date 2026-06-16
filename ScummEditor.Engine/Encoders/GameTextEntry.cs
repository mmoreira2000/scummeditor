using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>One translatable text with its stable id (e.g. "LF003.SCRP000.t005").</summary>
    public class GameTextEntry
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Text { get; set; }
    }
}
