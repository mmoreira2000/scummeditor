using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// AKOS (v7/v8) cel export/import must preserve the exact per-pixel PALETTE INDEX, even when the palette
    /// has duplicate colors. The pixels carry the raw index (IndexedImageHelper), so our own round-trip is
    /// index-faithful; but an external editor that re-derives a pixel's index from its COLOR on save (e.g.
    /// IDraw3, which keeps the palette order but rewrites pixels by colour) would collapse two indices that
    /// share a colour. The fix makes the exported palette's entries all DISTINCT (an imperceptible tweak on a
    /// duplicate; the game ignores the PNG palette), so every index maps to its own colour and such an editor
    /// round-trips the indices faithfully. These tests guard both the uniqueness and the index preservation.
    /// </summary>
    public class AkosPaletteIndexTests
    {
        private readonly ITestOutputHelper _out;
        public AkosPaletteIndexTests(ITestOutputHelper o) { _out = o; }

        // --- the fix, in isolation: FromIndexMatrix yields a unique palette but keeps the indices -----------
        [Fact]
        public void FromIndexMatrixMakesPaletteUniqueButKeepsIndices()
        {
            var palette = new Color[8];
            for (int i = 0; i < 8; i++) palette[i] = Color.FromArgb(i * 16, i * 16, i * 16);
            palette[1] = palette[0]; // duplicate of 0
            palette[6] = palette[5]; // duplicate of 5

            var indices = new byte[4, 2];
            indices[0, 0] = 0; indices[1, 0] = 1; indices[2, 0] = 5; indices[3, 0] = 6; // uses the dup siblings
            indices[0, 1] = 2; indices[1, 1] = 3; indices[2, 1] = 4; indices[3, 1] = 7;

            using (Bitmap bmp = IndexedImageHelper.FromIndexMatrix(indices, palette, -1))
            {
                // every entry distinct now
                var seen = new HashSet<int>();
                foreach (Color c in bmp.Palette.Entries) Assert.True(seen.Add(c.ToArgb()), "palette still has a duplicate colour");
                // ...but the two duplicated entries only moved by a hair
                Assert.True(NearlyEqual(bmp.Palette.Entries[1], palette[0]), "dup entry perturbed too far");
                Assert.True(NearlyEqual(bmp.Palette.Entries[6], palette[5]), "dup entry perturbed too far");
                // indices survive a real PNG save/reload
                string tmp = Path.Combine(Path.GetTempPath(), "akos_unique.png");
                bmp.Save(tmp, ImageFormat.Png);
                using (var re = new Bitmap(tmp))
                {
                    byte[,] back = IndexedImageHelper.GetIndexMatrix(re);
                    Assert.Equal((byte)1, back[1, 0]);
                    Assert.Equal((byte)6, back[3, 0]);
                    Assert.Equal((byte)5, back[2, 0]);
                }
                File.Delete(tmp);
            }
        }

        // --- real COMI AKOS cels: palette unique + a colour-re-deriving editor keeps every index ------------
        [SkippableFact]
        public void ComiAkosCelsExportWithUniquePaletteAndSurviveAColorRederivingEditor()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            int tested = 0, dupPalettes = 0, collapses = 0; string first = null;
            foreach (BlockBase akos in AllAkos(game))
            {
                if (!AkosImageEncoder.CanEncode(akos)) continue;
                int cc = AkosImageDecoder.GetCelCount(akos);
                for (int c = 0; c < cc && tested < 80; c++)
                {
                    Size sz = AkosImageDecoder.GetCelSize(akos, c);
                    if (sz.Width < 1 || sz.Height < 1) continue;
                    using (Bitmap cel = AkosImageDecoder.DecodeCel(akos, c))
                    {
                        if (cel == null || !IndexedImageHelper.IsIndexed(cel)) continue;
                        Color[] pal = cel.Palette.Entries;
                        if (HasDuplicate(pal)) dupPalettes++; // must be 0 after the fix

                        byte[,] idx = IndexedImageHelper.GetIndexMatrix(cel);
                        byte[,] rederived = RederiveByColor(idx, pal); // what IDraw3-style save produces
                        if (!Same(idx, rederived)) { collapses++; if (first == null) first = "AKOS cel " + c + " index collapsed under a colour-re-deriving editor"; }
                        tested++;
                    }
                }
                if (tested >= 80) break;
            }
            _out.WriteLine("COMI AKOS cels: tested={0} palettesStillWithDuplicates={1} indexCollapses={2}", tested, dupPalettes, collapses);
            Skip.If(tested == 0, "no encodable AKOS cel found");
            Assert.Equal(0, dupPalettes); // the fix: exported palettes are unique
            Assert.True(collapses == 0, "a colour-re-deriving editor would still change an index: " + first);
        }

        // --- our own full round-trip stays index-faithful (decode -> PNG -> import -> ReplaceCel -> decode) --
        [SkippableFact]
        public void FullAkosCelEditRoundTripKeepsExactIndices()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(info == null, "COMI (v8) not present");
            ScummGameData game = ScummGameData.LoadFromGameInfo(info);

            int tested = 0, mismatches = 0; string first = null;
            foreach (BlockBase akos in AllAkos(game))
            {
                if (!AkosImageEncoder.CanEncode(akos)) continue;
                int cc = AkosImageDecoder.GetCelCount(akos);
                for (int c = 0; c < cc && tested < 15; c++)
                {
                    Size sz = AkosImageDecoder.GetCelSize(akos, c);
                    if (sz.Width < 1 || sz.Height < 1) continue;
                    byte[,] want; string tmp = Path.Combine(Path.GetTempPath(), "akos_full.png");
                    using (Bitmap cel = AkosImageDecoder.DecodeCel(akos, c))
                    {
                        if (cel == null || !IndexedImageHelper.IsIndexed(cel)) continue;
                        cel.Save(tmp, ImageFormat.Png);
                    }
                    using (var edited = new Bitmap(tmp)) want = IndexedImageHelper.GetIndexMatrix(edited);
                    File.Delete(tmp);
                    // A few cels decode to an index outside the codec-1 palette size (a separate, pre-existing
                    // decode/encode edge, unrelated to palette-colour uniqueness); skip those here.
                    try { AkosImageEncoder.ReplaceCel(akos, c, want); }
                    catch (ScummEditor.Engine.Exceptions.ImageEncodeException) { continue; }
                    using (Bitmap after = AkosImageDecoder.DecodeCel(akos, c))
                    {
                        if (!Same(want, IndexedImageHelper.GetIndexMatrix(after))) { mismatches++; if (first == null) first = "cel " + c; }
                    }
                    tested++;
                }
                if (tested >= 15) break;
            }
            _out.WriteLine("full AKOS edit round-trips: tested={0} mismatches={1} first={2}", tested, mismatches, first);
            Skip.If(tested == 0, "no encodable AKOS cel found");
            Assert.True(mismatches == 0, "full AKOS edit round-trip changed an index: " + first);
        }

        // --- helpers ---------------------------------------------------------------------------------------
        private static bool NearlyEqual(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) <= 2 && Math.Abs(a.G - b.G) <= 2 && Math.Abs(a.B - b.B) <= 2;
        }

        private static bool HasDuplicate(Color[] pal)
        {
            var seen = new HashSet<int>();
            foreach (Color c in pal) if (!seen.Add(c.ToArgb())) return true;
            return false;
        }

        private static byte[,] RederiveByColor(byte[,] idx, Color[] pal)
        {
            var firstOf = new Dictionary<int, byte>();
            for (int i = 0; i < pal.Length && i < 256; i++) { int k = pal[i].ToArgb(); if (!firstOf.ContainsKey(k)) firstOf[k] = (byte)i; }
            int w = idx.GetLength(0), h = idx.GetLength(1);
            var o = new byte[w, h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) o[x, y] = firstOf[pal[idx[x, y]].ToArgb()];
            return o;
        }

        private static bool Same(byte[,] a, byte[,] b)
        {
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int y = 0; y < a.GetLength(1); y++) for (int x = 0; x < a.GetLength(0); x++) if (a[x, y] != b[x, y]) return false;
            return true;
        }

        private static IEnumerable<BlockBase> AllAkos(ScummGameData game)
        {
            foreach (DataDisk disk in game.DataDisks)
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                    foreach (BlockBase b in Walk(lflf))
                        if (b.BlockType == "AKOS") yield return b;
        }

        private static IEnumerable<BlockBase> Walk(BlockBase b)
        {
            yield return b;
            foreach (BlockBase c in b.Childrens) foreach (BlockBase x in Walk(c)) yield return x;
        }
    }
}
