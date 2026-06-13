using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /*
    SCUMM v4 data container (DISKnn.LEC, one file per floppy disk; XOR 0x69).

    Layout (small header: [size:4 LE][tag:2 ascii], size includes the 6-byte header):
        LE                      whole-file wrapper (= v5/v6 LECF)
          FO                    room offset table for this disk (= v5/v6 LOFF)
          LF x N                one per room (= v5/v6 LFLF); a 2-byte room number follows the header
            RO                  the room (= v5/v6 ROOM)
            SC x n              room/global scripts
            SO                  sound

    For now every block is kept byte-for-byte (RawContent) and only the structural containers
    (LE / LF / RO) are walked, so the file round-trips identically. Typed parsing of the inner
    blocks is layered on top in later steps.
    */
    public class Scumm4DataFile : ScummV6DataFile
    {
        public Scumm4DataFile(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        public override string BlockType
        {
            get { return "LE"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            ReadBlockHeader(binaryReader);
            Scumm4Blocks.WalkChildren(this, binaryReader, BlockOffSet + BlockSize);
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            WriteBlockHeader(binaryWriter);
            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
            binaryWriter.Flush();
        }
    }

    /// <summary>A v4 room container (LF). Begins with a 2-byte room number, then the room blocks.</summary>
    public class Scumm4DiskBlock : BlockBase
    {
        public Scumm4DiskBlock(BlockBase blockBase) : base(blockBase) { }

        public ushort RoomNumber { get; set; }

        public override string BlockType
        {
            get { return "LF"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            ReadBlockHeader(binaryReader);
            RoomNumber = binaryReader.ReadUint16(false);
            Scumm4Blocks.WalkChildren(this, binaryReader, BlockOffSet + BlockSize);
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += 2; // the room-number word that precedes the child blocks
        }

        public override void CalculateOffsets()
        {
            long nextOffSet = BlockOffSet + HeaderLength + 2;
            foreach (BlockBase child in Childrens)
            {
                nextOffSet = ConfigureAndReturnNextOffset(child, nextOffSet);
                child.CalculateOffsets();
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            WriteBlockHeader(binaryWriter);
            binaryWriter.Write(RoomNumber, false);
            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
        }
    }

    /// <summary>A v4 room (RO): a sequence of room sub-blocks (HD, CC, BX, PA, BM, OI, OC, EX, EN, LS, ...).</summary>
    public class Scumm4RoomBlock : BlockBase
    {
        public Scumm4RoomBlock(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "RO"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            ReadBlockHeader(binaryReader);
            Scumm4Blocks.WalkChildren(this, binaryReader, BlockOffSet + BlockSize);
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            WriteBlockHeader(binaryWriter);
            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
        }
    }

    /// <summary>Shared walk for the v4 containers: read child blocks until the parent block ends.</summary>
    public static class Scumm4Blocks
    {
        public static void WalkChildren(BlockBase parent, Stream binaryReader, long endPosition)
        {
            while (binaryReader.Position < endPosition)
            {
                // Not every byte in a container is a self-describing block: a room's sound
                // region is raw resource data whose block size does not tile to the parent end.
                // When the next header is not a plausible block, keep the rest of the parent
                // verbatim so the file still round-trips exactly.
                if (!LooksLikeBlock(binaryReader, parent.GameInfo, endPosition))
                {
                    var raw = new RawDataBlock(parent, (int)(endPosition - binaryReader.Position));
                    raw.LoadFromBinaryReader(binaryReader);
                    parent.Childrens.Add(raw);
                    break;
                }

                string tag = BlockBase.PeekTag(binaryReader, parent.GameInfo);
                BlockBase child = CreateChild(parent, tag);
                child.LoadFromBinaryReader(binaryReader);
                parent.Childrens.Add(child);
            }
        }

        private static BlockBase CreateChild(BlockBase parent, string tag)
        {
            switch (tag)
            {
                case "LF":
                    return new Scumm4DiskBlock(parent);
                case "RO":
                    return new Scumm4RoomBlock(parent);
                case "HD": // room header (= v5/v6 RMHD)
                    return new RoomHeader(parent);
                case "PA": // room palette (= v5/v6 CLUT)
                    return new PaletteData(parent, "PA");
                default:
                    return new NotImplementedDataBlock(parent, tag);
            }
        }

        /// <summary>
        /// True when the bytes at the current position form a plausible v4 block header: a size
        /// that fits within the parent and a 2-character uppercase/digit tag.
        /// </summary>
        private static bool LooksLikeBlock(Stream binaryReader, GameInfo gameInfo, long endPosition)
        {
            if (binaryReader.Position + 6 > endPosition)
            {
                return false;
            }

            byte[] head = binaryReader.PeekBytes(6);
            uint size = (uint)(head[0] | (head[1] << 8) | (head[2] << 16) | (head[3] << 24));

            if (size < 6 || binaryReader.Position + size > endPosition)
            {
                return false;
            }
            return IsTagByte(head[4]) && IsTagByte(head[5]);
        }

        private static bool IsTagByte(byte b)
        {
            return (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'A' && b <= (byte)'Z');
        }
    }

    /// <summary>
    /// A run of raw bytes inside a v4 container that is not a self-describing block (e.g. the
    /// trailing sound data of a room). It has no header of its own and is kept verbatim.
    /// </summary>
    public class RawDataBlock : BlockBase
    {
        private readonly int _length;
        public byte[] Contents { get; set; }

        public RawDataBlock(BlockBase blockBase, int length) : base(blockBase)
        {
            _length = length;
        }

        public override string BlockType
        {
            get { return "(raw)"; }
        }

        public override void CalculateBlockSize()
        {
            BlockSize = (uint)Contents.Length; // pure data: no header
        }

        public override void CalculateOffsets()
        {
            // no children
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            BlockOffSet = binaryReader.Position;
            Contents = binaryReader.ReadBytes(_length);
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            binaryWriter.WriteBytes(Contents);
        }
    }
}
