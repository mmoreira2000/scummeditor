using System.IO;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// RMSC - the SCUMM v8 "room scripts" block (The Curse of Monkey Island). v8 moved a room's scripts
    /// and object code OUT of the ROOM block into this sibling block inside the LFLF: it holds the room's
    /// entry/exit scripts (ENCD/EXCD), local scripts (LSCR) and object code (OBCD). Each child is read with
    /// the right typed block (ScriptBlock for the scripts so the text pipeline finds them; ObjectCode for
    /// OBCD) and keeps its bytes verbatim, so the block rebuilds byte-for-byte. Anything unexpected is kept
    /// as a generic container, so the file still round-trips exactly.
    /// </summary>
    public class RoomScriptsBlock : BlockBase
    {
        public RoomScriptsBlock(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "RMSC"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            long endPosition = binaryReader.Position - 8 + BlockSize;
            while (binaryReader.Position < endPosition)
            {
                string tag = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
                switch (tag)
                {
                    case "ENCD":
                    case "EXCD":
                    case "LSCR":
                        var script = new ScriptBlock(this, tag);
                        script.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(script);
                        break;

                    case "OBCD":
                        var obcd = new ObjectCode(this);
                        obcd.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(obcd);
                        break;

                    default:
                        var raw = new RawContainerBlock(this, tag);
                        raw.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(raw);
                        break;
                }
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
        }
    }
}
