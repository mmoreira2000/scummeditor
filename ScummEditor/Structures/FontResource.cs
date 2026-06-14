using ScummEditor.Structures.DataFile;

namespace ScummEditor.Structures
{
    /// <summary>One loaded standalone font (v4 90x.LFL) paired with the file it came from.</summary>
    public class FontResource
    {
        public string FilePath { get; set; }
        public Charset Charset { get; set; }
    }
}
