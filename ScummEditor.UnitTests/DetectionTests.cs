using ScummEditor.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Folder detection (Functions.FindScummGameInFolder) over a representative spread of the test
    /// library: every SCUMM version, floppy and CD/talkie editions. The exhaustive sweep over all
    /// games lives in the validation harness; here we pin the well-known ones.
    /// </summary>
    public class DetectionTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1FloppyVga, ScummGame.MonkeyIsland1Floppy, 4, false)]
        [InlineData(GameLibrary.MonkeyIsland1FloppyEga, ScummGame.MonkeyIsland1Floppy, 4, false)]
        [InlineData(GameLibrary.Loom, ScummGame.Loom, 4, false)]
        [InlineData(GameLibrary.MonkeyIsland2Floppy, ScummGame.MonkeyIsland2, 5, false)]
        [InlineData(GameLibrary.MonkeyIsland1CdVga, ScummGame.MonkeyIsland1VGA, 5, false)]
        [InlineData(GameLibrary.FateOfAtlantisFloppy, ScummGame.FateOfAtlantis, 5, false)]
        [InlineData(GameLibrary.FateOfAtlantisCd, ScummGame.FateOfAtlantis, 5, true)]
        [InlineData(GameLibrary.DayOfTheTentacleFloppy, ScummGame.DayOfTheTentacle, 6, false)]
        [InlineData(GameLibrary.DayOfTheTentacleCd, ScummGame.DayOfTheTentacle, 6, true)]
        [InlineData(GameLibrary.SamAndMaxFloppy, ScummGame.SamAndMax, 6, false)]
        [InlineData(GameLibrary.SamAndMaxCd, ScummGame.SamAndMax, 6, true)]
        public void DetectsGameVersionAndEdition(string relativePath, ScummGame expectedGame, int expectedVersion, bool expectedTalkie)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData library folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.Equal(expectedVersion, info.ScummVersion);
            Assert.Equal(expectedTalkie, info.IsTalkie);
        }

        [Fact]
        public void UnknownFolderDetectsNothing()
        {
            // A folder with no SCUMM data must not be mis-detected as a game.
            string empty = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scummeditor_empty_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(empty);
            try
            {
                GameInfo info = Functions.FindScummGameInFolder(empty);
                Assert.True(info == null || info.LoadedGame == ScummGame.None);
            }
            finally
            {
                System.IO.Directory.Delete(empty, true);
            }
        }
    }
}
