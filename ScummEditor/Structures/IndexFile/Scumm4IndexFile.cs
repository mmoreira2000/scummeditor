using System.Collections.Generic;
using System.IO;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Structures.IndexFile
{
    /*
    SCUMM v4 index file (000.LFL; plaintext - the block headers are not XOR-encrypted, although
    the room-name strings inside the RN block are individually XOR 0xFF).

    It is a flat sequence of small-header blocks ([size:4 LE][tag:2 ascii]) until end of file:
        RN   room names      ([room#:1][name:9 XOR 0xFF], terminated by room#==0)
        0R   directory of rooms     ([count:2 LE] then count x [room#:1][offset:4 LE])
        0S   directory of scripts
        0N   directory of sounds
        0C   directory of costumes
        0O   directory of objects   ([count:2 LE] then count x [class:3 LE][owner/state:1])

    For now the six blocks are kept byte-for-byte so the index round-trips identically; typed
    parsing (room names, directory entries) is layered on top in later steps.
    */
    public class Scumm4IndexFile : ScummV6IndexFile
    {
        public List<BlockBase> Blocks { get; private set; }

        public Scumm4IndexFile(GameInfo gameInfo) : base(gameInfo)
        {
            Blocks = new List<BlockBase>();
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            Blocks = new List<BlockBase>();

            while (binaryReader.Position < binaryReader.Length)
            {
                string tag = BlockBase.PeekTag(binaryReader, GameInfo);
                var block = new NotImplementedDataBlock(null, tag, GameInfo);
                block.LoadFromBinaryReader(binaryReader);
                Blocks.Add(block);
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            foreach (BlockBase block in Blocks)
            {
                block.SaveToBinaryWriter(binaryWriter);
            }
            binaryWriter.Flush();
        }
    }
}
