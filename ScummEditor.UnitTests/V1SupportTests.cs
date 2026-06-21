using System.IO;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v1 "classic" support (Maniac Mansion / Zak McKracken DOS floppy, index magic 0x0A31). M0
    /// foundation: detection (the same XOR-0xFF GF_OLD_BUNDLE container as v2/v3old, but a count-less
    /// index with hardcoded per-game resource counts and a 1-byte global-object table), byte-identical
    /// container + index round-trip, and the hardcoded directory counts the classic parse depends on.
    /// Real-data tests skip when the GameData library is absent.
    /// </summary>
    public class V1SupportTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1, ScummGame.ManiacMansion)]
        [InlineData(GameLibrary.ZakV1, ScummGame.ZakMcKracken)]
        public void DetectsV1Game(string relativePath, ScummGame expectedGame)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.Equal(1, info.ScummVersion);
            Assert.True(info.UsesOldBundle);       // GF_OLD_BUNDLE container, like v2/v3old
            Assert.True(info.UsesClassicIndex);    // count-less 0x0A31 index
            Assert.False(info.UsesSmallHeader);
            Assert.Equal(0xFF, info.XorKey);       // whole file XOR 0xFF
            Assert.Equal(1, info.GlobalObjectEntrySize); // 1 byte/object (v3old = 4)
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1ContainerRoundTripsByteIdentical(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            foreach (DataDisk disk in game.DataDisks)
            {
                byte[] original = ReadDecrypted(disk.FilePath, game.LoadedGameInfo.XorKey);
                byte[] resaved = Save(disk.Tree);
                Assert.True(BytesEqual(original, resaved),
                    Path.GetFileName(disk.FilePath) + " did not round-trip byte-identical");
            }

            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            byte[] idxOriginal = ReadDecrypted(game.LoadedGameInfo.IndexFile, game.LoadedGameInfo.IndexXorKey);
            byte[] idxResaved;
            using (var ms = new MemoryStream()) { index.SaveToBinaryWriter(ms); idxResaved = ms.ToArray(); }
            Assert.True(BytesEqual(idxOriginal, idxResaved), "00.LFL index did not round-trip byte-identical");
        }

        /// <summary>
        /// The v1 index stores NO counts (hardcoded per game). Asserting the exact directory counts proves
        /// the count-less classic parse walks the file correctly. The index size equals
        /// 2 + numObjects + sum(count*3): Maniac = 2+800+165+105+600+300 = 1972; Zak = 2+775+183+111+465+360 = 1896.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1, 55, 35, 200, 100)]
        [InlineData(GameLibrary.ZakV1, 61, 37, 155, 120)]
        public void V1ClassicIndexParsesHardcodedCounts(string relativePath, int rooms, int costumes, int scripts, int sounds)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            Assert.NotNull(index.RoomDirectory);
            Assert.Equal(rooms, index.RoomDirectory.Count);
            Assert.Equal(costumes, index.CostumeDirectory.Count);
            Assert.Equal(scripts, index.ScriptDirectory.Count);
            Assert.Equal(sounds, index.SoundDirectory.Count);
        }

        /// <summary>The new v1 (0x0A31) branch must not change v2/v3 old-bundle detection.</summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2, 2, ScummGame.ManiacMansion)]
        [InlineData(GameLibrary.LoomEga, 3, ScummGame.Loom)]
        [InlineData(GameLibrary.Indy3Ega, 3, ScummGame.IndianaJones3)]
        public void OlderBundleGamesStillDetectCorrectly(string relativePath, int expectedVersion, ScummGame expectedGame)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedVersion, info.ScummVersion);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.False(info.UsesClassicIndex);
        }

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
        }

        private static byte[] Save(BlockBase tree)
        {
            using (var ms = new MemoryStream()) { tree.SaveToBinaryWriter(ms); return ms.ToArray(); }
        }

        private static byte[] ReadDecrypted(string path, int xorKey)
        {
            byte[] data = File.ReadAllBytes(path);
            if (xorKey != 0) for (int i = 0; i < data.Length; i++) data[i] ^= (byte)xorKey;
            return data;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
