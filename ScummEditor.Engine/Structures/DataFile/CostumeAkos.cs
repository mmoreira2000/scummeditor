namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// A SCUMM v7 AKOS costume (The Dig, Full Throttle). Structurally it is just the generic IFF
    /// container (AKHD/AKOF/AKCI/AKCD/AKPL/RGBS sub-blocks), so it reuses RawContainerBlock unchanged
    /// for a byte-exact round-trip. The distinct type exists only so the GUI can route it to the AKOS
    /// costume viewer (AkosImageDecoder reads its sub-blocks) instead of the generic hex view - the
    /// same pattern the v5/v6 Costume and v4 CostumeV4 blocks use.
    /// </summary>
    public class CostumeAkos : RawContainerBlock
    {
        public CostumeAkos(BlockBase parent, string blockType) : base(parent, blockType) { }
    }
}
