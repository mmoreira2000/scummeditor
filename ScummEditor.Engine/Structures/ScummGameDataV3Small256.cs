using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// Game data for the SCUMM v3 GF_OLD256 games (Indiana Jones 3 VGA, Zak FM-Towns): one NN.LFL
    /// room file per room (a bare RO block + sibling SC/CO/SO), a 00.LFL small-header index
    /// (0R/0S/0N/0C/0O - like v4 but with no RN block) and 9x.LFL charsets. Everything inside the
    /// blocks is byte-identical to v4, so the v4 block/index classes are reused. The two differences
    /// handled here: resource-directory offsets are FILE-ABSOLUTE (the room file starts with RO at
    /// offset 0, with no LF/disk wrapper), and a directory entry's room number maps to its NN.LFL.
    /// </summary>
    public class ScummGameDataV3Small256 : ScummGameData
    {
        protected override ScummDataFile CreateDataFile()
        {
            return new ScummV3Small256DataFile(null, LoadedGameInfo);
        }

        protected override ScummIndexFile CreateIndexFile()
        {
            // The v4 index reader types 0S/0N/0C and keeps the rest verbatim; the v3 index is the
            // same flat block sequence minus the RN block, so it parses unchanged.
            return new ScummV4IndexFile(LoadedGameInfo);
        }

        protected override void AfterLoad()
        {
            LoadV3Charsets();
        }

        /// <summary>
        /// Links each script/sound/costume directory entry to the block holding its bytes. The entry
        /// offset is file-absolute within the room's NN.LFL (RO at 0), and the entry's room number
        /// selects which NN.LFL. Recorded once at load, while block offsets are still the on-disk values.
        /// </summary>
        protected override void LinkDataAndIndexFile()
        {
            var index = IndexFile as ScummV4IndexFile;
            if (index == null)
            {
                return;
            }

            Dictionary<int, BlockBase> roomTrees = BuildRoomTreeMap();

            foreach (ScummV4ResourceDirectory directory in index.ResourceDirectories)
            {
                foreach (ScummV4DirectoryEntry entry in directory.Entries)
                {
                    entry.ContainingBlockId = null;

                    BlockBase tree;
                    if (entry.RoomNumber == 0 || !roomTrees.TryGetValue(entry.RoomNumber, out tree))
                    {
                        continue; // empty slot or a room we do not have
                    }

                    BlockBase containing = FindContainingBlock(tree, entry.Offset);
                    if (containing == null)
                    {
                        continue;
                    }

                    entry.ContainingBlockId = containing.UniqueId;
                    entry.OffsetWithinBlock = (uint)(entry.Offset - containing.BlockOffSet);
                }
            }
        }

        /// <summary>Recomputes each linked directory offset (file-absolute) from its block's new position.</summary>
        protected override void FixUpIndexOffsets()
        {
            var index = IndexFile as ScummV4IndexFile;
            if (index == null)
            {
                return;
            }

            var blocksById = new Dictionary<string, BlockBase>();
            foreach (DataDisk disk in DataDisks)
            {
                CollectBlocks(disk.Tree, blocksById);
            }

            foreach (ScummV4ResourceDirectory directory in index.ResourceDirectories)
            {
                foreach (ScummV4DirectoryEntry entry in directory.Entries)
                {
                    BlockBase containing;
                    if (entry.ContainingBlockId == null
                        || !blocksById.TryGetValue(entry.ContainingBlockId, out containing))
                    {
                        continue;
                    }

                    entry.Offset = (uint)(containing.BlockOffSet + entry.OffsetWithinBlock);
                }
            }
        }

        /// <summary>Maps each room number (parsed from the NN.LFL file name) to its loaded room tree.</summary>
        private Dictionary<int, BlockBase> BuildRoomTreeMap()
        {
            var map = new Dictionary<int, BlockBase>();
            foreach (DataDisk disk in DataDisks)
            {
                int room = RoomNumberFromPath(disk.FilePath);
                if (room > 0)
                {
                    map[room] = disk.Tree;
                }
            }
            return map;
        }

        private static int RoomNumberFromPath(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int number;
            return int.TryParse(name, out number) ? number : -1;
        }

        /// <summary>The deepest block whose byte range contains the file-absolute position.</summary>
        private static BlockBase FindContainingBlock(BlockBase tree, long absolutePosition)
        {
            BlockBase best = null;
            FindContainingBlock(tree, absolutePosition, ref best);
            return best;
        }

        private static void FindContainingBlock(BlockBase block, long absolutePosition, ref BlockBase best)
        {
            // NOTE: this runs at load time, when only the leaf blocks have a BlockSize (read from their
            // on-disk header) - the root container's size is still 0 (it is computed at save time). So we
            // must NOT prune the walk when a block fails the containment test: the size-0 root never
            // "contains" anything, yet its children are exactly the resource blocks we need to link. We
            // therefore always recurse and only record a block as a candidate when it genuinely contains
            // the position (size > 0). The deepest/highest-offset match wins, which is the resource block.
            bool contains = block.BlockSize > 0
                            && block.BlockOffSet <= absolutePosition
                            && absolutePosition < block.BlockOffSet + block.BlockSize;
            if (contains && (best == null || block.BlockOffSet > best.BlockOffSet))
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
    }
}
