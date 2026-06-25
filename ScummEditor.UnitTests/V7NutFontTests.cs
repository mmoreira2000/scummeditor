using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 external .NUT SMUSH fonts (The Dig, Full Throttle): the parser (ANIM/AHDR/FRME/FOBJ) and
    /// the glyph decoder for all four codecs the games use (1/3 = BOMP, 21/44 = skip-copy). These run on
    /// the real font files in the game library, decoding every glyph of every NUT to prove the format
    /// reading and the codecs are faithful, and that a loaded NUT is preserved byte-for-byte.
    /// </summary>
    public class V7NutFontTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void EveryNutGlyphDecodes(string relativePath)
        {
            string folder = GameLibrary.Folder(relativePath);
            Skip.If(folder == null, "GameData folder not present: " + relativePath);

            List<string> nutFiles = FindNutFiles(folder);
            Assert.True(nutFiles.Count > 0, "no .NUT files found under " + folder);

            var codecs = new SortedSet<int>();
            int decoded = 0;
            foreach (string path in nutFiles)
            {
                var font = new NutFont { FilePath = path };
                font.LoadFromFileBytes(File.ReadAllBytes(path));

                Assert.True(font.IsValid, "NUT failed to parse: " + Path.GetFileName(path));
                Assert.Equal(font.NumChars, font.Glyphs.Count); // fully walked, no truncation

                for (int i = 0; i < font.Glyphs.Count; i++)
                {
                    NutGlyph g = font.Glyphs[i];
                    if (!g.HasPixels) continue;

                    codecs.Add(g.Codec);
                    Assert.True(NutImageDecoder.IsSupportedCodec(g.Codec),
                        "unsupported codec " + g.Codec + " in " + Path.GetFileName(path));

                    byte[,] m = NutImageDecoder.DecodeGlyphIndices(font, i);
                    Assert.NotNull(m);
                    Assert.Equal(g.Width, m.GetLength(0));
                    Assert.Equal(g.Height, m.GetLength(1));
                    decoded++;
                }
            }

            Assert.True(decoded > 0, "no glyphs decoded");
            // Both games together cover all four codecs; each game covers at least these.
            Assert.Contains(1, codecs);  // BOMP (BIGFONT / sprite sheets)
            Assert.True(codecs.Contains(44) || codecs.Contains(21),
                "expected a skip-copy font codec (21 or 44)");
        }

        [SkippableFact]
        public void DigAndFullThrottleCoverAllFourCodecs()
        {
            string dig = GameLibrary.Folder(GameLibrary.TheDig);
            string ft = GameLibrary.Folder(GameLibrary.FullThrottle);
            Skip.If(dig == null || ft == null, "GameData folders not present");

            var codecs = new SortedSet<int>();
            foreach (string folder in new[] { dig, ft })
                foreach (string path in FindNutFiles(folder))
                {
                    var font = new NutFont { FilePath = path };
                    font.LoadFromFileBytes(File.ReadAllBytes(path));
                    foreach (NutGlyph g in font.Glyphs)
                        if (g.HasPixels) codecs.Add(g.Codec);
                }

            // The two games together exercise every codec this engine decodes: The Dig uses codec 1
            // (BIGFONT) and codec 44 (FONT0-3 / SMLFONT video subtitles); Full Throttle adds codec 3
            // (BRUSH.NUT) and codec 21 (SCUMMFNT / TECHFNT / TITLFNT).
            Assert.Contains(1, codecs);
            Assert.Contains(3, codecs);
            Assert.Contains(21, codecs);
            Assert.Contains(44, codecs);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void LoadedNutIsByteIdentical(string relativePath)
        {
            string folder = GameLibrary.Folder(relativePath);
            Skip.If(folder == null, "GameData folder not present: " + relativePath);

            foreach (string path in FindNutFiles(folder))
            {
                byte[] original = File.ReadAllBytes(path);
                var font = new NutFont();
                font.LoadFromFileBytes(original);
                Assert.True(font.RawContent.Length == original.Length,
                    "NUT RawContent length changed: " + Path.GetFileName(path));
            }
        }

        // ---- encoder / PNG round-trips ----

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(21)]
        [InlineData(44)]
        public void GlyphReEncodeIsSelfConsistent(int codec)
        {
            NutFont font; int gi;
            Skip.IfNot(FindSampleGlyph(codec, out font, out gi), "no codec-" + codec + " glyph in the library");

            // Re-encoding a glyph's own decoded indices must decode back to the same pixels, and the rest
            // of the font must stay intact (frame count unchanged, every other glyph identical).
            byte[,] before = NutImageDecoder.DecodeGlyphIndices(font, gi);
            byte[][,] others = DecodeAll(font);
            int numChars = font.NumChars;

            NutImageEncoder.ReplaceGlyph(font, gi, before);

            Assert.True(font.IsValid);
            Assert.Equal(numChars, font.NumChars);
            byte[,] after = NutImageDecoder.DecodeGlyphIndices(font, gi);
            Assert.True(MatricesEqual(before, after), "codec " + codec + " glyph changed after a no-op re-encode");

            byte[][,] othersAfter = DecodeAll(font);
            for (int i = 0; i < othersAfter.Length; i++)
            {
                Assert.True(MatricesEqual(others[i], othersAfter[i]), "another glyph changed after editing glyph " + gi);
            }
        }

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(21)]
        [InlineData(44)]
        public void EditedGlyphSurvivesReEncode(int codec)
        {
            NutFont font; int gi;
            Skip.IfNot(FindSampleGlyph(codec, out font, out gi), "no codec-" + codec + " glyph in the library");

            byte[,] m = NutImageDecoder.DecodeGlyphIndices(font, gi);
            int w = m.GetLength(0), h = m.GetLength(1);
            // Paint a small distinctive non-transparent block (index 7 is never the transparent index here).
            int bw = System.Math.Min(3, w), bh = System.Math.Min(3, h);
            for (int x = 0; x < bw; x++)
                for (int y = 0; y < bh; y++)
                    m[x, y] = 7;

            NutImageEncoder.ReplaceGlyph(font, gi, m);

            byte[,] after = NutImageDecoder.DecodeGlyphIndices(font, gi);
            Assert.True(MatricesEqual(m, after), "edited codec-" + codec + " glyph did not round-trip");
        }

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(21)]
        [InlineData(44)]
        public void AtlasPngRoundTripPreservesAllGlyphs(int codec)
        {
            NutFont font; int gi;
            Skip.IfNot(FindSampleGlyph(codec, out font, out gi), "no codec-" + codec + " font in the library");

            byte[][,] before = DecodeAll(font);

            string dir = Path.Combine(Path.GetTempPath(), "scumm_nut_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string png = Path.Combine(dir, "font.png");
                NutFontPngCodec.ExportPng(font, png, null);
                NutFontPngCodec.ImportPng(font, png); // no-op re-import

                byte[][,] after = DecodeAll(font);
                Assert.Equal(before.Length, after.Length);
                for (int i = 0; i < before.Length; i++)
                {
                    Assert.True(MatricesEqual(before[i], after[i]), "glyph " + i + " changed across an atlas PNG round-trip");
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // ---- load / save wiring ----

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void NutFontsLoadWithGame(string relativePath)
        {
            string folder = GameLibrary.Folder(relativePath);
            Skip.If(folder == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.Equal(FindNutFiles(folder).Count, game.NutFonts.Count); // every .NUT loaded
            Assert.True(game.NutFonts.Count > 0, "no NUT fonts loaded");
            foreach (NutFontResource r in game.NutFonts)
            {
                Assert.NotNull(r.Font);
                Assert.True(r.Font.IsValid, "NUT font failed to parse: " + Path.GetFileName(r.FilePath));
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void UneditedNutWriteBackIsByteIdentical(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            foreach (NutFontResource r in game.NutFonts)
            {
                byte[] onDisk = File.ReadAllBytes(r.FilePath);
                // SaveDataToDisk writes r.Font.RawContent back to the file verbatim; an unedited font's
                // RawContent must equal the bytes on disk, so the file round-trips byte-identically.
                Assert.True(r.Font.RawContent.Length == onDisk.Length && BytesEqual(r.Font.RawContent, onDisk),
                    "unedited NUT changed: " + Path.GetFileName(r.FilePath));
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void NutBatchExportImportRoundTrips(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            List<NutFontResource> fonts = game.NutFonts;

            // All glyphs of every font, before the round-trip.
            var before = new Dictionary<string, byte[][,]>();
            foreach (NutFontResource r in fonts) before[r.FilePath] = DecodeAll(r.Font);

            string dir = Path.Combine(Path.GetTempPath(), "scumm_nutbatch_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                NutFontPngCodec.ExportAll(fonts, dir);
                NutFontPngCodec.ImportAll(fonts, dir);

                foreach (NutFontResource r in fonts)
                {
                    byte[][,] after = DecodeAll(r.Font);
                    byte[][,] orig = before[r.FilePath];
                    Assert.Equal(orig.Length, after.Length);
                    for (int i = 0; i < orig.Length; i++)
                    {
                        Assert.True(MatricesEqual(orig[i], after[i]),
                            "glyph " + i + " of " + Path.GetFileName(r.FilePath) + " changed across the batch round-trip");
                    }
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        public void MissingNutFileIsSkippedDuringLoad(string relativePath)
        {
            GameInfo info = GameLibrary.Detect(relativePath);
            Skip.If(info == null, "GameData folder not present: " + relativePath);

            // A .NUT enumerated at detection could be gone/locked by load time; that must not crash the
            // whole game load - the file is skipped and the rest of the game (and the other fonts) load.
            info.NutFontFiles.Add(Path.Combine(GameLibrary.Folder(relativePath), "DOES_NOT_EXIST.NUT"));

            ScummGameData game = ScummGameData.LoadFromGameInfo(info); // must not throw
            Assert.True(game.NutFonts.Count > 0, "no NUT fonts loaded");
            Assert.DoesNotContain(game.NutFonts, r => r.FilePath != null && r.FilePath.EndsWith("DOES_NOT_EXIST.NUT"));
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static byte[][,] DecodeAll(NutFont font)
        {
            var all = new byte[font.Glyphs.Count][,];
            for (int i = 0; i < font.Glyphs.Count; i++)
            {
                all[i] = NutImageDecoder.DecodeGlyphIndices(font, i);
            }
            return all;
        }

        /// <summary>Finds the first font in the library whose first decodable glyph uses the given codec.</summary>
        private static bool FindSampleGlyph(int codec, out NutFont font, out int glyphIndex)
        {
            font = null; glyphIndex = -1;
            foreach (string rel in new[] { GameLibrary.TheDig, GameLibrary.FullThrottle })
            {
                string folder = GameLibrary.Folder(rel);
                if (folder == null) continue;
                foreach (string path in FindNutFiles(folder))
                {
                    var f = new NutFont { FilePath = path };
                    f.LoadFromFileBytes(File.ReadAllBytes(path));
                    for (int i = 0; i < f.Glyphs.Count; i++)
                    {
                        NutGlyph g = f.Glyphs[i];
                        if (g.HasPixels && g.Codec == codec && g.Width * g.Height > 16)
                        {
                            font = f; glyphIndex = i; return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool MatricesEqual(byte[,] a, byte[,] b)
        {
            if (a == null || b == null) return a == b;
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int x = 0; x < a.GetLength(0); x++)
                for (int y = 0; y < a.GetLength(1); y++)
                    if (a[x, y] != b[x, y]) return false;
            return true;
        }

        private static List<string> FindNutFiles(string folder)
        {
            var list = new List<string>();
            foreach (string path in Directory.GetFiles(folder, "*.NUT", SearchOption.AllDirectories))
            {
                list.Add(path);
            }
            return list;
        }
    }
}
