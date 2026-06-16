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
    public class ScummV4ResourceDirectory : BlockBase
    {
        private readonly string _blockType;

        public ScummV4ResourceDirectory(BlockBase blockBase, string blockType, GameInfo gameInfo)
            : base(blockBase, gameInfo)
        {
            _blockType = blockType;
            Entries = new List<ScummV4DirectoryEntry>();
        }

        public List<ScummV4DirectoryEntry> Entries { get; private set; }

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

            Entries = new List<ScummV4DirectoryEntry>();
            int count = binaryReader.ReadUint16();
            for (int i = 0; i < count; i++)
            {
                var entry = new ScummV4DirectoryEntry();
                entry.RoomNumber = binaryReader.ReadByte1();
                entry.Offset = binaryReader.ReadUint32();
                Entries.Add(entry);
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Write((ushort)Entries.Count);
            foreach (ScummV4DirectoryEntry entry in Entries)
            {
                binaryWriter.Write(entry.RoomNumber);
                binaryWriter.Write(entry.Offset);
            }
        }
    }
}
