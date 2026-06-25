using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>One loaded external .NUT SMUSH font (v7 The Dig / Full Throttle) paired with the file it
    /// came from. The counterpart of <see cref="FontResource"/> for the in-container CHAR charsets.</summary>
    public class NutFontResource
    {
        public string FilePath { get; set; }
        public NutFont Font { get; set; }
    }
}
