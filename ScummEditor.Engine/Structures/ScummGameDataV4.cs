using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// Game data for the SCUMM v4 engine (small-header container spread over several DISKnn.LEC
    /// disks, a 000.LFL index, and standalone 90x.LFL fonts). The index entries (0S/0N/0C) are
    /// linked to the data block that holds their bytes on load, and on save the FO room-offset
    /// tables and those directory offsets are recomputed from the (recalculated) block positions.
    /// </summary>
    public class ScummGameDataV4 : ScummGameData
    {
        protected override ScummDataFile CreateDataFile()
        {
            return new ScummV4DataFile(null, LoadedGameInfo);
        }

        protected override ScummIndexFile CreateIndexFile()
        {
            return new ScummV4IndexFile(LoadedGameInfo);
        }

        protected override void AfterLoad()
        {
            LoadAllFonts();
            DetectV4Edition();
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
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (IOException)
                {
                    continue; // a 90x.LFL font enumerated at detection is now missing/locked: skip it, still load the game
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                var charset = new Charset(null, LoadedGameInfo);
                charset.LoadFromFileBytes(bytes);
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

        /// <summary>
        /// Links each v4 resource-directory entry (0S/0N/0C) to the data block that holds its bytes,
        /// recording the offset within that block. Done once at load, while block offsets are still
        /// the original on-disk values, so the offset can be recomputed after edits move blocks.
        /// FO entries (rooms) need no link - they are matched to LF blocks by room number at save.
        /// </summary>
        protected override void LinkDataAndIndexFile()
        {
            var index = IndexFile as ScummV4IndexFile;
            if (index == null)
            {
                return;
            }

            Dictionary<int, V4RoomLocation> rooms = BuildV4RoomMap();

            foreach (ScummV4ResourceDirectory directory in index.ResourceDirectories)
            {
                foreach (ScummV4DirectoryEntry entry in directory.Entries)
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
        protected override void FixUpIndexOffsets()
        {
            var index = IndexFile as ScummV4IndexFile;
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

                var lfByRoom = new Dictionary<int, ScummV4DiskBlock>();
                foreach (ScummV4DiskBlock lf in disk.Tree.Childrens.OfType<ScummV4DiskBlock>())
                {
                    lfByRoom[lf.RoomNumber] = lf;
                }

                foreach (RoomOffsetTableItem item in fo.Rooms)
                {
                    ScummV4DiskBlock lf;
                    if (lfByRoom.TryGetValue(item.Id, out lf))
                    {
                        item.OffSet = (uint)lf.BlockOffSet;
                    }
                }
            }

            // 0S/0N/0C: recompute each linked entry from its containing block's new position.
            foreach (ScummV4ResourceDirectory directory in index.ResourceDirectories)
            {
                foreach (ScummV4DirectoryEntry entry in directory.Entries)
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
                foreach (ScummV4DiskBlock lf in disk.Tree.Childrens.OfType<ScummV4DiskBlock>())
                {
                    ScummV4RoomBlock ro = lf.Childrens.OfType<ScummV4RoomBlock>().FirstOrDefault();
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

        /// <summary>Where a v4 room lives: its disk container plus the LF/RO blocks.</summary>
        private class V4RoomLocation
        {
            public ScummDataFile Disk;
            public ScummV4DiskBlock Lf;
            public ScummV4RoomBlock Ro;
        }
    }
}
