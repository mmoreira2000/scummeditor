using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Exceptions;
using ScummEditor.Structures.DataFile;
using ScummEditor.Structures.IndexFile;
using System.Diagnostics;

namespace ScummEditor.Structures
{
    public enum ScummGame
    {
        None = 0,
        SamAndMax = 1,
        DayOfTheTentacle = 2,
        FateOfAtlantis = 3,
        MonkeyIsland1VGA= 4,
        MonkeyIsland2 = 5,
        MonkeyIsland1VGASpeech = 6,
        MonkeyIsland1Floppy = 7,   // SCUMM v4 (000.LFL + DISKnn.LEC)
        Loom = 8                   // SCUMM v4 (Loom CD)
    }

    /// <summary>The graphics edition of a v4 floppy game (its rooms are 16-color EGA or 256-color VGA).</summary>
    public enum GameEdition
    {
        Unknown = 0,
        FloppyEga,
        FloppyVga,
        Cd
    }

    public class GameInfo
    {
        public int ScummVersion { get; set; }
        public ScummGame LoadedGame { get; set; }
        public bool Xored { get; set; }
        public int XorKey { get; set; }

        /// <summary>
        /// XOR key for the index file. Same as <see cref="XorKey"/> on v5/v6, but 0 (plaintext)
        /// on v4, whose 000.LFL index is not whole-file encrypted.
        /// </summary>
        public int IndexXorKey { get; set; }

        /// <summary>True when the release ships recorded speech (the CD / talkie edition).</summary>
        public bool IsTalkie { get; set; }

        /// <summary>
        /// True when the release ships ripped CD audio (CDDA.SOU) instead of a speech file -
        /// e.g. the Monkey Island 1 CD edition, whose music lives on CD audio tracks.
        /// </summary>
        public bool HasCdAudio { get; set; }

        /// <summary>
        /// Path of the speech/effects container (MONSTER.SOU or the FM Towns-style
        /// "game".SOU) when the release ships one, regardless of its size; null otherwise.
        /// </summary>
        public string SpeechFilePath { get; set; }

        /// <summary>Path of the ripped CD audio container (CDDA.SOU) when present; null otherwise.</summary>
        public string CdAudioFilePath { get; set; }

        /// <summary>
        /// The graphics edition (EGA/VGA/CD), determined after loading. Used for the status bar;
        /// only meaningful for v4 floppy games (v5/v6 use IsTalkie/HasCdAudio instead).
        /// </summary>
        public GameEdition Edition { get; set; }

        public string IndexFile { get; set; }
        public string DataFile { get; set; }

        /// <summary>
        /// All data containers that make up the game. v5/v6 keep everything in a single file
        /// (so this has one entry); v4 spreads the rooms over several DISKnn.LEC disks.
        /// </summary>
        public List<string> DataFiles { get; set; }

        /// <summary>
        /// Standalone font files (v4 keeps its charsets as separate plaintext 90x.LFL files,
        /// unlike v5/v6 which embed CHAR blocks in the data file). Null/empty for v5/v6.
        /// </summary>
        public List<string> FontFiles { get; set; }
    }

    /// <summary>One loaded data container paired with the file it came from.</summary>
    public class DataDisk
    {
        public string FilePath { get; set; }
        public ScummV6DataFile Tree { get; set; }
    }

    /// <summary>One loaded standalone font (v4 90x.LFL) paired with the file it came from.</summary>
    public class FontResource
    {
        public string FilePath { get; set; }
        public Charset Charset { get; set; }
    }

    public class ScummV6GameData
    {
        public GameInfo LoadedGameInfo { get; set; }

        public ScummV6IndexFile IndexFile { get; set; }

        /// <summary>The first (or only) data container. For multi-disk v4 games see <see cref="DataDisks"/>.</summary>
        public ScummV6DataFile DataFile { get; set; }

        /// <summary>Every loaded data container (one per file). v5/v6 has a single entry.</summary>
        public List<DataDisk> DataDisks { get; private set; } = new List<DataDisk>();

        /// <summary>Standalone font files loaded for the game (v4 90x.LFL); empty for v5/v6.</summary>
        public List<FontResource> Fonts { get; private set; } = new List<FontResource>();

