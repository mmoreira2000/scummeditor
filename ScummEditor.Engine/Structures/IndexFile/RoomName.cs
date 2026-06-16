using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Exceptions;

namespace ScummEditor.Structures.IndexFile
{
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

        /// <summary>Decoded room name (the on-disk bytes are XOR'ed with 0xFF).</summary>
        public string Name
        {
            get { return _roomName; }
        }
    }
}
