using System;
using System.IO;
using ScummEditor.Exceptions;

namespace ScummEditor.Structures.IndexFile
{
    public class ScummV6IndexFile
    {
        private readonly GameInfo _gameInfo;
        public RoomNamesV6 RNAM { get; set; }
        public MaximumValues MAXS { get; set; }
        public DirectoryOfRooms DROO { get; set; }
        public DirectoryOfScripts DSCR { get; set; }
        public DirectoryOfSounds DSOU { get; set; }
        public DirectoryOfCostumes DCOS { get; set; }
        public DirectoryOfCharsets DCHR { get; set; }
        public DirectoryOfObjects DOBJ { get; set; }
        public DirectoryOfArrays AARY { get; set; }

        public ScummV6IndexFile(GameInfo gameInfo)
        {
            _gameInfo = gameInfo;
        }

        public void LoadFromBinaryReader(Stream binaryReader)
        {
            RNAM = new RoomNamesV6(null, _gameInfo);
            RNAM.LoadFromBinaryReader(binaryReader);

            MAXS = new MaximumValues(null, _gameInfo);
            MAXS.LoadFromBinaryReader(binaryReader);

            DROO = new DirectoryOfRooms(null, _gameInfo);
            DROO.LoadFromBinaryReader(binaryReader);

            DSCR = new DirectoryOfScripts(null, _gameInfo);
            DSCR.LoadFromBinaryReader(binaryReader);

            DSOU = new DirectoryOfSounds(null, _gameInfo);
            DSOU.LoadFromBinaryReader(binaryReader);

            DCOS = new DirectoryOfCostumes(null, _gameInfo);
            DCOS.LoadFromBinaryReader(binaryReader);

            DCHR = new DirectoryOfCharsets(null, _gameInfo);
            DCHR.LoadFromBinaryReader(binaryReader);

            DOBJ = new DirectoryOfObjects(null, _gameInfo);
            DOBJ.LoadFromBinaryReader(binaryReader);

            if (_gameInfo.ScummVersion == 6)
            {
                AARY = new DirectoryOfArrays(null, _gameInfo);
                AARY.LoadFromBinaryReader(binaryReader);
            }

            if (binaryReader.Length != binaryReader.Position)
            {
                throw new InvalidFileFormatException(string.Format("The file could not be read completly. There are {0} left in the file", binaryReader.Length - binaryReader.Position));
            }
        }


        public void SaveToBinaryWriter(Stream binaryWriter)
        {
            RNAM.SaveToBinaryWriter(binaryWriter);
            MAXS.SaveToBinaryWriter(binaryWriter);
            DROO.SaveToBinaryWriter(binaryWriter);
            DSCR.SaveToBinaryWriter(binaryWriter);
            DSOU.SaveToBinaryWriter(binaryWriter);
            DCOS.SaveToBinaryWriter(binaryWriter);
            DCHR.SaveToBinaryWriter(binaryWriter);
            DOBJ.SaveToBinaryWriter(binaryWriter);

            if (_gameInfo.ScummVersion == 6)
            {
                AARY.SaveToBinaryWriter(binaryWriter);
            }

            binaryWriter.Flush();
        }
    }
}