        public void LoadDataFromDisc(string filePath)
        {
            LoadedGameInfo = Functions.FindScummGame(filePath);
            LoadDetectedGame();
        }

        /// <summary>Loads a game already detected by Functions.FindScummGamesInFolder.</summary>
        public void LoadFromGameInfo(GameInfo gameInfo)
        {
            LoadedGameInfo = gameInfo;
            LoadDetectedGame();
        }

        private void LoadDetectedGame()
        {
            if (LoadedGameInfo == null || LoadedGameInfo.LoadedGame == ScummGame.None)
            {
                return;
            }

            var fileStream = new XoredFileStream(LoadedGameInfo.IndexXorKey, LoadedGameInfo.IndexFile, FileMode.Open, FileAccess.Read);

            LoadIndexFromBinaryReader(fileStream);
            fileStream.Close();

            LoadAllDataFiles();

            // The index<->data linking below is the v5/v6 directory model; the v4 index uses a
            // different layout and is linked separately once its directories are parsed.
            if (LoadedGameInfo.ScummVersion == 4)
            {
                LoadAllFonts();
                DetectV4Edition();
                LinkDataAndIndexFileV4();
            }
            else
            {
                LinkDataAndIndexFile();
            }
        }

        /// <summary>Loads the standalone font files (v4 90x.LFL, plaintext) into Charset objects.</summary>
        private void LoadAllFonts()
        {
            Fonts.Clear();
            if (LoadedGameInfo.FontFiles == null)
            {
                return;
            }

            foreach (string path in LoadedGameInfo.FontFiles)
            {
                var charset = new Charset(null, LoadedGameInfo);
                charset.LoadFromFileBytes(File.ReadAllBytes(path));
                Fonts.Add(new FontResource { FilePath = path, Charset = charset });
            }
        }

        /// <summary>
        /// Tells apart the v4 graphics editions after loading: a ripped-CD-audio game is the CD
        /// edition; otherwise the presence of a palette (PA) block means VGA (256 colors) and its
        /// absence means EGA (16 colors), since EGA rooms store no palette.
        /// </summary>
        private void DetectV4Edition()
        {
            if (LoadedGameInfo.HasCdAudio)
            {
                LoadedGameInfo.Edition = GameEdition.Cd;
                return;
            }

            bool hasPalette = false;
            foreach (DataDisk disk in DataDisks)
            {
                if (ContainsBlock(disk.Tree, "PA"))
                {
                    hasPalette = true;
                    break;
                }
            }

            LoadedGameInfo.Edition = hasPalette ? GameEdition.FloppyVga : GameEdition.FloppyEga;
        }

