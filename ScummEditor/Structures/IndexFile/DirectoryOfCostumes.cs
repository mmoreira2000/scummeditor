namespace ScummEditor.Structures.IndexFile
{
    public class DirectoryOfCostumes : DirectoryOfItems
    {
        public DirectoryOfCostumes(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override string BlockType
        {
            get { return "DCOS"; }
        }
    }
}