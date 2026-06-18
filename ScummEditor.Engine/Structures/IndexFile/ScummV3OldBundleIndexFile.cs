using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures.IndexFile
{
    /*
    SCUMM v3 "old bundle" index (00.LFL; whole file XOR 0xFF, handled by the load stream). Unlike the
    v4 tagged 0R/0S/0N/0C/0O blocks, it is a fixed-layout magic directory:

        [magic:uint16 LE = 0x0100]
        [numGlobalObjects:uint16 LE]
        [object table: numGlobalObjects x 4 bytes]      (class:3 LE + owner/state:1)
        ROOM    directory  [count:uint8][count roomno bytes (filler)][count x offset:uint16 LE]
        COSTUME directory  [count:uint8][count roomno bytes][count x offset:uint16 LE]
        SCRIPT  directory  [count:uint8][count roomno bytes][count x offset:uint16 LE]
        SOUND   directory  [count:uint8][count roomno bytes][count x offset:uint16 LE]

    A resource is located by its directory entry: (roomNumber, offset) -> the byte at that offset in
    that room's NN.LFL. Round-trip strategy: keep the decrypted bytes verbatim and write them back
    unchanged; the typed directories below are a navigation/edit overlay that records, per entry, the
    room number, the offset, and the byte position of the offset word (so a future edit can rewrite it).
    */
    public class ScummV3OldBundleIndexFile : ScummV5V6IndexFile
    {
        public ScummV3OldBundleIndexFile(GameInfo gameInfo) : base(gameInfo) { }

        /// <summary>The whole (decrypted) index file; written back verbatim on an unedited save.</summary>
        public byte[] RawContent { get; private set; }

        public V3OldResourceDirectory RoomDirectory { get; private set; }
        public V3OldResourceDirectory CostumeDirectory { get; private set; }
        public V3OldResourceDirectory ScriptDirectory { get; private set; }
        public V3OldResourceDirectory SoundDirectory { get; private set; }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            RawContent = binaryReader.ReadBytes((int)(binaryReader.Length - binaryReader.Position));
            ParseDirectories();
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            binaryWriter.WriteBytes(RawContent);
            binaryWriter.Flush();
        }

        private void ParseDirectories()
        {
            try
            {
                int p = 2; // skip magic 0x0100
                int numObjects = RawContent[p] | (RawContent[p + 1] << 8);
                // Object-table stride is 4 bytes/object for v3 (Loom/Indy3 EGA) but 1 byte/object for
                // v1/v2 (Maniac/Zak); reading the wrong stride walks every directory off-position.
                int objectEntrySize = GameInfo != null && GameInfo.GlobalObjectEntrySize > 0 ? GameInfo.GlobalObjectEntrySize : 4;
                p += 2 + numObjects * objectEntrySize;

                RoomDirectory = ReadDirectory(RawContent, ref p);
                CostumeDirectory = ReadDirectory(RawContent, ref p);
                ScriptDirectory = ReadDirectory(RawContent, ref p);
                SoundDirectory = ReadDirectory(RawContent, ref p);
            }
            catch (System.IndexOutOfRangeException)
            {
                // Leave the typed overlay null on a malformed index; RawContent still round-trips.
            }
        }

        private static V3OldResourceDirectory ReadDirectory(byte[] data, ref int p)
        {
            int count = data[p];
            p += 1;

            var roomNumbers = new byte[count];
            System.Array.Copy(data, p, roomNumbers, 0, count);
            p += count;

            int offsetArrayPosition = p;
            var offsets = new int[count];
            for (int i = 0; i < count; i++)
            {
                offsets[i] = data[p] | (data[p + 1] << 8);
                p += 2;
            }

            return new V3OldResourceDirectory(roomNumbers, offsets, offsetArrayPosition);
        }
    }

    /// <summary>A v3 old-bundle resource directory: parallel room-number and uint16-offset arrays.</summary>
    public class V3OldResourceDirectory
    {
        public V3OldResourceDirectory(byte[] roomNumbers, int[] offsets, int offsetArrayPosition)
        {
            RoomNumbers = roomNumbers;
            Offsets = offsets;
            OffsetArrayPosition = offsetArrayPosition;
        }

        /// <summary>Room number that holds each resource (index = resource id). For ROOM these are filler.</summary>
        public byte[] RoomNumbers { get; private set; }

        /// <summary>File-relative offset of each resource within its room's NN.LFL (0xFFFF = missing).</summary>
        public int[] Offsets { get; private set; }

        /// <summary>Byte position of the offset array within the index RawContent (for in-place rewrite on edit).</summary>
        public int OffsetArrayPosition { get; private set; }

        public int Count
        {
            get { return Offsets == null ? 0 : Offsets.Length; }
        }
    }
}