        private static bool ContainsBlock(BlockBase block, string tag)
        {
            if (block.BlockType == tag)
            {
                return true;
            }
            foreach (BlockBase child in block.Childrens)
            {
                if (ContainsBlock(child, tag))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Loads every data container listed in the game info (one for v5/v6, several for v4).</summary>
        private void LoadAllDataFiles()
        {
            DataDisks.Clear();

            List<string> paths = LoadedGameInfo.DataFiles;
            if (paths == null || paths.Count == 0)
            {
                paths = new List<string> { LoadedGameInfo.DataFile };
            }

            foreach (string path in paths)
            {
                var stream = new XoredFileStream(LoadedGameInfo.XorKey, path, FileMode.Open, FileAccess.Read);
                ScummV6DataFile tree;
                if (LoadedGameInfo.ScummVersion == 4)
                {
                    tree = new Scumm4DataFile(null, LoadedGameInfo);
                }
                else
                {
                    tree = new ScummV6DataFile(null, LoadedGameInfo);
                }
                tree.LoadFromBinaryReader(stream);
                stream.Close();

                DataDisks.Add(new DataDisk { FilePath = path, Tree = tree });
            }

            DataFile = DataDisks[0].Tree;
        }

        private void LinkDataAndIndexFile()
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

        /// <summary>Where a v4 room lives: its disk container plus the LF/RO blocks.</summary>
        private class V4RoomLocation
        {
            public ScummV6DataFile Disk;
            public Scumm4DiskBlock Lf;
            public Scumm4RoomBlock Ro;
        }

        /// <summary>
        /// Links each v4 resource-directory entry (0S/0N/0C) to the data block that holds its bytes,
        /// recording the offset within that block. Done once at load, while block offsets are still
        /// the original on-disk values, so the offset can be recomputed after edits move blocks.
        /// FO entries (rooms) need no link - they are matched to LF blocks by room number at save.
        /// </summary>
        private void LinkDataAndIndexFileV4()
        {
            var index = IndexFile as Scumm4IndexFile;
            if (index == null)
            {
                return;
            }

            Dictionary<int, V4RoomLocation> rooms = BuildV4RoomMap();

            foreach (Scumm4ResourceDirectory directory in index.ResourceDirectories)
            {
                foreach (Scumm4DirectoryEntry entry in directory.Entries)
                {
                    entry.ContainingBlockId = null;

                    V4RoomLocation room;
                    if (entry.RoomNumber == 0 || !rooms.TryGetValue(entry.RoomNumber, out room))
                    {
                        continue; // empty slot or a room we do not have; leave it untouched on save
                    }

                    // 0S/0N/0C offsets are measured from the room's RO block (RO = LF + 8).
                    long absolutePosition = room.Ro.BlockOffSet + entry.Offset;
                    BlockBase containing = FindContainingBlock(room.Disk, absolutePosition);
                    if (containing == null)
                    {
                        continue;
                    }

                    entry.ContainingBlockId = containing.UniqueId;
                    entry.OffsetWithinBlock = (uint)(absolutePosition - containing.BlockOffSet);
                }
            }
        }

        /// <summary>
        /// Recomputes the v4 index offsets after edits: each disk's FO room-offset table (disk-absolute
        /// LF positions) and the 0S/0N/0C directories (room-relative resource positions). Runs after
        /// every disk's block sizes/offsets have been recalculated, so block positions are current.
        /// </summary>
        private void PostProcessChangesV4()
        {
            var index = IndexFile as Scumm4IndexFile;
            if (index == null)
            {
                return;
            }

            Dictionary<int, V4RoomLocation> rooms = BuildV4RoomMap();

            var blocksById = new Dictionary<string, BlockBase>();
            foreach (DataDisk disk in DataDisks)
            {
                CollectBlocks(disk.Tree, blocksById);
            }

            // FO (one per disk): each room entry points at that room's LF block (disk-absolute).
            foreach (DataDisk disk in DataDisks)
            {
                RoomOffsetTable fo = disk.Tree.Childrens.OfType<RoomOffsetTable>().FirstOrDefault();
                if (fo == null)
                {
                    continue;
                }

                var lfByRoom = new Dictionary<int, Scumm4DiskBlock>();
                foreach (Scumm4DiskBlock lf in disk.Tree.Childrens.OfType<Scumm4DiskBlock>())
                {
                    lfByRoom[lf.RoomNumber] = lf;
                }

                foreach (RoomOffsetTableItem item in fo.Rooms)
                {
                    Scumm4DiskBlock lf;
                    if (lfByRoom.TryGetValue(item.Id, out lf))
                    {
                        item.OffSet = (uint)lf.BlockOffSet;
                    }
                }
            }

            // 0S/0N/0C: recompute each linked entry from its containing block's new position.
            foreach (Scumm4ResourceDirectory directory in index.ResourceDirectories)
            {
                foreach (Scumm4DirectoryEntry entry in directory.Entries)
                {
                    BlockBase containing;
                    V4RoomLocation room;
                    if (entry.ContainingBlockId == null
                        || !blocksById.TryGetValue(entry.ContainingBlockId, out containing)
                        || !rooms.TryGetValue(entry.RoomNumber, out room))
                    {
                        continue;
                    }

                    long newAbsolutePosition = containing.BlockOffSet + entry.OffsetWithinBlock;
                    entry.Offset = (uint)(newAbsolutePosition - room.Ro.BlockOffSet);
                }
            }
        }

        private Dictionary<int, V4RoomLocation> BuildV4RoomMap()
        {
            var rooms = new Dictionary<int, V4RoomLocation>();
            foreach (DataDisk disk in DataDisks)
            {
                foreach (Scumm4DiskBlock lf in disk.Tree.Childrens.OfType<Scumm4DiskBlock>())
                {
                    Scumm4RoomBlock ro = lf.Childrens.OfType<Scumm4RoomBlock>().FirstOrDefault();
                    if (ro == null)
                    {
                        continue;
                    }
                    rooms[lf.RoomNumber] = new V4RoomLocation { Disk = disk.Tree, Lf = lf, Ro = ro };
                }
            }
            return rooms;
        }

        /// <summary>The deepest block in the disk tree whose byte range contains the given position.</summary>
        private static BlockBase FindContainingBlock(BlockBase diskTree, long absolutePosition)
        {
            BlockBase best = null;
            FindContainingBlock(diskTree, absolutePosition, ref best);
            return best;
        }

        private static void FindContainingBlock(BlockBase block, long absolutePosition, ref BlockBase best)
        {
            bool contains = block.BlockOffSet <= absolutePosition
                            && absolutePosition < block.BlockOffSet + block.BlockSize;
            if (!contains)
            {
                return;
            }

            if (best == null || block.BlockOffSet > best.BlockOffSet)
            {
                best = block;
            }
            foreach (BlockBase child in block.Childrens)
            {
                FindContainingBlock(child, absolutePosition, ref best);
            }
        }

        private static void CollectBlocks(BlockBase block, Dictionary<string, BlockBase> map)
        {
            map[block.UniqueId] = block;
            foreach (BlockBase child in block.Childrens)
            {
                CollectBlocks(child, map);
            }
        }

        public void SaveDataToDisk()
        {
            PostProcessChanges();

            var fileIndex = Path.Combine(LoadedGameInfo.IndexFile);

            var x2 = new XoredFileStream(LoadedGameInfo.IndexXorKey, fileIndex, FileMode.Create, FileAccess.Write);
            SaveIndexToBinaryWriter(x2);
            x2.Flush();
            x2.Close();

            // Write back every data container to its own file (v4 has several disks).
            foreach (DataDisk disk in DataDisks)
            {
                var dataStream = new XoredFileStream(LoadedGameInfo.XorKey, disk.FilePath, FileMode.Create, FileAccess.Write);
                disk.Tree.SaveToBinaryWriter(dataStream);
                dataStream.Flush();
                dataStream.Close();
            }

            // Write back the standalone font files (v4 90x.LFL, plaintext = the charset bytes).
            foreach (FontResource font in Fonts)
            {
                File.WriteAllBytes(font.FilePath, font.Charset.RawContent);
            }
        }

        public void LoadDataFromBinaryReader(Stream binaryReader)
        {
            if (LoadedGameInfo.ScummVersion == 4)
            {
                DataFile = new Scumm4DataFile(null, LoadedGameInfo);
            }
            else
            {
                DataFile = new ScummV6DataFile(null, LoadedGameInfo);
            }
            DataFile.LoadFromBinaryReader(binaryReader);
        }

        public void SaveDataToBinaryWriter(Stream binaryWriter)
        {
            DataFile.SaveToBinaryWriter(binaryWriter);
        }

        public void LoadIndexFromBinaryReader(Stream binaryReader)
        {
            if (LoadedGameInfo.ScummVersion == 4)
            {
                IndexFile = new Scumm4IndexFile(LoadedGameInfo);
            }
            else
            {
                IndexFile = new ScummV6IndexFile(LoadedGameInfo);
            }
            IndexFile.LoadFromBinaryReader(binaryReader);
        }

        public void SaveIndexToBinaryWriter(Stream binaryWriter)
        {
            IndexFile.SaveToBinaryWriter(binaryWriter);
        }

        public void PostProcessChanges()
        {
            foreach (DataDisk disk in DataDisks)
            {
                disk.Tree.CalculateBlockSize();
                disk.Tree.CalculateOffsets();
            }

            // The directory/offset fix-up below is the v5/v6 model; v4 uses its own.
            if (LoadedGameInfo.ScummVersion == 4)
            {
                PostProcessChangesV4();
                return;
            }

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