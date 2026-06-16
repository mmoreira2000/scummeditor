using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>One loaded standalone font (v4 90x.LFL) paired with the file it came from.</summary>
    public class FontResource
    {
        public string FilePath { get; set; }
        public Charset Charset { get; set; }
    }
}
