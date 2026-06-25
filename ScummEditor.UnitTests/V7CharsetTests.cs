using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 (The Dig, Full Throttle) in-resource CHAR fonts. v7 keeps its dialogue/verb charsets as
    /// CHAR blocks inside the LFLF (same body layout as v5/v6), now typed as Charset so the font viewer
    /// and the existing CharsetPngCodec export/import pipeline work on them. (The external .NUT SMUSH
    /// fonts are a separate resource - see the NUT font tests.)
    /// </summary>
    public class V7CharsetTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void InResourceCharsetsAreTypedAndDecode(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            List<Charset> charsets = CharsetPngCodec.CollectCharsets(game.DataFile);

            // The CHAR blocks must now be typed as Charset (not RawContainerBlock), so CollectCharsets
            // finds them; both v7 games ship several in-resource charsets.
            Assert.True(charsets.Count > 0, "no in-resource CHAR charset found (CHAR not typed as Charset?)");

            foreach (Charset charset in charsets)
            {
                Assert.True(charset.NumChars > 0, "charset declares 0 characters");
                Assert.True(charset.PresentGlyphCount() > 0, "charset decoded no present glyphs");
                Assert.InRange(charset.BitsPerPixel, 1, 8);
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void CharsetPngRoundTripIsByteIdentical(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Charset charset = CharsetPngCodec.CollectCharsets(game.DataFile).First();
            byte[] before = (byte[])charset.RawContent.Clone();

            string dir = Path.Combine(Path.GetTempPath(), "scumm_v7_char_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string png = Path.Combine(dir, "charset.png");
                string guide = Path.Combine(dir, "charset.guide.png");
                CharsetPngCodec.ExportPng(charset, png, guide);

                // Re-importing the unedited atlas must rebuild the exact same bytes (every glyph unchanged).
                CharsetPngCodec.ImportPng(charset, png);

                Assert.Equal(before.Length, charset.RawContent.Length);
                Assert.True(before.SequenceEqual(charset.RawContent),
                    "no-op CHAR export/import changed the charset bytes");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
