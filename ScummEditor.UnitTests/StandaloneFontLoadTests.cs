using System.IO;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// The standalone-font loaders (v3 9N.LFL charsets, v4 90x.LFL fonts) read each file with
    /// File.ReadAllBytes, and the GUI load path is not wrapped in a try-catch. A font enumerated at
    /// detection could be missing/locked by load time; that must not crash the whole game load - the
    /// unreadable file is skipped and the rest of the game (and the other fonts) still load. Mirrors
    /// V7NutFontTests.MissingNutFileIsSkippedDuringLoad for the external .NUT fonts.
    /// </summary>
    public class StandaloneFontLoadTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Vga)]
        public void MissingV3CharsetIsSkippedDuringLoad(string relativePath)
        {
            GameInfo info = GameLibrary.Detect(relativePath);
            Skip.If(info == null, "GameData folder not present: " + relativePath);
            Skip.If(info.FontFiles == null || info.FontFiles.Count == 0, "game has no standalone charset files");

            info.FontFiles.Add(Path.Combine(GameLibrary.Folder(relativePath), "DOES_NOT_EXIST.LFL"));

            ScummGameData game = ScummGameData.LoadFromGameInfo(info); // must not throw
            Assert.True(game.V3Charsets.Count > 0, "no v3 charsets loaded");
            Assert.DoesNotContain(game.V3Charsets, c => c.FilePath != null && c.FilePath.EndsWith("DOES_NOT_EXIST.LFL"));
        }

        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1FloppyVga)]
        public void MissingV4FontIsSkippedDuringLoad(string relativePath)
        {
            GameInfo info = GameLibrary.Detect(relativePath);
            Skip.If(info == null, "GameData folder not present: " + relativePath);
            Skip.If(info.FontFiles == null || info.FontFiles.Count == 0, "game has no standalone font files");

            info.FontFiles.Add(Path.Combine(GameLibrary.Folder(relativePath), "DOES_NOT_EXIST.LFL"));

            ScummGameData game = ScummGameData.LoadFromGameInfo(info); // must not throw
            Assert.True(game.Fonts.Count > 0, "no v4 fonts loaded");
            Assert.DoesNotContain(game.Fonts, f => f.FilePath != null && f.FilePath.EndsWith("DOES_NOT_EXIST.LFL"));
        }
    }
}
