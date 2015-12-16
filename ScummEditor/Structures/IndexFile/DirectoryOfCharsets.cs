namespace ScummEditor.Structures.IndexFile
{
    public class DirectoryOfCharsets : DirectoryOfItems
    {
        public DirectoryOfCharsets(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override string BlockType
        {
            get { return "DCHR"; }
        }
    }
}