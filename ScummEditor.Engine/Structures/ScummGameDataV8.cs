using System.Collections.Generic;
using System.Linq;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// Game data for the SCUMM v8 engine (The Curse of Monkey Island). v8 extends v7: the same IFF
    /// "big header" LECF/LOFF/LFLF/ROOM container (not XOR-encrypted), the same AKOS costumes, and the
    /// same external .NUT SMUSH fonts and .BUN iMUSE bundles - so the v7 loader is reused wholesale. v8
    /// differs in:
    ///   - the index file (COMI.LA0): a DRSC block, 4-byte directory counts, a 168-byte MAXS and a DOBJ
    ///     with 40-byte object names, handled by <see cref="ScummV8IndexFile"/>;
    ///   - two data containers (COMI.LA1 + COMI.LA2) instead of one, loaded through the shared multi-disk
    ///     <see cref="ScummGameData.DataDisks"/> path (the same one v4 uses);
    ///   - a remapped script opcode language, a separate RMSC room-scripts block, larger room/object
    ///     headers and 4-byte text escapes - all handled in later milestones by the data-file block
    ///     classes and the v8 disassembler, switched on GameInfo.ScummVersion.
    /// For the foundation milestone the room/RMSC content is read with the generic recursive reader, so
    /// both data files round-trip byte-for-byte before any block gets typed v8 support.
    /// </summary>
    public class ScummGameDataV8 : ScummGameDataV7
    {
        protected override ScummIndexFile CreateIndexFile()
        {
            return new ScummV8IndexFile(LoadedGameInfo);
        }

        /// <summary>Loads the v8 external resources: the .NUT fonts (via the inherited v7 loader) and the
        /// LANGUAGE.TAB localized text (where almost all of COMI's on-screen text lives).</summary>
        protected override void AfterLoad()
        {
            base.AfterLoad(); // v7 loader: NUT fonts (+ the BND/.TRS path, which finds nothing for v8)
            LoadLanguageTab();
        }

        private void LoadLanguageTab()
        {
            string path = LoadedGameInfo.LanguageTabPath;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
            try
            {
                var tab = new LanguageTabFile(path);
                tab.Load(System.IO.File.ReadAllBytes(path));
                LocalizedTextFiles.Add(tab);
            }
            catch (System.IO.IOException) { }                 // vanished/locked between detection and load
            catch (System.UnauthorizedAccessException) { }
        }

        /// <summary>
        /// Links each index directory entry to its data block so a size-changing edit can relocate it on
        /// save. v8 differs from v5/v6/v7 in two ways handled here:
        ///   - the game spans TWO data files, so this iterates every <see cref="ScummGameData.DataDisks"/>
        ///     entry and uses each disk's own LOFF;
        ///   - the LOFF entry points at the LFLF (not the ROOM) and every directory offset is relative to
        ///     the LFLF (ScummVM getResourceRoomOffset returns 8 for a v8 room, i.e. the ROOM sits 8 bytes
        ///     into the LFLF), so the base here is the LFLF block, not the ROOM block.
        /// DROO is not linked by offset: a v8 DROO entry's number is the DISK number and its offset is 0
        /// (the engine derives the room position from the LOFF), so it never needs relocation.
        /// LFLFs and LOFF entries are matched positionally (both are in file order).
        /// </summary>
        protected override void LinkDataAndIndexFile()
        {
            var index = (ScummV8IndexFile)IndexFile;
            for (int d = 0; d < DataDisks.Count; d++)
            {
                List<DiskBlock> lflfs = DataDisks[d].Tree.GetLFLFs();
                RoomOffsetTable loff = DataDisks[d].Tree.GetLOFF();
                // ScummVM rejects a v8 file whose LFLF and LOFF counts differ, so surface it at load time
                // (the save path already throws on the same mismatch) instead of silently linking a prefix
                // and leaving the surplus rooms with stale, unrelocated index offsets.
                if (lflfs.Count != loff.Rooms.Count)
                {
                    throw new InvalidFileFormatException("v8: the number of LFLF blocks does not match the LOFF table.");
                }
                for (int k = 0; k < lflfs.Count; k++)
                {
                    int roomNumber = loff.Rooms[k].Id;
                    // A room duplicated byte-identically on both disks (COMI's menu rooms 1/3/6/72/92/93)
                    // has ONE directory entry, resolved by the engine against the disk DROO names as its
                    // owner. Link only the owner disk's copy so the entry tracks the right blocks.
                    if (!IsOwnerDisk(index, roomNumber, d)) continue;

                    DiskBlock lflf = lflfs[k];
                    long lflfOffset = lflf.BlockOffSet;
                    LinkRoomResources(index.DSCR, lflf, "SCRP", roomNumber, lflfOffset);
                    LinkRoomResources(index.DSOU, lflf, "SOUN", roomNumber, lflfOffset);
                    LinkRoomResources(index.DCOS, lflf, CostumeBlockType, roomNumber, lflfOffset);
                    LinkRoomResources(index.DCHR, lflf, "CHAR", roomNumber, lflfOffset);
                    LinkRoomResources(index.DRSC, lflf, "RMSC", roomNumber, lflfOffset);
                }
            }
        }

        /// <summary>
        /// True when data-disk index <paramref name="diskIndex"/> (0-based) is the one DROO names as the
        /// owner of <paramref name="roomNumber"/> (DROO stores a 1-based disk number per room). A room that
        /// exists on only one disk, or whose DROO entry is missing, defaults to its single disk.
        /// </summary>
        private static bool IsOwnerDisk(ScummV8IndexFile index, int roomNumber, int diskIndex)
        {
            if (roomNumber < 0 || roomNumber >= index.DROO.Rooms.Count) return true;
            int owner = index.DROO.Rooms[roomNumber].Number; // 1-based disk number
            if (owner <= 0) return true; // not a disk-numbered DROO entry: keep it
            return owner == diskIndex + 1;
        }

        /// <summary>
        /// Links one LFLF's blocks of a given type to their directory entries, scoped by BOTH the room
        /// number and the LFLF-relative offset (two rooms can hold a block at the same relative offset, so
        /// the offset alone is not unique). Each matched entry stores the block's UniqueId for the save-time
        /// relocation in <see cref="FixUpIndexOffsets"/>.
        /// </summary>
        private static void LinkRoomResources(DirectoryOfItems directory, DiskBlock lflf, string blockType, int roomNumber, long lflfOffset)
        {
            if (directory == null) return;
            foreach (BlockBase block in lflf.Childrens.Where(b => b.BlockType == blockType))
            {
                uint relativeOffset = (uint)(block.BlockOffSet - lflfOffset);
                directory.Rooms.Where(x => x.Number == roomNumber && x.Offset == relativeOffset).ToList()
                    .ForEach(r => r.ItemId = block.UniqueId);
            }
        }

        /// <summary>
        /// Recomputes the index offsets from the (recalculated) block positions on save, across both data
        /// files. Each disk's LOFF entry is set to its LFLF's new offset (positional match), and every
        /// linked DSCR/DSOU/DCOS/DCHR/DRSC entry to its block's new LFLF-relative offset. DROO is left
        /// untouched (disk number + zero offset).
        /// </summary>
        protected override void FixUpIndexOffsets()
        {
            var index = (ScummV8IndexFile)IndexFile;
            for (int d = 0; d < DataDisks.Count; d++)
            {
                List<DiskBlock> lflfs = DataDisks[d].Tree.GetLFLFs();
                RoomOffsetTable loff = DataDisks[d].Tree.GetLOFF();
                if (lflfs.Count != loff.Rooms.Count)
                {
                    throw new InvalidFileFormatException("v8: the number of LFLF blocks does not match the LOFF table.");
                }

                for (int k = 0; k < lflfs.Count; k++)
                {
                    DiskBlock lflf = lflfs[k];
                    long lflfOffset = lflf.BlockOffSet;
                    // Every disk's LOFF entry tracks its own LFLF position (the LOFF is per-disk and the
                    // engine consults it after picking the disk from DROO), so update it regardless of owner.
                    loff.Rooms[k].OffSet = (uint)lflfOffset; // v8 LOFF points at the LFLF

                    // The shared directories are resolved against the owner disk only (duplicated rooms).
                    if (!IsOwnerDisk(index, loff.Rooms[k].Id, d)) continue;

                    FixUpRoomResources(index.DSCR, lflf, "SCRP", lflfOffset);
                    FixUpRoomResources(index.DSOU, lflf, "SOUN", lflfOffset);
                    FixUpRoomResources(index.DCOS, lflf, CostumeBlockType, lflfOffset);
                    FixUpRoomResources(index.DCHR, lflf, "CHAR", lflfOffset);
                    FixUpRoomResources(index.DRSC, lflf, "RMSC", lflfOffset);
                }
            }
        }

        private static void FixUpRoomResources(DirectoryOfItems directory, DiskBlock lflf, string blockType, long lflfOffset)
        {
            if (directory == null) return;
            foreach (BlockBase block in lflf.Childrens.Where(b => b.BlockType == blockType))
            {
                directory.Rooms.Where(x => x.ItemId == block.UniqueId).ToList()
                    .ForEach(r => r.Offset = (uint)(block.BlockOffSet - lflfOffset));
            }
        }
    }
}
