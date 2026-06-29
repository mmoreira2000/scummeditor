namespace ScummEditor.Engine.Structures.IndexFile
{
    /// <summary>
    /// Directory of room scripts (DRSC) - a SCUMM v8-only index block (The Curse of Monkey Island). v8
    /// moved each room's scripts/objects out of the ROOM block into a sibling RMSC block, and DRSC is the
    /// directory that points at those RMSC blocks. Its on-disk layout is the same [count][room numbers]
    /// [offsets] as every other v8 directory (with the v8 4-byte count), so it reuses
    /// <see cref="DirectoryOfItems"/> unchanged.
    /// </summary>
    public class DirectoryOfRoomScripts : DirectoryOfItems
    {
        public DirectoryOfRoomScripts(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override string BlockType
        {
            get { return "DRSC"; }
        }
    }
}
