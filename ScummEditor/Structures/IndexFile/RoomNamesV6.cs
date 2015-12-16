using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Exceptions;

namespace ScummEditor.Structures.IndexFile
{
    public class RoomNamesV6 : BlockBase
    {
        public List<RoomName> Rooms { get; set; }

        public RoomNamesV6(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo)
        {
            Rooms=new List<RoomName>();
        }

        /*
        RNAM - Room Names
        ----
        Block Name	  (4 bytes)
        Block Size	  (4 bytes BE)
            #Room No	  (1 byte)     <--- SCUMM V5 only
            #Room Name	  (9 bytes) XOR'ed with FF    <--- SCUMM V5 only
        Blank Byte(00)	(1 byte)
        */
        public byte BlankByte { get; set; }

        public override string BlockType
        {
            get { return "RNAM"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            switch (_gameInfo.ScummVersion)
            {
                case 5:
                    LoadScummV5(binaryReader);
                    break;
                case 6:
                    LoadScummV6(binaryReader);
                    break;
            }
        }


        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            switch (_gameInfo.ScummVersion)
            {
                case 5:
                    SaveScummV5(binaryWriter);
                    break;
                case 6:
                    SaveScummV6(binaryWriter);
                    break;
            }
        }

        private void SaveScummV5(Stream binaryWriter)
        {
            foreach (RoomName room in Rooms)
            {
                binaryWriter.Write(room.RoomNumber);
                binaryWriter.Write(room.RoomNameData);
            }
            binaryWriter.Write(BlankByte);
        }

        private void SaveScummV6(Stream binaryWriter)
        {
            binaryWriter.Write(BlankByte);
        }

        private void LoadScummV5(Stream binaryReader)
        {
            BlankByte = binaryReader.ReadByte1();
            while (BlankByte != 0)
            {
                var room = new RoomName();
                room.RoomNumber = BlankByte;
                room.RoomNameData = binaryReader.ReadBytes(9);
                Rooms.Add(room);

                BlankByte = binaryReader.ReadByte1();
            }
        }

        private void LoadScummV6(Stream binaryReader)
        {
            BlankByte = binaryReader.ReadByte1();
            if (BlankByte != 0)
            {
                throw new InvalidFileFormatException("Sequencia de caracteres não esperada.");
            }
        }
    }

    public class RoomName
    {
        public byte RoomNumber { get; set; }

        private byte[] _roomNameData;
        private string _roomName;
        public byte[] RoomNameData
        {
            get { return _roomNameData; }
            set
            {
                _roomNameData = value;

                _roomName = BinaryHelper.ConvertByteArrayToUTF8String(_roomNameData.Where(b => b != 255).Select(xb => (byte)(xb ^ 0xFF)).ToArray());
            }
        }
    }
}