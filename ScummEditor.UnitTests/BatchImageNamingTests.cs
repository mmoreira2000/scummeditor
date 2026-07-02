using System.IO;
using System.Text.RegularExpressions;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Batch image export names its files with zero-padded numeric tokens (Room#001, Room#010, Room#100)
    /// so a directory listing sorts them in numeric order. These tests pin the padding helper
    /// (BatchImageNaming) and prove that a real batch export produces padded names AND that a folder
    /// exported by an older, unpadded version still re-imports cleanly (the import resolver's fallback).
    /// </summary>
    public class BatchImageNamingTests
    {
        // ---- pure helper behaviour ----

        [Theory]
        [InlineData("Room#005.png", "Room#5.png")]
        [InlineData("Room#010 Obj#000 Img#000 ZP#000.png", "Room#10 Obj#0 Img#0 ZP#0.png")]
        [InlineData("Room#100.png", "Room#100.png")]                 // already >= 3 digits: unchanged
        [InlineData("Costume#003 FrameIndex#012.png", "Costume#3 FrameIndex#12.png")]
        [InlineData("charset_003.png", "charset_3.png")]
        [InlineData("nutfont_000_FONT0.png", "nutfont_0_FONT0.png")] // the base name's own "0" is not a leading zero
        [InlineData("Room#000.png", "Room#0.png")]                   // an all-zero run collapses to a single 0
        public void StripLeadingZerosDerivesTheLegacyName(string padded, string expectedLegacy)
        {
            Assert.Equal(expectedLegacy, BatchImageNaming.StripLeadingZeros(padded));
        }

        [Fact]
        public void ResolveForImportPrefersThePaddedName()
        {
            string dir = FreshDir();
            try
            {
                string padded = Path.Combine(dir, "Room#005.png");
                File.WriteAllText(padded, "x");
                Assert.Equal(padded, BatchImageNaming.ResolveForImport(dir, "Room#005.png"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void ResolveForImportFallsBackToTheLegacyUnpaddedName()
        {
            string dir = FreshDir();
            try
            {
                string legacy = Path.Combine(dir, "Room#5.png"); // a folder exported by an older version
                File.WriteAllText(legacy, "x");
                Assert.Equal(legacy, BatchImageNaming.ResolveForImport(dir, "Room#005.png"));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void ResolveForImportReturnsThePaddedPathWhenNothingExists()
        {
            string dir = FreshDir();
            try
            {
                string resolved = BatchImageNaming.ResolveForImport(dir, "Room#005.png");
                Assert.Equal(Path.Combine(dir, "Room#005.png"), resolved);
                Assert.False(File.Exists(resolved)); // caller then skips the missing file
            }
            finally { Directory.Delete(dir, true); }
        }

        // ---- end-to-end on a real game (v2 uses the padded export + the resolver's legacy fallback) ----

        /// <summary>Every numeric token in a real v2 batch export is at least three digits, and a copy of the
        /// export renamed to the old unpadded scheme still re-imports as a clean no-op via the fallback.</summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2ExportIsZeroPaddedAndLegacyNamesStillImport(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);

            string dir = FreshDir();
            string legacyDir = FreshDir();
            try
            {
                int exported = ScummV2Graphics.Export(game, dir, new ScummV4GraphicsBatch.ExportOptions(), null, null);
                Assert.True(exported > 0, "nothing exported");

                string[] files = Directory.GetFiles(dir, "*.png");
                Assert.NotEmpty(files);
                bool sawPadding = false;
                foreach (string f in files)
                {
                    string fn = Path.GetFileName(f);
                    foreach (Match m in Regex.Matches(fn, @"#(\d+)"))
                    {
                        Assert.True(m.Groups[1].Value.Length >= 3, fn + " has a numeric token narrower than 3 digits");
                        if (m.Groups[1].Value[0] == '0') sawPadding = true;
                    }
                }
                Assert.True(sawPadding, "no zero-padded token was produced (expected low room/object numbers to pad)");

                // Recreate the folder with the OLD unpadded names and confirm the importer still finds them.
                foreach (string f in files)
                    File.Copy(f, Path.Combine(legacyDir, BatchImageNaming.StripLeadingZeros(Path.GetFileName(f))));

                ScummV4GraphicsBatch.ImportReport report = ScummV2Graphics.Import(game, legacyDir, null);
                Assert.Empty(report.Errors);
                Assert.Equal(0, report.Imported); // unmodified export -> nothing re-imported, even under the legacy names
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
                try { Directory.Delete(legacyDir, true); } catch { }
            }
        }

        private static string FreshDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "scummeditor_padnames_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
