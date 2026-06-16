using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Structures.IndexFile
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
    public class ScummV4IndexFile : ScummV5V6IndexFile
    {
        public List<BlockBase> Blocks { get; private set; }

        /// <summary>The script directory (0S); offsets are room-relative. Null if absent.</summary>
        public ScummV4ResourceDirectory ScriptDirectory { get; private set; }
        /// <summary>The sound directory (0N); offsets are room-relative. Null if absent.</summary>
        public ScummV4ResourceDirectory SoundDirectory { get; private set; }
        /// <summary>The costume directory (0C); offsets are room-relative. Null if absent.</summary>
        public ScummV4ResourceDirectory CostumeDirectory { get; private set; }

        public ScummV4IndexFile(GameInfo gameInfo) : base(gameInfo)
        {
            Blocks = new List<BlockBase>();
        }

        /// <summary>The directories whose offsets must be recomputed after edits (0S, 0N, 0C).</summary>
        public IEnumerable<ScummV4ResourceDirectory> ResourceDirectories
        {
            get
            {
                if (ScriptDirectory != null) yield return ScriptDirectory;
                if (SoundDirectory != null) yield return SoundDirectory;
                if (CostumeDirectory != null) yield return CostumeDirectory;
            }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            Blocks = new List<BlockBase>();

            while (binaryReader.Position < binaryReader.Length)
            {
                string tag = BlockBase.PeekTag(binaryReader, GameInfo);

                // 0S/0N/0C carry room-relative resource offsets that shift when resources are
                // edited, so they are typed and rewritten. RN/0R/0O have no movable offsets
                // (0R room offsets are always 0; 0O has none) and are kept verbatim.
                if (tag == "0S" || tag == "0N" || tag == "0C")
                {
                    var directory = new ScummV4ResourceDirectory(null, tag, GameInfo);
                    directory.LoadFromBinaryReader(binaryReader);
                    Blocks.Add(directory);

                    if (tag == "0S") ScriptDirectory = directory;
                    else if (tag == "0N") SoundDirectory = directory;
                    else CostumeDirectory = directory;
                }
                else
                {
                    var block = new NotImplementedDataBlock(null, tag, GameInfo);
                    block.LoadFromBinaryReader(binaryReader);
                    Blocks.Add(block);
                }
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
