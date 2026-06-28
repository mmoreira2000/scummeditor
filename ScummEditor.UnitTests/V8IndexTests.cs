using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) index relocation - the save-path that lets a size-changing
    /// edit produce a loadable game. v8 spans two data files and its directory offsets are LFLF-relative
    /// (the ROOM sits 8 bytes into the LFLF), so the relocation is v8-specific. Two gates:
    ///   (1) a NO-OP PostProcessChanges must leave the index AND both data files byte-identical (proving
    ///       the link/fixup reproduce the exact original offsets - i.e. the offset base is correct);
    ///   (2) after a size-CHANGING text edit, every used directory offset, resolved against the
    ///       re-serialized data, must still land on the correct block (no stale offsets - the bug that
    ///       black-screened edited v3 games).
    /// </summary>
    public class V8IndexTests
    {
        private readonly ITestOutputHelper _output;

        public V8IndexTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public void NoOpPostProcessKeepsIndexAndDataByteIdentical()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(info == null, "COMI (v8) not present");

            ScummGameData game = ScummGameData.LoadFromGameInfo(info);
            game.PostProcessChanges(); // CalculateOffsets + the v8 FixUpIndexOffsets, with no edit

            using (var ms = new MemoryStream())
            {
                game.IndexFile.SaveToBinaryWriter(ms);
                AssertBytesEqual(File.ReadAllBytes(info.IndexFile), ms.ToArray(), Path.GetFileName(info.IndexFile));
            }
            foreach (DataDisk disk in game.DataDisks)
            {
                using (var ms = new MemoryStream())
                {
                    disk.Tree.SaveToBinaryWriter(ms);
                    AssertBytesEqual(File.ReadAllBytes(disk.FilePath), ms.ToArray(), Path.GetFileName(disk.FilePath));
                }
            }
        }

        [SkippableFact]
        public void SizeChangingEditRelocatesEveryDirectoryOffset()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(info == null, "COMI (v8) not present");

            ScummGameData game = ScummGameData.LoadFromGameInfo(info);

            // Apply a real size-changing text edit (lengthen the first plain-text entry).
            string tmp = Path.Combine(Path.GetTempPath(), "comi_v8_idx_edit.txt");
            GameTextManager.ExportToFileV8(game, tmp, GameTextCodec.Default(), "COMI");
            string[] lines = File.ReadAllLines(tmp);
            bool edited = false;
            for (int i = 0; i < lines.Length && !edited; i++)
            {
                if (lines[i].Length == 0 || lines[i][0] == ';') continue;
                int eq = lines[i].IndexOf(" = ");
                if (eq <= 0) continue;
                string text = lines[i].Substring(eq + 3);
                if (text.Length >= 3 && text.Any(char.IsLetter) && !text.Contains("{"))
                {
                    lines[i] = lines[i].Substring(0, eq) + " = " + text + " ZZ";
                    edited = true;
                }
            }
            Skip.If(!edited, "no plain-text v8 entry to edit");
            File.WriteAllLines(tmp, lines);
            GameTextImportReport report = GameTextManager.ImportFromFileV8(game, tmp);
            File.Delete(tmp);
            Assert.Empty(report.Errors);
            Assert.True(report.StringsChanged >= 1);

            // Recompute offsets, then re-serialize the index + both data files and re-parse them, exactly
            // as a save+reload would. The reload's offsets must resolve against the reloaded data.
            game.PostProcessChanges();

            var idx = ReParseIndex(game.IndexFile, info);
            var disks = new List<ScummDataFile>();
            foreach (DataDisk disk in game.DataDisks) disks.Add(ReParseDisk(disk.Tree, info));

            // Map each room number to its owning LFLF (the disk is taken from DROO; LOFF is positional).
            var roomToLflf = new Dictionary<int, DiskBlock>();
            for (int d = 0; d < disks.Count; d++)
            {
                List<DiskBlock> lflfs = disks[d].GetLFLFs();
                RoomOffsetTable loff = disks[d].GetLOFF();
                for (int k = 0; k < lflfs.Count && k < loff.Rooms.Count; k++)
                {
                    int room = loff.Rooms[k].Id;
                    int owner = room < idx.DROO.Rooms.Count ? idx.DROO.Rooms[room].Number : (d + 1);
                    if (owner == d + 1 || !roomToLflf.ContainsKey(room)) roomToLflf[room] = lflfs[k];
                }
            }

            int resolved = 0;
            resolved += AssertDirectoryResolves(idx.DSCR, "SCRP", roomToLflf);
            resolved += AssertDirectoryResolves(idx.DSOU, "SOUN", roomToLflf);
            resolved += AssertDirectoryResolves(idx.DCOS, "AKOS", roomToLflf);
            resolved += AssertDirectoryResolves(idx.DRSC, "RMSC", roomToLflf);
            _output.WriteLine("v8 directory entries resolved after the edit: {0}", resolved);
            Assert.True(resolved > 0, "no directory entries resolved - the index was not relocated");
        }

        /// <summary>Checks every used entry of a directory resolves to a block of the expected tag.</summary>
        private static int AssertDirectoryResolves(DirectoryOfItems dir, string tag, Dictionary<int, DiskBlock> roomToLflf)
        {
            if (dir == null) return 0;
            int count = 0;
            foreach (DirectoryItem entry in dir.Rooms)
            {
                if (entry.Offset == 0) continue; // unused slot (room 0 / no resource)
                DiskBlock lflf;
                if (!roomToLflf.TryGetValue(entry.Number, out lflf)) continue; // room not present on either disk
                long target = lflf.BlockOffSet + entry.Offset;
                bool ok = lflf.Childrens.Any(b => b.BlockOffSet == target && b.BlockType == tag);
                Assert.True(ok, string.Format("{0} room {1} offset 0x{2:X} does not resolve to a {0} block after the edit", tag, entry.Number, entry.Offset));
                count++;
            }
            return count;
        }

        private static ScummV8IndexFile ReParseIndex(ScummIndexFile original, GameInfo info)
        {
            using (var ms = new MemoryStream())
            {
                original.SaveToBinaryWriter(ms);
                ms.Position = 0;
                var fresh = new ScummV8IndexFile(info);
                fresh.LoadFromBinaryReader(ms);
                return fresh;
            }
        }

        private static ScummDataFile ReParseDisk(ScummDataFile tree, GameInfo info)
        {
            using (var ms = new MemoryStream())
            {
                tree.SaveToBinaryWriter(ms);
                ms.Position = 0;
                var fresh = new ScummDataFile(null, info);
                fresh.LoadFromBinaryReader(ms);
                return fresh;
            }
        }

        private static void AssertBytesEqual(byte[] expected, byte[] actual, string label)
        {
            Assert.True(expected.Length == actual.Length,
                string.Format("{0}: length {1} != {2}", label, expected.Length, actual.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail(string.Format("{0}: first byte differs at offset 0x{1:X} (expected 0x{2:X2}, got 0x{3:X2})",
                        label, i, expected[i], actual[i]));
                }
            }
        }
    }
}
