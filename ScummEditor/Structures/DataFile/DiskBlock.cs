using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    //LFLF - Disk Block
    //  ROOM - Room Block
    //  SCRP - Script Block
    //  SOUN - Sound Block
    //  COST - Costume Block
    //  CHAR - Charset
    public class DiskBlock : BlockBase
    {
        public DiskBlock(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "LFLF"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            var ROOM = new RoomBlock(this);
            ROOM.LoadFromBinaryReader(binaryReader);
            Childrens.Add(ROOM);

            var blocosPossiveis = new [] { "SCRP", "SOUN", "COST", "CHAR"};

            string typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
            while (blocosPossiveis.Any(x => x.Equals(typeRead)))
            {
                switch (typeRead)
                {
                    case "COST":
                        var costumeBlock = new Costume(this);
                        costumeBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(costumeBlock);
                        break;

                    default:
                        var Default = new NotImplementedDataBlock(this, typeRead);
                        Default.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(Default);
                        break;
                }

                typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
            }

            /*
            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "SCRP")
            {
                var scriptBlock = new NotImplementedDataBlock(this, "SCRP");
                scriptBlock.LoadFromBinaryReader(binaryReader);
                Childrens.Add(scriptBlock);
            }

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "SOUN")
            {
                var soundBlock = new NotImplementedDataBlock(this, "SOUN");
                soundBlock.LoadFromBinaryReader(binaryReader);
                Childrens.Add(soundBlock);
            }

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "COST")
            {
                var costumeBlock = new Costume(this);
                costumeBlock.LoadFromBinaryReader(binaryReader);
                Childrens.Add(costumeBlock);
            }

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "CHAR")
            {
                var charsetBlock = new NotImplementedDataBlock(this, "CHAR");
                charsetBlock.LoadFromBinaryReader(binaryReader);
                Childrens.Add(charsetBlock);
            }
             */
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (var child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
        }

        public RoomBlock GetROOM()
        {
            return (RoomBlock)Childrens.Single(x => x.GetType() == typeof(RoomBlock));
        }

        public List<Costume> GetCostumes()
        {
            return Childrens.OfType<Costume>().ToList();
        }
    }
}