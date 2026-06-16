using System;
using System.Collections.Generic;

namespace ScummEditor.Structures.IndexFile
{
    public abstract class DirectoryOfItems : BlockBase
    {
        /*
        DROO - Directory of Rooms
        DSCR - Directory of Scripts
        DSOU - Directory of Sounds
        DCOS - Directory of Costumes
        DCHR - Directory of Charsets
        ----
        Block Name	(4 bytes)
        Block Size	(4 bytes BE)
        No of items	(2 bytes)
         *Room Number	(1 byte)
         *Offset	(4 bytes)
        */
        public DirectoryOfItems(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo)
        {
            Rooms = new List<DirectoryItem>();
        }

        public ushort NumOfItems { get; set; }

        public List<DirectoryItem> Rooms { get; set; }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Rooms = new List<DirectoryItem>();
            NumOfItems = binaryReader.ReadUint16();
            for (int i = 0; i < NumOfItems; i++)
            {
                var room = new DirectoryItem();
                room.Number = binaryReader.ReadByte1();
                Rooms.Add(room);
            }
            for (int i = 0; i < NumOfItems; i++)
            {
                Rooms[i].Offset = binaryReader.ReadUint32();
            }

        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Write(NumOfItems);

            foreach (DirectoryItem item in Rooms)
            {
                binaryWriter.Write(item.Number);
            }
            foreach (DirectoryItem item in Rooms)
            {
                binaryWriter.Write(item.Offset);
            }

        }
    }
}