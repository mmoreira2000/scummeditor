using System;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// Game data for the SCUMM v7 engine (The Dig, Full Throttle). The container is the same IFF
    /// "big header" LECF/LOFF/LFLF/ROOM tree as v5/v6, so the v5/v6 data file and save/offset code are
    /// reused; v7 differs only in:
    ///   - the index file (GAME.LA0): an extra ANAM block and a 130-byte MAXS, handled by
    ///     <see cref="ScummV7IndexFile"/>;
    ///   - costumes stored as AKOS blocks instead of COST (so the costume directory DCOS links to the
    ///     AKOS blocks - see <see cref="CostumeBlockType"/>);
    ///   - no whole-file XOR encryption (the GameInfo keys are 0).
    /// The version-specific block parsing (the 10-byte v7 room header, the generic reader for AKOS /
    /// SOUN / object blocks) lives in the data-file block classes, switched on GameInfo.ScummVersion.
    /// </summary>
    public class ScummGameDataV7 : ScummGameDataV5V6
    {
        protected override ScummIndexFile CreateIndexFile()
        {
            return new ScummV7IndexFile(LoadedGameInfo);
        }

        protected override string CostumeBlockType
        {
            get { return "AKOS"; }
        }

        /// <summary>Loads the external .NUT SMUSH fonts that sit next to the .LA0/.LA1 container.</summary>
        protected override void AfterLoad()
        {
            base.AfterLoad();
            LoadNutFonts();
            LoadLocalizedText();
        }

        /// <summary>Loads The Dig's LANGUAGE.BND (localized in-game text) and the .TRS subtitle/UI files.</summary>
        private void LoadLocalizedText()
        {
            LocalizedTextFiles.Clear();

            if (!string.IsNullOrEmpty(LoadedGameInfo.LanguageBundlePath) && File.Exists(LoadedGameInfo.LanguageBundlePath))
            {
                try
                {
                    var bundle = new LanguageBundleFile(LoadedGameInfo.LanguageBundlePath);
                    bundle.Load(File.ReadAllBytes(LoadedGameInfo.LanguageBundlePath));
                    LocalizedTextFiles.Add(bundle);
                }
                catch (IOException)
                {
                    // LANGUAGE.BND vanished/locked between detection and load: skip it, still load the game
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            if (LoadedGameInfo.TrsFiles != null)
            {
                foreach (string path in LoadedGameInfo.TrsFiles)
                {
                    if (!File.Exists(path)) continue;
                    try
                    {
                        var trs = new TrsFile(path);
                        trs.Load(File.ReadAllBytes(path));
                        LocalizedTextFiles.Add(trs);
                    }
                    catch (IOException)
                    {
                        // a .TRS that vanished/locked between detection and load: skip, still load the game
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        private void LoadNutFonts()
        {
            NutFonts.Clear();
            if (LoadedGameInfo.NutFontFiles == null)
            {
                return;
            }

            foreach (string path in LoadedGameInfo.NutFontFiles)
            {
                byte[] bytes;
                try
                {
                    bytes = File.ReadAllBytes(path);
                }
                catch (IOException)
                {
                    continue; // a .NUT enumerated at detection is now missing/locked: skip it, still load the game
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                var font = new NutFont { FilePath = path };
                font.LoadFromFileBytes(bytes); // a malformed NUT parses to IsValid=false, it does not throw
                NutFonts.Add(new NutFontResource { FilePath = path, Font = font });
            }
        }

        /// <summary>
        /// Links the index directories to the data blocks like v5/v6, but matches each resource to its
        /// directory entry by BOTH the room number AND the offset relative to the ROOM block. Two
        /// different rooms can legitimately hold a resource at the same relative offset (verified in real
        /// data: The Dig's DCOS costumes, Full Throttle's DSOU sounds), so the v5/v6 offset-only match
        /// would cross-link them and relocate the wrong entry on a size-changing edit. The room number
        /// comes from the LOFF table (its entries survive edits), matched by the ROOM block's offset.
        /// </summary>
        protected override void LinkDataAndIndexFile()
        {
            RoomOffsetTable loff = DataFile.GetLOFF();
            foreach (DiskBlock diskBlock in DataFile.GetLFLFs())
            {
                long roomOffset = diskBlock.GetROOM().BlockOffSet;

                RoomOffsetTableItem loffEntry = loff.Rooms.FirstOrDefault(r => r.OffSet == roomOffset);
                if (loffEntry == null)
                {
                    continue;
                }
                int roomNumber = loffEntry.Id;

                // The room directory (DROO) stores absolute LFLF offsets, which are unique, so it needs
                // no room scoping.
                IndexFile.DROO.Rooms.Where(x => x.Offset == diskBlock.BlockOffSet).ToList()
                    .ForEach(r => r.ItemId = diskBlock.UniqueId);

                LinkRoomResources(IndexFile.DSCR, diskBlock, "SCRP", roomNumber, roomOffset);
                LinkRoomResources(IndexFile.DSOU, diskBlock, "SOUN", roomNumber, roomOffset);
                LinkRoomResources(IndexFile.DCOS, diskBlock, CostumeBlockType, roomNumber, roomOffset);
                LinkRoomResources(IndexFile.DCHR, diskBlock, "CHAR", roomNumber, roomOffset);
            }
        }

        /// <summary>
        /// Links one room's resources of a given block type to their directory entries, scoped by BOTH
        /// the room number and the offset relative to the ROOM block. Each matched entry gets the block's
        /// UniqueId, which the (shared) FixUpIndexOffsets uses to relocate it on save.
        /// </summary>
        private static void LinkRoomResources(DirectoryOfItems directory, DiskBlock diskBlock, string blockType, int roomNumber, long roomOffset)
        {
            foreach (BlockBase block in diskBlock.Childrens.Where(b => b.BlockType == blockType))
            {
                uint relativeOffset = (uint)(block.BlockOffSet - roomOffset);
                directory.Rooms.Where(x => x.Number == roomNumber && x.Offset == relativeOffset).ToList()
                    .ForEach(r => r.ItemId = block.UniqueId);
            }
        }
    }
}
