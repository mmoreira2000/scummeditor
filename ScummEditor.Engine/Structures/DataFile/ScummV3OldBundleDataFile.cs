using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    SCUMM v3 "old bundle" (GF_OLD_BUNDLE) room container - one NN.LFL file per room (Loom EGA,
    Indy3 EGA, Zak DOS). The whole file is XOR 0xFF (handled by the load stream), and the body is
    an UNTAGGED chain of chunks that tile from offset 0 to EOF:

        [size:uint16 LE][payload]   size = the whole chunk (including the 2-byte size word)

    The first chunk is the room; the following chunks are its scripts/sounds/costumes, located only
    by the index's room-number + offset (there are NO ascii tags). The room chunk itself is a fixed
    binary struct (width@+4, height@+6, IM00@+0x0A, numObjects@+20, ...) parsed by the typed readers
    layered on top in later steps.

    Round-trip strategy (matches SoundBlockV4): keep the decrypted bytes verbatim in RawContent and
    write them back unchanged, so the file round-trips byte-for-byte; expose the chunk boundaries as
    a navigation overlay so the room/resource readers can find each chunk without re-tagging anything.
    */
    public class ScummV3OldBundleDataFile : ScummV5V6DataFile
    {
        public ScummV3OldBundleDataFile(BlockBase blockBase, GameInfo gameInfo) : base(blockBase, gameInfo) { }

        /// <summary>The whole (decrypted) room file. Written back verbatim on an unedited save.</summary>
        public byte[] RawContent { get; private set; }

        /// <summary>The chunk boundaries (offset+size into RawContent); chunk 0 is the room.</summary>
        public List<V3OldChunk> Chunks { get; private set; }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            BlockOffSet = binaryReader.Position;
            RawContent = binaryReader.ReadBytes((int)(binaryReader.Length - binaryReader.Position));
            Chunks = ParseChunks(RawContent);
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            binaryWriter.WriteBytes(RawContent);
            binaryWriter.Flush();
        }

        public override void CalculateBlockSize()
        {
            BlockSize = (uint)(RawContent == null ? 0 : RawContent.Length);
        }

        public override void CalculateOffsets()
        {
            // A verbatim file has no child blocks to position.
        }

        /// <summary>The room chunk (chunk 0), or null if the file is empty/malformed.</summary>
        public V3OldChunk GetRoomChunk()
        {
            return (Chunks != null && Chunks.Count > 0) ? Chunks[0] : null;
        }

        /// <summary>Walks the [size:uint16 LE] chunk chain from offset 0 to the end of the buffer.</summary>
        private static List<V3OldChunk> ParseChunks(byte[] data)
        {
            var chunks = new List<V3OldChunk>();
            int p = 0;
            while (p + 2 <= data.Length)
            {
                int size = data[p] | (data[p + 1] << 8);
                if (size < 2 || p + size > data.Length)
                {
                    // Trailing/malformed data: keep the remainder as one final chunk so nothing is lost.
                    chunks.Add(new V3OldChunk(p, data.Length - p));
                    break;
                }
                chunks.Add(new V3OldChunk(p, size));
                p += size;
            }
            return chunks;
        }
    }

    /// <summary>One untagged [size:uint16][payload] chunk inside a v3 old-bundle room file.</summary>
    public class V3OldChunk
    {
        public V3OldChunk(int offset, int size)
        {
            Offset = offset;
            Size = size;
        }

        /// <summary>Position of the chunk (its 2-byte size word) within the room file's RawContent.</summary>
        public int Offset { get; private set; }

        /// <summary>Total chunk size in bytes, including the 2-byte size word.</summary>
        public int Size { get; private set; }
    }
}
