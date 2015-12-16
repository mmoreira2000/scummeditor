namespace ScummEditor.Structures.IndexFile
{
    public class DirectoryOfScripts : DirectoryOfItems
    {
        public DirectoryOfScripts(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override string BlockType
        {
            get { return "DSCR"; }
        }
    }
}