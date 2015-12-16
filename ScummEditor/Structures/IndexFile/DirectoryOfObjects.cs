using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.IndexFile
{
    public class DirectoryOfObjects : BlockBase
    {
        /*
        DOBJ - Directory of Objects
        ----
        Block Name	  (4 bytes)
        Block Size	  (4 bytes BE)
        No of items	  (2 bytes)
         *Owner/State (1 byte)
            owner (4 bits)
            state (4 bits)
         *Class Data (4 bytes)
        */
        public DirectoryOfObjects(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo)
        {
            Owners = new List<DirectoryObject>();
        }

        public ushort NumOfItems { get; set; }
        public List<DirectoryObject> Owners { get; set; }

        public override string BlockType
        {
            get { return "DOBJ"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            NumOfItems = binaryReader.ReadUint16();

            Owners = new List<DirectoryObject>();

            for (int i = 0; i < NumOfItems; i++)
            {
                var directoryObject = new DirectoryObject();

                byte ownerStateByte = binaryReader.ReadByte1();

                directoryObject.Owner = BinaryHelper.GetBitsFromByte(ownerStateByte, 4);
                directoryObject.State = BinaryHelper.GetBitsFromByte(ownerStateByte, 4, 4);
                directoryObject.ClassData = binaryReader.ReadUint32();

                Owners.Add(directoryObject);
            }

        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Write(NumOfItems);

            for (int index = 0; index < Owners.Count; index++)
            {
                DirectoryObject directoryObject = Owners[index];
                var finalByte = BinaryHelper.Compose2Bytes(directoryObject.State, directoryObject.Owner, 4);
                binaryWriter.Write(finalByte);

                binaryWriter.Write(directoryObject.ClassData);
            }
        }
    }

    public struct DirectoryObject
    {
        public byte Owner { get; set; }
        public byte State { get; set; }
        public uint ClassData { get; set; }
    }

}