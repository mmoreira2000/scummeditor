namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// A SCUMM v7 SOUN sound resource (The Dig, Full Throttle). Structurally it is just the generic IFF
    /// container - The Dig wraps an iMUS digital-audio resource (MAP/FRMT/DATA), Full Throttle stores a
    /// Creative Voice File (VOC) - so it reuses RawContainerBlock unchanged for a byte-exact round-trip.
    /// The distinct type exists only so the GUI routes it to the v7 sound viewer (which decodes the iMUS
    /// PCM or the VOC to WAV for preview/export) instead of the generic hex view - the same pattern the
    /// v5/v6 SoundBlock and the v7 CostumeAkos / Charset blocks use.
    /// </summary>
    public class SoundBlockV7 : RawContainerBlock
    {
        public SoundBlockV7(BlockBase parent, string blockType) : base(parent, blockType) { }
    }
}
