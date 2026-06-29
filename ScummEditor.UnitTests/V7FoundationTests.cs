using System.IO;
using System.Linq;
using ScummEditor.Engine;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 (The Dig, Full Throttle) foundation: detection, container read, and a byte-identical
    /// load -> recompute -> save round-trip of both the GAME.LA0 index and the GAME.LA1 data file. The
    /// round-trip exercises the whole offset machinery (CalculateBlockSize / CalculateOffsets /
    /// FixUpIndexOffsets) and proves the v7 reader/writer is faithful before any block gets typed
    /// support. A separate check confirms the index directories link to their data blocks (so edits
    /// that move blocks will relocate the right entries), including the AKOS-as-costume mapping.
    /// </summary>
    public class V7FoundationTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig, 7, ScummGame.TheDig, "The Dig")]
        [InlineData(GameLibrary.TheDigPortuguese, 7, ScummGame.TheDig, "The Dig")]
        [InlineData(GameLibrary.FullThrottle, 7, ScummGame.FullThrottle, "Full Throttle")]
        [InlineData(GameLibrary.FullThrottlePortuguese, 7, ScummGame.FullThrottle, "Full Throttle")]
        public void DetectsV7Games(string relativePath, int version, ScummGame expectedGame, string expectedName)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);
            Assert.NotNull(info);
            Assert.Equal(version, info.ScummVersion);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.Equal(expectedName, ScummGameNames.DisplayName(info.LoadedGame));
            Assert.False(info.Xored);
            Assert.Equal(0, info.XorKey);
            Assert.Equal(0, info.IndexXorKey);
            Assert.False(info.UsesSmallHeader);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void LoadsContainerTree(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.NotNull(game);
            Assert.Equal("LECF", game.DataFile.BlockType);

            // Every room is an LFLF disk block; there must be a LOFF table and at least one room.
            Assert.NotNull(game.DataFile.GetLOFF());
            Assert.True(game.DataFile.GetLFLFs().Count > 0, "no LFLF room blocks parsed");

            // The index is the v7 layout (raw RNAM/MAXS/DOBJ/AARY/ANAM + typed directories).
            var index = game.IndexFile as ScummV7IndexFile;
            Assert.NotNull(index);
            Assert.NotNull(index.RawANAM); // v7-only audio-names block
            Assert.NotNull(index.DROO);
            Assert.True(index.DROO.Rooms.Count > 0, "DROO is empty");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void RoundTripIsByteIdentical(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);
            Assert.NotNull(info);
            ScummGameData game = ScummGameData.LoadFromGameInfo(info);

            byte[] originalIndex = File.ReadAllBytes(info.IndexFile);
            byte[] originalData = File.ReadAllBytes(info.DataFile);

            // Recompute sizes/offsets exactly as the on-disk save would (no edits applied).
            game.PostProcessChanges();

            byte[] savedIndex = SaveToBytes(s => game.IndexFile.SaveToBinaryWriter(s));
            byte[] savedData = SaveToBytes(s => game.DataFile.SaveToBinaryWriter(s));

            AssertBytesIdentical(originalIndex, savedIndex, "index (.LA0)");
            AssertBytesIdentical(originalData, savedData, "data (.LA1)");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void IndexDirectoriesLinkToDataBlocks(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            var index = (ScummV7IndexFile)game.IndexFile;

            // Linking sets ItemId on every directory entry that found its data block. If the v7 offset
            // convention (relative to ROOM) or the AKOS-as-costume mapping were wrong, nothing would link
            // and edits could not relocate the right entries. Every game ships scripts and costumes.
            Assert.True(LinkedCount(index.DSCR) > 0, "no script (DSCR) entry linked to a SCRP block");
            Assert.True(LinkedCount(index.DCOS) > 0, "no costume (DCOS) entry linked to an AKOS block");
        }

        [SkippableFact]
        public void RejectsV8CurseOfMonkeyIsland()
        {
            Skip.If(GameLibrary.Folder(GameLibrary.CurseOfMonkeyIsland) == null, "COMI (v8) not present");

            // The Curse of Monkey Island is SCUMM v8 and ships the same COMI.LA0/COMI.LA1 naming and the
            // same plain RNAM/LECF magic as v7, so the content checks pass. The v7 detector must NOT claim
            // it (it is detected as v8 by DetectScummV8, which runs first): it used to be mislabelled "The
            // Dig" (v7) and crash on load (the v8 index has a DRSC block and a larger MAXS). v8 detection
            // itself is asserted by V8SupportTests; here we only guard that the v7 path never grabs it.
            GameInfo info = Functions.FindScummGameInFolder(GameLibrary.Folder(GameLibrary.CurseOfMonkeyIsland));
            Assert.NotEqual(7, info.ScummVersion);
            Assert.NotEqual(ScummGame.TheDig, info.LoadedGame);
            Assert.NotEqual(ScummGame.FullThrottle, info.LoadedGame);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void DirectoryLinkingIsRoomScoped(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            var index = (ScummV7IndexFile)GameLibrary.Load(relativePath).IndexFile;

            // Every directory ItemId must belong to entries of a SINGLE room. Before the room-scoped
            // linking fix, resources at the same relative offset in different rooms cross-linked (the last
            // room processed overwrote the rest), so a size-changing edit relocated the wrong entry. The
            // Dig's DCOS and Full Throttle's DSOU contain such colliding offsets, so this catches it.
            AssertNoCrossRoomLink(index.DSCR, "DSCR");
            AssertNoCrossRoomLink(index.DSOU, "DSOU");
            AssertNoCrossRoomLink(index.DCOS, "DCOS");
            AssertNoCrossRoomLink(index.DCHR, "DCHR");
        }

        private static void AssertNoCrossRoomLink(DirectoryOfItems directory, string name)
        {
            foreach (var group in directory.Rooms.Where(r => !string.IsNullOrEmpty(r.ItemId)).GroupBy(r => r.ItemId))
            {
                int distinctRooms = group.Select(r => r.Number).Distinct().Count();
                Assert.True(distinctRooms == 1,
                    string.Format("{0}: one ItemId links entries from {1} different rooms (cross-room linking)", name, distinctRooms));
            }
        }

        private static int LinkedCount(DirectoryOfItems directory)
        {
            return directory.Rooms.Count(r => !string.IsNullOrEmpty(r.ItemId));
        }

        private static byte[] SaveToBytes(System.Action<Stream> save)
        {
            using (var stream = new MemoryStream())
            {
                save(stream);
                return stream.ToArray();
            }
        }

        private static void AssertBytesIdentical(byte[] expected, byte[] actual, string label)
        {
            if (expected.Length != actual.Length)
            {
                Assert.Fail(string.Format("{0}: length differs - original {1}, rebuilt {2}", label, expected.Length, actual.Length));
            }
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail(string.Format("{0}: first byte differs at offset {1} - original 0x{2:X2}, rebuilt 0x{3:X2}",
                        label, i, expected[i], actual[i]));
                }
            }
        }
    }
}
