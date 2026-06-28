using System.IO;
using ScummEditor.Engine.Exceptions;

namespace ScummEditor.Engine.Structures.IndexFile
{
    /// <summary>
    /// Index reader for the SCUMM v7 games (The Dig, Full Throttle): the GAME.LA0 file. Its block
    /// order is RNAM, MAXS, DROO, DSCR, DSOU, DCOS, DCHR, DOBJ, AARY, ANAM - the same set as v6 plus
    /// the v7-only ANAM (audio resource names). The file is plaintext (not XOR-encrypted).
    ///
    /// Only the five resource directories (DROO/DSCR/DSOU/DCOS/DCHR) are parsed into typed objects,
    /// because their offsets must be recomputed when a block is edited; their on-disk layout is the
    /// same [count][room numbers][offsets] as v5/v6, so the existing DirectoryOf* readers serve them.
    /// The remaining blocks hold no data-file offsets and are kept verbatim. The typed DROO/DSCR/DSOU/
    /// DCOS/DCHR properties are inherited from <see cref="ScummIndexFile"/> so the shared v5/v6
    /// index-linking code (in ScummGameDataV5V6) works unchanged.
    /// </summary>
    public class ScummV7IndexFile : ScummIndexFile
    {
        public RawIndexBlock RawRNAM { get; private set; }
        public RawIndexBlock RawMAXS { get; private set; }
        public RawIndexBlock RawDOBJ { get; private set; }
        public RawIndexBlock RawAARY { get; private set; }

        /// <summary>v7-only directory of audio resource names (for the .BUN bundles).</summary>
        public RawIndexBlock RawANAM { get; private set; }

        public ScummV7IndexFile(GameInfo gameInfo) : base(gameInfo) { }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            RawRNAM = ReadRawBlock(binaryReader, "RNAM");
            RawMAXS = ReadRawBlock(binaryReader, "MAXS");

            DROO = new DirectoryOfRooms(null, GameInfo);
            DROO.LoadFromBinaryReader(binaryReader);

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
            RawANAM = ReadRawBlock(binaryReader, "ANAM");

            if (binaryReader.Length != binaryReader.Position)
            {
                throw new InvalidFileFormatException(string.Format(
                    "The v7 index file could not be read completely. There are {0} bytes left.",
                    binaryReader.Length - binaryReader.Position));
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            RawRNAM.SaveToBinaryWriter(binaryWriter);
            RawMAXS.SaveToBinaryWriter(binaryWriter);
            DROO.SaveToBinaryWriter(binaryWriter);
            DSCR.SaveToBinaryWriter(binaryWriter);
            DSOU.SaveToBinaryWriter(binaryWriter);
            DCOS.SaveToBinaryWriter(binaryWriter);
            DCHR.SaveToBinaryWriter(binaryWriter);
            RawDOBJ.SaveToBinaryWriter(binaryWriter);
            RawAARY.SaveToBinaryWriter(binaryWriter);
            RawANAM.SaveToBinaryWriter(binaryWriter);

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
