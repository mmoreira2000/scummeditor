using System.Collections.Generic;

namespace ScummEditor.Structures.DataFile
{
    public class RoomOffsetTable : BlockBase
    {
        //LOFF - Room Offset Table
        //num rooms: 1 byte
        //  room id: 1 byte
        //  offset : 4 bytes (absolute offset of the ROOM block)
        public RoomOffsetTable(BlockBase blockBase) : base(blockBase)
        {
            Rooms = new List<RoomOffsetTableItem>();
        }
        public byte NumOfRooms { get; set; }
        public List<RoomOffsetTableItem> Rooms { get; set; }

        public override string BlockType
        {
            get { return "LOFF"; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();

            uint block = 1;
            block+= (uint)(5 * Rooms.Count);

            BlockSize += block;
        }

        #region Save & Load

        public override void LoadFromBinaryReader(System.IO.Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            NumOfRooms = binaryReader.ReadByte1();

            Rooms = new List<RoomOffsetTableItem>();
            for (int i = 0; i < NumOfRooms; i++)
            {
                var room = new RoomOffsetTableItem();
                room.Id = binaryReader.ReadByte1();
                room.OffSet = binaryReader.ReadUint32();

                Rooms.Add(room);
            }
        }

        public override void SaveToBinaryWriter(System.IO.Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.WriteByte(NumOfRooms);

            foreach (var room in Rooms)
            {
                binaryWriter.WriteByte(room.Id);
                binaryWriter.Write(room.OffSet);
            }
        }

        #endregion
    }

    public class RoomOffsetTableItem
    {
        public byte Id { get; set; }
        public uint OffSet { get; set; }
    }

}