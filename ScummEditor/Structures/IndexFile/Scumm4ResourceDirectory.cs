using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.IndexFile
{
    /*
    A SCUMM v4 resource directory block in 000.LFL: 0S (scripts), 0N (sounds) or 0C (costumes).

    Layout (small header [size:4 LE][tag:2], then):
        count   : 2 bytes LE
        entries : count x [ roomNumber:1 ][ offset:4 LE ]   (interleaved, stride 5)

    The resource id is the entry index (slot 0..count-1). roomNumber is the room that holds the
    resource; offset is measured from that room's RO block (RO = LF + 8). This matches ScummVM
    readResTypeList (resource_v4.cpp) and loadResource (resource.cpp: seek fileOffs + roomBase).

    0R (rooms: room offsets are always 0, located via each disk's FO) and 0O (objects: no offsets)
    do NOT need typing and are kept verbatim.
    */
    public class Scumm4ResourceDirectory : BlockBase
    {
        private readonly string _blockType;

        public Scumm4ResourceDirectory(BlockBase blockBase, string blockType, GameInfo gameInfo)
            : base(blockBase, gameInfo)
        {
            _blockType = blockType;
            Entries = new List<Scumm4DirectoryEntry>();
        }

        public List<Scumm4DirectoryEntry> Entries { get; private set; }

        public override string BlockType
        {
            get { return _blockType; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)(2 + Entries.Count * 5); // count word + 5 bytes per entry
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Entries = new List<Scumm4DirectoryEntry>();
            int count = binaryReader.ReadUint16();
            for (int i = 0; i < count; i++)
            {
                var entry = new Scumm4DirectoryEntry();
                entry.RoomNumber = binaryReader.ReadByte1();
                entry.Offset = binaryReader.ReadUint32();
                Entries.Add(entry);
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Write((ushort)Entries.Count);
            foreach (Scumm4DirectoryEntry entry in Entries)
            {
                binaryWriter.Write(entry.RoomNumber);
                binaryWriter.Write(entry.Offset);
            }
        }
    }

    /// <summary>
    /// One entry of a v4 resource directory. Besides the stored room/offset, it carries a link to
    /// the tree block that holds the resource (resolved once at load), so the offset can be
    /// recomputed from that block's new position after edits change resource sizes.
    /// </summary>
    public class Scumm4DirectoryEntry
    {
        public byte RoomNumber { get; set; }
        public uint Offset { get; set; }

        /// <summary>UniqueId of the deepest tree block that contains this resource's bytes (or null if unlinked).</summary>
        public string ContainingBlockId { get; set; }

        /// <summary>Offset of the resource within that containing block.</summary>
        public uint OffsetWithinBlock { get; set; }
    }
}
