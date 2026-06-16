using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>One loaded data container paired with the file it came from.</summary>
    public class DataDisk
    {
        public string FilePath { get; set; }
        public ScummV5V6DataFile Tree { get; set; }
    }
}
