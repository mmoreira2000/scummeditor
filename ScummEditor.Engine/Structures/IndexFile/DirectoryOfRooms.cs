using System;
using System.Collections.Generic;

namespace ScummEditor.Structures.IndexFile
{
    public class DirectoryOfRooms : DirectoryOfItems
    {
        public DirectoryOfRooms(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override string BlockType
        {
            get { return "DROO"; }
        }
    }
}