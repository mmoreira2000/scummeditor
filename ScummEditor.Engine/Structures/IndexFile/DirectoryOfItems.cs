using System;
using System.Collections.Generic;

namespace ScummEditor.Engine.Structures.IndexFile
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

        /// <summary>
        /// SCUMM v8 (The Curse of Monkey Island) widened the item-count field of every index directory
        /// from a 2-byte to a 4-byte little-endian integer (the rest of the body is unchanged). The real
        /// counts still fit in a ushort, so <see cref="NumOfItems"/> stays a ushort and only the on-disk
        /// width changes per version - keeping the byte-identical round-trip on both v4-v7 and v8.
        /// </summary>
        private bool UsesWideCount
        {
            get { return _gameInfo != null && _gameInfo.ScummVersion == 8; }
        }

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Rooms = new List<DirectoryItem>();
            if (UsesWideCount)
            {
                NumOfItems = (ushort)binaryReader.ReadUint32();
            }
            else
            {
                NumOfItems = binaryReader.ReadUint16();
            }
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

            if (UsesWideCount)
            {
                binaryWriter.Write((uint)NumOfItems);
            }
            else
            {
                binaryWriter.Write(NumOfItems);
            }

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