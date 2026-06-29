using System.IO;
using ScummEditor.Engine.Exceptions;

namespace ScummEditor.Engine.Structures.IndexFile
{
    /// <summary>
    /// Index reader for the SCUMM v8 game (The Curse of Monkey Island): the COMI.LA0 file. Its block
    /// order is RNAM, MAXS, DROO, DRSC, DSCR, DSOU, DCOS, DCHR, DOBJ, AARY - the v6/v7 set plus the
    /// v8-only DRSC (directory of room scripts) and WITHOUT the v7 ANAM. The file is plaintext (not
    /// XOR-encrypted).
    ///
    /// Deltas vs <see cref="ScummV7IndexFile"/>: every directory's item-count is a 4-byte field instead
    /// of 2 (handled inside <see cref="DirectoryOfItems"/> via the SCUMM version); MAXS is a 168-byte
    /// body of 17 uint32 fields; DOBJ stores a 40-byte name per object. MAXS, DOBJ, AARY and RNAM hold
    /// no data-file offsets, so they are kept verbatim; the six directories are parsed so their offsets
    /// can be recomputed on a size-changing edit. The typed DROO/DSCR/DSOU/DCOS/DCHR properties are
    /// inherited from <see cref="ScummIndexFile"/>; DRSC is added here.
    /// </summary>
    public class ScummV8IndexFile : ScummIndexFile
    {
        public RawIndexBlock RawRNAM { get; private set; }
        public RawIndexBlock RawMAXS { get; private set; }
        public RawIndexBlock RawDOBJ { get; private set; }
        public RawIndexBlock RawAARY { get; private set; }

        /// <summary>v8-only directory of room scripts (points at the RMSC blocks).</summary>
        public DirectoryOfRoomScripts DRSC { get; private set; }

        public ScummV8IndexFile(GameInfo gameInfo) : base(gameInfo) { }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            RawRNAM = ReadRawBlock(binaryReader, "RNAM");
            RawMAXS = ReadRawBlock(binaryReader, "MAXS");

            DROO = new DirectoryOfRooms(null, GameInfo);
            DROO.LoadFromBinaryReader(binaryReader);

            DRSC = new DirectoryOfRoomScripts(null, GameInfo);
            DRSC.LoadFromBinaryReader(binaryReader);

            DSCR = new DirectoryOfScripts(null, GameInfo);
            DSCR.LoadFromBinaryReader(binaryReader);

            DSOU = new DirectoryOfSounds(null, GameInfo);
            DSOU.LoadFromBinaryReader(binaryReader);

            DCOS = new DirectoryOfCostumes(null, GameInfo);
            DCOS.LoadFromBinaryReader(binaryReader);

            DCHR = new DirectoryOfCharsets(null, GameInfo);
            DCHR.LoadFromBinaryReader(binaryReader);

            RawDOBJ = ReadRawBlock(binaryReader, "DOBJ");
            RawAARY = ReadRawBlock(binaryReader, "AARY");

            if (binaryReader.Length != binaryReader.Position)
            {
                throw new InvalidFileFormatException(string.Format(
                    "The v8 index file could not be read completely. There are {0} bytes left.",
                    binaryReader.Length - binaryReader.Position));
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            RawRNAM.SaveToBinaryWriter(binaryWriter);
            RawMAXS.SaveToBinaryWriter(binaryWriter);
            DROO.SaveToBinaryWriter(binaryWriter);
            DRSC.SaveToBinaryWriter(binaryWriter);
            DSCR.SaveToBinaryWriter(binaryWriter);
            DSOU.SaveToBinaryWriter(binaryWriter);
            DCOS.SaveToBinaryWriter(binaryWriter);
            DCHR.SaveToBinaryWriter(binaryWriter);
            RawDOBJ.SaveToBinaryWriter(binaryWriter);
            RawAARY.SaveToBinaryWriter(binaryWriter);

            binaryWriter.Flush();
        }

        private RawIndexBlock ReadRawBlock(Stream binaryReader, string expectedTag)
        {
            var block = new RawIndexBlock(GameInfo, expectedTag);
            block.LoadFromBinaryReader(binaryReader);
            return block;
        }
    }
}
