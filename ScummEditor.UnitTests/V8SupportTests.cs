using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) foundation: content-only detection, the COMI.LA0 index with
    /// its v8 layout (DRSC block, 4-byte directory counts, 168-byte MAXS, named DOBJ), and a byte-identical
    /// round-trip of the index plus BOTH data files (COMI.LA1 + COMI.LA2). The data is not XOR-encrypted,
    /// so the re-serialized bytes must equal the raw file bytes exactly. The sweep covers every v8 edition
    /// present in the library (the CD release plus the localized ones).
    /// </summary>
    public class V8SupportTests
    {
        [SkippableFact]
        public void DetectsCurseOfMonkeyIslandFromContentAlone()
        {
            string folder = GameLibrary.Folder(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(folder == null, "COMI (v8) not present");

            GameInfo info = Functions.FindScummGameInFolder(folder);

            Assert.Equal(ScummGame.CurseOfMonkeyIsland, info.LoadedGame);
            Assert.Equal(8, info.ScummVersion);
            Assert.False(info.Xored);                 // v8 data is plaintext
            Assert.Equal(2, info.DataFiles.Count);    // COMI.LA1 + COMI.LA2
            Assert.EndsWith(".LA1", info.DataFiles[0]);
            Assert.EndsWith(".LA2", info.DataFiles[1]);
        }

        [SkippableFact]
        public void IndexDirectoriesParse()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var index = (ScummV8IndexFile)game.IndexFile;

            // The six typed directories must parse with sane (4-byte) counts; DRSC is the v8-only one.
            Assert.True(index.DROO.NumOfItems > 0, "DROO empty");
            Assert.True(index.DRSC.NumOfItems > 0, "DRSC (room scripts) empty");
            Assert.True(index.DSCR.NumOfItems > 0, "DSCR empty");
            Assert.True(index.DSOU.NumOfItems > 0, "DSOU empty");
            Assert.True(index.DCOS.NumOfItems > 0, "DCOS empty");
            Assert.NotNull(index.RawMAXS);
            Assert.NotNull(index.RawDOBJ);
        }

        [SkippableFact]
        public void LoadsBothDataDisks()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            Assert.Equal(2, game.DataDisks.Count);
            // Every disk's LECF tree holds at least one LFLF room.
            foreach (DataDisk disk in game.DataDisks)
            {
                Assert.True(disk.Tree.Childrens.Count > 0, "empty data tree: " + disk.FilePath);
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.CurseOfMonkeyIsland)]
        [InlineData(GameLibrary.CurseOfMonkeyIslandPortuguese)]
        public void ContainerRoundTripsByteIdentical(string relativePath)
        {
            string folder = GameLibrary.Folder(relativePath);
            Skip.If(folder == null, "not present: " + relativePath);

            AssertRoundTrip(GameLibrary.Detect(relativePath));
        }

        [SkippableFact]
        public void EveryV8EditionRoundTripsByteIdentical()
        {
            string root = GameLibrary.Folder("ScummV8");
            Skip.If(root == null, "ScummV8 library not present");

            int editions = 0;
            foreach (string indexPath in Directory.GetFiles(root, "COMI.LA0", SearchOption.AllDirectories))
            {
                string folder = Path.GetDirectoryName(indexPath);
                GameInfo info = Functions.FindScummGameInFolder(folder);
                if (info == null || info.LoadedGame != ScummGame.CurseOfMonkeyIsland)
                {
                    continue;
                }
                AssertRoundTrip(info);
                editions++;
            }

            Assert.True(editions > 0, "no v8 editions detected under ScummV8");
        }

        /// <summary>
        /// Loads the game, re-serializes the index and EVERY data disk, and asserts each output is
        /// byte-for-byte identical to the original file on disk (v8 is not XOR-encrypted, so the raw file
        /// bytes are the comparison baseline).
        /// </summary>
        private static void AssertRoundTrip(GameInfo info)
        {
            ScummGameData game = ScummGameData.LoadFromGameInfo(info);

            using (var indexStream = new MemoryStream())
            {
                game.IndexFile.SaveToBinaryWriter(indexStream);
                AssertBytesEqual(File.ReadAllBytes(info.IndexFile), indexStream.ToArray(),
                    Path.GetFileName(info.IndexFile));
            }

            foreach (DataDisk disk in game.DataDisks)
            {
                using (var dataStream = new MemoryStream())
                {
                    disk.Tree.SaveToBinaryWriter(dataStream);
                    AssertBytesEqual(File.ReadAllBytes(disk.FilePath), dataStream.ToArray(),
                        Path.GetFileName(disk.FilePath));
                }
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
