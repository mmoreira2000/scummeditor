using System.IO;
using ScummEditor.Engine;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v1/v2 support (Maniac Mansion, Zak McKracken classic). M0 foundation: detection (the v2
    /// games share the v3old magic 0x0100 but ship no charsets and use a 1-byte global-object table),
    /// byte-identical container + index round-trip, and the object-table stride that the index parse
    /// depends on. All real-data tests skip when the GameData library is absent.
    /// </summary>
    public class V2SupportTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2, ScummGame.ManiacMansion)]
        [InlineData(GameLibrary.ZakV2, ScummGame.ZakMcKracken)]
        public void DetectsV2Game(string relativePath, ScummGame expectedGame)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.Equal(2, info.ScummVersion);
            Assert.True(info.UsesOldBundle);     // GF_OLD_BUNDLE container, like v3old
            Assert.False(info.UsesSmallHeader);
            Assert.Equal(0xFF, info.XorKey);     // whole file XOR 0xFF
            Assert.Equal(1, info.GlobalObjectEntrySize); // v2 = 1 byte/object (v3old = 4)
        }

        /// <summary>
        /// v2 detection must NOT be confused with the v3old EGA games (same 0x0100 magic) - the charset
        /// count splits them - and v3old/v3small detection must be unchanged.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga, 3, ScummGame.Loom)]
        [InlineData(GameLibrary.Indy3Ega, 3, ScummGame.IndianaJones3)]
        [InlineData(GameLibrary.ZakFmTowns, 3, ScummGame.ZakMcKracken)]
        public void V3GamesStillDetectAsV3(string relativePath, int expectedVersion, ScummGame expectedGame)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedVersion, info.ScummVersion);
            Assert.Equal(expectedGame, info.LoadedGame);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2ContainerRoundTripsByteIdentical(string relativePath)
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
        /// The v2 index uses a 1-byte global-object table; with the v3old 4-byte stride the four resource
        /// directories parse off-position (garbage counts or a null overlay). Asserting the exact counts
        /// proves the stride is handled. (Real counts: Maniac 61/40/179/120, Zak 61/40/155/120.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2, 179)]
        [InlineData(GameLibrary.ZakV2, 155)]
        public void V2IndexParsesDirectoriesWithOneByteObjectTable(string relativePath, int expectedScripts)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            Assert.NotNull(index.RoomDirectory);
            Assert.Equal(61, index.RoomDirectory.Count);
            Assert.Equal(40, index.CostumeDirectory.Count);
            Assert.Equal(expectedScripts, index.ScriptDirectory.Count);
            Assert.Equal(120, index.SoundDirectory.Count);
        }

        // ------------------------------------------------------------------ helpers

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
        }

        private static byte[] Save(BlockBase tree)
        {
            using (var ms = new MemoryStream())
            {
                tree.SaveToBinaryWriter(ms);
                return ms.ToArray();
            }
        }

        private static byte[] ReadDecrypted(string path, int xorKey)
        {
            byte[] data = File.ReadAllBytes(path);
            if (xorKey != 0)
            {
                for (int i = 0; i < data.Length; i++) data[i] ^= (byte)xorKey;
            }
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
