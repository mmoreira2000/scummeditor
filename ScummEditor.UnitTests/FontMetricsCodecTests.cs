using System.Collections.Generic;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Per-glyph X/Y draw-offset export/import (FontMetricsCodec, the FontXY feature). Re-importing an
    /// unmodified export is a byte-identical no-op; an edit patches exactly the two offset bytes and is
    /// size-neutral (the bitmap and width/height are untouched).
    /// </summary>
    public class FontMetricsCodecTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1CdVga)] // v5 (CHAR blocks)
        [InlineData(GameLibrary.Loom)]               // v4 (90x.LFL standalone fonts)
        public void MetricsRoundTripAndEditAreSizeNeutral(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            List<Charset> charsets = game.GetAllEditableCharsets();
            Skip.If(charsets == null || charsets.Count == 0, "no editable charsets in this game");

            Charset cs = null;
            foreach (Charset c in charsets) if (c.PresentGlyphCount() > 0) { cs = c; break; }
            Assert.NotNull(cs);

            byte[] before = (byte[])cs.RawContent.Clone();

            // Re-importing an unmodified export changes nothing, byte-for-byte.
            string export = FontMetricsCodec.Export(cs);
            Assert.Contains(":", export);
            List<string> errors;
            int changed = FontMetricsCodec.Import(cs, export, out errors);
            Assert.Empty(errors);
            Assert.Equal(0, changed);
            Assert.Equal(before, cs.RawContent);

            // Edit one present glyph's X/Y offsets.
            Glyph g = null;
            foreach (Glyph gg in cs.Glyphs) if (gg.Present) { g = gg; break; }
            Assert.NotNull(g);
            int index = g.Index, dataOffset = g.DataOffset;
            int newX = g.XOffset == 5 ? 6 : 5;
            int newY = g.YOffset == -3 ? -2 : -3;

            changed = FontMetricsCodec.Import(cs, index.ToString("X2") + ": " + newX + " " + newY, out errors);
            Assert.Empty(errors);
            Assert.Equal(1, changed);
            Assert.Equal(before.Length, cs.RawContent.Length); // size-neutral
            Assert.Equal(unchecked((byte)(sbyte)newX), cs.RawContent[dataOffset + 2]);
            Assert.Equal(unchecked((byte)(sbyte)newY), cs.RawContent[dataOffset + 3]);

            // width/height (offset +0/+1) untouched.
            Assert.Equal(before[dataOffset + 0], cs.RawContent[dataOffset + 0]);
            Assert.Equal(before[dataOffset + 1], cs.RawContent[dataOffset + 1]);

            // Reparse reflects the new offsets.
            Glyph after = null;
            foreach (Glyph gg in cs.Glyphs) if (gg.Index == index) { after = gg; break; }
            Assert.NotNull(after);
            Assert.Equal(newX, after.XOffset);
            Assert.Equal(newY, after.YOffset);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1CdVga)]
        public void ImportReportsBadLinesWithoutAborting(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            List<Charset> charsets = game.GetAllEditableCharsets();
            Skip.If(charsets == null || charsets.Count == 0, "no editable charsets");
            Charset cs = null;
            foreach (Charset c in charsets) if (c.PresentGlyphCount() > 0) { cs = c; break; }
            Assert.NotNull(cs);

            Glyph g = null;
            foreach (Glyph gg in cs.Glyphs) if (gg.Present) { g = gg; break; }

            string text =
                "; a comment\n" +
                "9999: 0 0\n" +                          // unknown/absent glyph (hex 0x9999) -> reported
                g.Index.ToString("X2") + ": 200 0\n" +   // out of range -> reported
                g.Index.ToString("X2") + ": " + (g.XOffset == 1 ? 2 : 1) + " " + g.YOffset + "\n"; // valid edit
            List<string> errors;
            int changed = FontMetricsCodec.Import(cs, text, out errors);

            Assert.Equal(1, changed);          // the one valid line applied
            Assert.True(errors.Count >= 2);    // the absent + out-of-range lines reported
        }

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
        }
    }
}
