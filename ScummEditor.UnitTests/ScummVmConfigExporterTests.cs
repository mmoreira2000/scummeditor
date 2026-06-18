using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// The ScummVM launch-profile exporter: gameid mapping and the platform rule that pins the right
    /// graphics variant for an edited game whose index MD5 no longer matches ScummVM's database.
    /// </summary>
    public class ScummVmConfigExporterTests
    {
        [Theory]
        [InlineData(ScummGame.IndianaJones3, "indy3")]
        [InlineData(ScummGame.Loom, "loom")]
        [InlineData(ScummGame.ZakMcKracken, "zak")]
        [InlineData(ScummGame.MonkeyIsland1Floppy, "monkey")]
        [InlineData(ScummGame.MonkeyIsland1VGA, "monkey")]
        [InlineData(ScummGame.MonkeyIsland2, "monkey2")]
        [InlineData(ScummGame.FateOfAtlantis, "atlantis")]
        [InlineData(ScummGame.DayOfTheTentacle, "tentacle")]
        [InlineData(ScummGame.SamAndMax, "samnmax")]
        public void ResolvesGameId(ScummGame game, string expected)
        {
            Assert.Equal(expected, ScummVmConfigExporter.ResolveGameId(game));
        }

        [Fact]
        public void FmTownsV3GamesGetTheFmTownsPlatform()
        {
            // The only v3 releases that ship ripped CD audio are the FM-Towns ones.
            Assert.Equal("fmtowns", ScummVmConfigExporter.ResolvePlatform(
                new GameInfo { LoadedGame = ScummGame.Loom, ScummVersion = 3, HasCdAudio = true }));
            Assert.Equal("fmtowns", ScummVmConfigExporter.ResolvePlatform(
                new GameInfo { LoadedGame = ScummGame.ZakMcKracken, ScummVersion = 3, HasCdAudio = true }));
            Assert.Equal("fmtowns", ScummVmConfigExporter.ResolvePlatform(
                new GameInfo { LoadedGame = ScummGame.IndianaJones3, ScummVersion = 3, HasCdAudio = true }));
        }

        [Fact]
        public void Mi1FloppyEgaGetsPcButVgaGetsNoPlatform()
        {
            // EGA must be pinned (platform=pc) or ScummVM defaults to the VGA variant and garbles it;
            // VGA must have NO platform or it would be filtered out, leaving EGA.
            Assert.Equal("pc", ScummVmConfigExporter.ResolvePlatform(
                new GameInfo { LoadedGame = ScummGame.MonkeyIsland1Floppy, ScummVersion = 4, Edition = GameEdition.FloppyEga }));
            Assert.Null(ScummVmConfigExporter.ResolvePlatform(
                new GameInfo { LoadedGame = ScummGame.MonkeyIsland1Floppy, ScummVersion = 4, Edition = GameEdition.FloppyVga }));
        }

        [Fact]
        public void OtherGamesGetNoPlatform()
        {
            // v3 DOS VGA (no CD audio), and v5/v6 - identified uniquely by their files.
            Assert.Null(ScummVmConfigExporter.ResolvePlatform(
                new GameInfo { LoadedGame = ScummGame.IndianaJones3, ScummVersion = 3, HasCdAudio = false }));
            Assert.Null(ScummVmConfigExporter.ResolvePlatform(
                new GameInfo { LoadedGame = ScummGame.DayOfTheTentacle, ScummVersion = 6 }));
            Assert.Null(ScummVmConfigExporter.ResolvePlatform(
                new GameInfo { LoadedGame = ScummGame.MonkeyIsland2, ScummVersion = 5 }));
        }

        [Fact]
        public void GeneratedIniHasEngineGameidPlatformAndPath()
        {
            var info = new GameInfo { LoadedGame = ScummGame.Loom, ScummVersion = 3, HasCdAudio = true };
            string ini = ScummVmConfigExporter.GenerateIni(info, @"C:\games\loom-fmtowns");

            Assert.Contains("[scummvm]", ini);
            Assert.Contains("engineid=scumm", ini);
            Assert.Contains("gameid=loom", ini);
            Assert.Contains("platform=fmtowns", ini);
            Assert.Contains(@"path=C:\games\loom-fmtowns", ini);
            Assert.Contains("[" + ScummVmConfigExporter.BuildTargetName(info) + "]", ini);
        }

        [Fact]
        public void Mi1VgaIniOmitsPlatformAndWarnsNotToAddIt()
        {
            var info = new GameInfo { LoadedGame = ScummGame.MonkeyIsland1Floppy, ScummVersion = 4, Edition = GameEdition.FloppyVga };
            string ini = ScummVmConfigExporter.GenerateIni(info, @"C:\games\mi1vga");

            Assert.DoesNotContain("platform=", ini);
            Assert.Contains("do NOT add", ini); // the cautionary note for the VGA variant
        }
    }
}
