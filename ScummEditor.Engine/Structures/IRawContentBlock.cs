namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// A block whose body is kept as raw bytes (or null when the block is a parsed container that
    /// holds its bytes in its children). Lets the GUI show a hex view for the generic byte-preserved
    /// blocks (NotImplementedDataBlock and the v7 RawContainerBlock / RawDataBlock / RawIndexBlock).
    /// </summary>
    public interface IRawContentBlock
    {
        byte[] Contents { get; }
    }
}
