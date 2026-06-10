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

                    case "SOUN":
                        // Only decode SOUN on v6; v5 (e.g. MI2 talkie) keeps the generic
                        // block so its byte-preserving quirks stay untouched.
                        if (_gameInfo.ScummVersion == 6)
                        {
                            var soundBlock = new SoundBlock(this);
                            soundBlock.LoadFromBinaryReader(binaryReader);
                            Childrens.Add(soundBlock);
                        }
                        else
                        {
                            var soundDefault = new NotImplementedDataBlock(this, typeRead);
                            soundDefault.LoadFromBinaryReader(binaryReader);
                            Childrens.Add(soundDefault);
                        }
                        break;

                    case "SCRP":
                        // v6 only: typed script block (disassemblable). v5 stays generic.
                        if (_gameInfo.ScummVersion == 6)
                        {
                            var scriptBlock = new ScriptBlock(this, "SCRP");
                            scriptBlock.LoadFromBinaryReader(binaryReader);
                            Childrens.Add(scriptBlock);
                        }
                        else
                        {
                            var scriptDefault = new NotImplementedDataBlock(this, typeRead);
                            scriptDefault.LoadFromBinaryReader(binaryReader);
                            Childrens.Add(scriptDefault);
                        }
                        break;

                    case "CHAR":
                        // v6 only: typed charset/font block (glyph viewer). v5 stays generic.
                        if (_gameInfo.ScummVersion == 6)
                        {
                            var charsetBlock = new Charset(this);
                            charsetBlock.LoadFromBinaryReader(binaryReader);
                            Childrens.Add(charsetBlock);
                        }
                        else
                        {
                            var charsetDefault = new NotImplementedDataBlock(this, typeRead);
                            charsetDefault.LoadFromBinaryReader(binaryReader);
                            Childrens.Add(charsetDefault);
                        }
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