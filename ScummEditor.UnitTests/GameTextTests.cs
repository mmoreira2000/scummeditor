using System;
using System.IO;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// The text pipeline must round-trip: exporting a game's strings and importing the unedited file
    /// back is a no-op (nothing changed, nothing rebuilt, no errors). This guards the v4 and v5/v6
    /// extract/import paths that the translation workflow depends on.
    /// </summary>
    public class GameTextTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1FloppyVga)] // v4
        [InlineData(GameLibrary.MonkeyIsland2Floppy)]    // v5
        [InlineData(GameLibrary.DayOfTheTentacleFloppy)] // v6
        public void TextExportThenReimportIsANoOp(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.NotNull(game);

            GameTextCodec codec = GameTextCodec.Default();
            string tmp = Path.Combine(Path.GetTempPath(), "scummeditor_text_" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                int count;
                GameTextImportReport report;
                if (game.LoadedGameInfo.ScummVersion == 4)
                {
                    count = GameTextManager.ExportToFileV4(game, tmp, codec, "noop");
                    report = GameTextManager.ImportFromFileV4(game, tmp);
                }
                else
                {
                    count = GameTextManager.ExportToFile(game.DataFile, tmp, codec, "noop");
                    report = GameTextManager.ImportFromFile(game.DataFile, tmp);
                }

                Assert.True(count > 0, "expected exported strings from " + relativePath);
                Assert.Equal(0, report.StringsChanged);
                Assert.Equal(0, report.BlocksRebuilt);
                Assert.Empty(report.Errors);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }
    }
}
