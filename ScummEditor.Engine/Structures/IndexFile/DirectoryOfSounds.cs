namespace ScummEditor.Structures.IndexFile
{
    public class DirectoryOfSounds : DirectoryOfItems
    {
        public DirectoryOfSounds(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override string BlockType
        {
            get { return "DSOU"; }
        }
    }
}