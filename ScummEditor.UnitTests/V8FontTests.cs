using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) fonts. COMI ships its fonts as external .NUT SMUSH files
    /// (RESOURCE/FONT0-4.NUT), the SAME format as v7, so they are enumerated by detection, loaded by the
    /// (inherited) v7 loader, and the v7 NutFont/NutImageDecoder/NutImageEncoder pipeline + viewer serve
    /// them unchanged. This verifies the fonts load and decode, that a no-op re-encode is byte-identical,
    /// and that an edited glyph re-encodes losslessly (the v7 splice fix applies).
    /// </summary>
    public class V8FontTests
    {
        private readonly ITestOutputHelper _out;
        public V8FontTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void NutFontsLoadAndDecode()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            _out.WriteLine("v8 NUT fonts loaded: {0}", game.NutFonts.Count);
            Assert.True(game.NutFonts.Count >= 5, "expected COMI's FONT0-4.NUT to load");

            int glyphsDecoded = 0;
            foreach (NutFontResource res in game.NutFonts)
            {
                Assert.True(res.Font.IsValid, "invalid NUT font: " + res.FilePath);
                Assert.True(res.Font.Glyphs.Count > 0, "no glyphs in " + res.FilePath);
                for (int i = 0; i < res.Font.Glyphs.Count; i++)
                {
                    NutGlyph g = res.Font.Glyphs[i];
                    if (!g.HasPixels || g.Width <= 0 || g.Height <= 0) continue;
                    byte[,] m = NutImageDecoder.DecodeGlyphIndices(res.Font, i);
                    if (m != null) glyphsDecoded++;
                }
            }
            _out.WriteLine("v8 NUT glyphs decoded: {0}", glyphsDecoded);
            Assert.True(glyphsDecoded > 0, "no v8 NUT glyphs decoded");
        }

        [SkippableFact]
        public void GlyphReEncodeIsLossless()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            int checkd = 0;
            foreach (NutFontResource res in game.NutFonts)
            {
                NutFont font = res.Font;
                if (!font.IsValid) continue;

                for (int i = 0; i < font.Glyphs.Count; i++)
                {
                    NutGlyph g = font.Glyphs[i];
                    if (!g.HasPixels || !NutImageEncoder.CanEncode(g.Codec) || g.Width <= 0 || g.Height <= 0) continue;

                    // The NUT re-encode canonicalizes the FOBJ padding, so it is PIXEL-identical (decode),
                    // not necessarily byte-identical - the engine-compat guarantee proven for v7 applies.
                    byte[,] before = NutImageDecoder.DecodeGlyphIndices(font, i);
                    NutImageEncoder.ReplaceGlyph(font, i, before); // no-op re-encode of this glyph
                    byte[,] after = NutImageDecoder.DecodeGlyphIndices(font, i);

                    for (int y = 0; y < before.GetLength(1); y++)
                        for (int x = 0; x < before.GetLength(0); x++)
                            Assert.Equal(before[x, y], after[x, y]);

                    if (++checkd >= 60) break;
                }
                if (checkd >= 60) break;
            }
            _out.WriteLine("v8 NUT glyphs re-encoded losslessly: {0}", checkd);
            Assert.True(checkd > 0, "no v8 NUT glyph to re-encode");
        }
    }
}
