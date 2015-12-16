using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.IndexFile
{
    public class DirectoryOfArrays : BlockBase
    {
        /*
        vlc (end code 0x0000)   (vlc = Variable Length Code // indica um loop, sendo o codigo de finalização o 0x0000)
            var no      : 16le
            x size -1   : 16le
            y size -1   : 16le
            type        : 16le (0: words, 1: bytes)
        */
        public DirectoryOfArrays(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo)
        {
            Items = new List<DirectoryArray>();
        }

        public List<DirectoryArray> Items { get; set; }
        public ushort Stop { get; set; }

        public override string BlockType
        {
            get { return "AARY"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            ushort verifyNumber = binaryReader.ReadUint16();

            Items=new List<DirectoryArray>();

            while (verifyNumber != 0)
            {
                var arrayItem = new DirectoryArray();
                arrayItem.VariableNumber = verifyNumber;
                arrayItem.XSize = binaryReader.ReadUint16();
                arrayItem.YSize = binaryReader.ReadUint16();
                arrayItem.Type = binaryReader.ReadUint16();

                Items.Add(arrayItem);

                verifyNumber = binaryReader.ReadUint16();
            }

            Stop = verifyNumber;
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (DirectoryArray item in Items)
            {
                binaryWriter.Write(item.VariableNumber);
                binaryWriter.Write(item.XSize);
                binaryWriter.Write(item.YSize);
                binaryWriter.Write(item.Type);
            }

            binaryWriter.Write(Stop);
        }
    }


    public struct DirectoryArray
    {
        public ushort VariableNumber { get; set; }
        public ushort XSize { get; set; }
        public ushort YSize { get; set; }
        public ushort Type { get; set; }
    }

}