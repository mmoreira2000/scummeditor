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

            // The LFLF children end where the LFLF block ends; unknown block types are kept
            // as byte-preserved generic blocks (some fan editions pack stray blocks here).
            long endPosition = binaryReader.Position - 8 + BlockSize;

            var ROOM = new RoomBlock(this);
            ROOM.LoadFromBinaryReader(binaryReader);
            Childrens.Add(ROOM);

            while (binaryReader.Position < endPosition)
            {
                string typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
                switch (typeRead)
                {
                    case "COST":
                        var costumeBlock = new Costume(this);
                        costumeBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(costumeBlock);
                        break;

                    case "SOUN":
                        // The sound block keeps its original bytes and only parses for display,
                        // so it is safe on both v5 (iMUSE in MI2/Indy4) and v6.
                        var soundBlock = new SoundBlock(this);
                        soundBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(soundBlock);
                        break;

                    case "SCRP":
                        // Typed script block; the disassembler is picked by SCUMM version.
                        var scriptBlock = new ScriptBlock(this, "SCRP");
                        scriptBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(scriptBlock);
                        break;

                    case "CHAR":
                        // The charset format (color map + offsets + glyphs) is the same on v5 and v6.
                        var charsetBlock = new Charset(this);
                        charsetBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(charsetBlock);
                        break;

                    default:
                        var Default = new NotImplementedDataBlock(this, typeRead);
                        Default.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(Default);
                        break;
                }
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