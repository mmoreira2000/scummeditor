using System.Collections.Generic;
using System.Linq;
using ScummEditor.Exceptions;
using ScummEditor.Structures.DataFile;
using ScummEditor.Structures.IndexFile;

namespace ScummEditor.Structures
{
    /// <summary>
    /// Game data for the SCUMM v5/v6 engines (IFF "big header" container, a single data file with a
    /// directory-based index). The index entries are linked to their data blocks by offset on load
    /// and the directory/LOFF offsets are recomputed from the block positions on save.
    /// </summary>
    public class ScummGameDataV5V6 : ScummGameData
    {
        protected override ScummV5V6DataFile CreateDataFile()
        {
            return new ScummV5V6DataFile(null, LoadedGameInfo);
        }

        protected override ScummV5V6IndexFile CreateIndexFile()
        {
            return new ScummV5V6IndexFile(LoadedGameInfo);
        }

        protected override void LinkDataAndIndexFile()
        {
            var diskBlocks = DataFile.GetLFLFs();
            foreach (DiskBlock diskBlock in diskBlocks)
            {
                List<DirectoryItem> matchRooms = IndexFile.DROO.Rooms.Where(x => x.Offset == diskBlock.BlockOffSet).ToList();
                matchRooms.ForEach(r => r.ItemId = diskBlock.UniqueId);

                long roomOffset = diskBlock.GetROOM().BlockOffSet;

                List<BlockBase> scripts = diskBlock.Childrens.Where(s => s.BlockType == "SCRP").ToList();
                foreach (var script in scripts)
                {
                    List<DirectoryItem> matchScripts = IndexFile.DSCR.Rooms.Where(x => x.Offset == (script.BlockOffSet - roomOffset)).ToList();
                    matchScripts.ForEach(r => r.ItemId = script.UniqueId);
                }

                List<BlockBase> sounds = diskBlock.Childrens.Where(s => s.BlockType == "SOUN").ToList();
                foreach (var sound in sounds)
                {
                    List<DirectoryItem> matchSounds = IndexFile.DSOU.Rooms.Where(x => x.Offset == (sound.BlockOffSet - roomOffset)).ToList();
                    matchSounds.ForEach(r => r.ItemId = sound.UniqueId);
                }

                List<BlockBase> costumes = diskBlock.Childrens.Where(s => s.BlockType == "COST").ToList();
                foreach (var costume in costumes)
                {
                    List<DirectoryItem> matchCostumes = IndexFile.DCOS.Rooms.Where(x => x.Offset == (costume.BlockOffSet - roomOffset)).ToList();
                    matchCostumes.ForEach(r => r.ItemId = costume.UniqueId);
                }

                List<BlockBase> characters = diskBlock.Childrens.Where(s => s.BlockType == "CHAR").ToList();
                foreach (var character in characters)
                {
                    List<DirectoryItem> matchChars = IndexFile.DCHR.Rooms.Where(x => x.Offset == (character.BlockOffSet - roomOffset)).ToList();
                    matchChars.ForEach(r => r.ItemId = character.UniqueId);
                }
            }
        }

        protected override void FixUpIndexOffsets()
        {
            RoomOffsetTable LOFF = DataFile.GetLOFF();
            List<DiskBlock> diskBlocks = DataFile.GetLFLFs();

            if (diskBlocks.Count != LOFF.Rooms.Count) throw new InvalidFileFormatException("The number of rooms does not match the number of LFLF blocks.");

            //Update ROOM positions offset.
            var orderedDisks = OrderDiskBlocks();
            foreach (RoomOffsetTableItem offsetTableItem in LOFF.Rooms)
            {
                offsetTableItem.OffSet = (uint)orderedDisks[offsetTableItem.Id].Childrens.Single(b => b.GetType() == typeof(RoomBlock)).BlockOffSet;
            }

            foreach (DiskBlock diskBlock in diskBlocks)
            {
                long roomOffset = diskBlock.GetROOM().BlockOffSet;

                List<BlockBase> scripts = diskBlock.Childrens.Where(s => s.BlockType == "SCRP").ToList();
                foreach (var script in scripts)
                {
                    List<DirectoryItem> matchScripts = IndexFile.DSCR.Rooms.Where(x => x.ItemId == script.UniqueId).ToList();
                    matchScripts.ForEach(r => r.Offset = (uint)(script.BlockOffSet - roomOffset));
                }

                List<BlockBase> sounds = diskBlock.Childrens.Where(s => s.BlockType == "SOUN").ToList();
                foreach (var sound in sounds)
                {
                    List<DirectoryItem> matchSounds = IndexFile.DSOU.Rooms.Where(x => x.ItemId == sound.UniqueId).ToList();
                    matchSounds.ForEach(r => r.Offset = (uint)(sound.BlockOffSet - roomOffset));
                }

                List<BlockBase> costumes = diskBlock.Childrens.Where(s => s.BlockType == "COST").ToList();
                foreach (var costume in costumes)
                {
                    List<DirectoryItem> matchCostumes = IndexFile.DCOS.Rooms.Where(x => x.ItemId == costume.UniqueId).ToList();
                    matchCostumes.ForEach(r => r.Offset = (uint)(costume.BlockOffSet - roomOffset));
                }

                List<BlockBase> characters = diskBlock.Childrens.Where(s => s.BlockType == "CHAR").ToList();
                foreach (var character in characters)
                {
                    List<DirectoryItem> matchChars = IndexFile.DCHR.Rooms.Where(x => x.ItemId == character.UniqueId).ToList();
                    matchChars.ForEach(r => r.Offset = (uint)(character.BlockOffSet - roomOffset));
                }
            }
        }

        private DiskBlock[] OrderDiskBlocks()
        {
            var result = new DiskBlock[IndexFile.DROO.Rooms.Count];

            //Disk numbering is annoying and can skip some rooms.
            //This routine walks every room found and places each one in an array with the
            //same size as the room index, leaving null the slots of the rooms that are
            //not in use (number 0 in the index file).
            //This makes updating the index offset tables much easier.
            List<DiskBlock> diskBlocks = DataFile.GetLFLFs();

            int nextRoomIndex = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (IndexFile.DROO.Rooms[i].Number == 1)
                {
                    result[i] = diskBlocks[nextRoomIndex];
                    nextRoomIndex++;
                }
            }
            return result;
        }
    }
}
