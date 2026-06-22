using System.Collections.Generic;
using System.Drawing;
using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Batch graphics export/import for SCUMM v1 (ScummV1Graphics). Regression for the bug where v1 batch
    /// export was routed through ScummV2Graphics (the v2 GdiV2 codec) and silently corrupted the GdiV1
    /// tilemap images. These prove the batch path uses the v1 codec (pixel-identical to the per-node decode)
    /// and that an unmodified export re-imports as a clean no-op.
    /// </summary>
    public class V1GraphicsTests
    {
        /// <summary>A batch-exported v1 background is pixel-identical to the per-node ScummV1ImageDecoder decode.</summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1BatchExportUsesTheV1Codec(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);

            string dir = FreshTempDir("v1gfx_export");
            try
            {
                int count = ScummV1Graphics.Export(game, dir, new ScummV4GraphicsBatch.ExportOptions(), null, null);
                Assert.True(count > 20, "expected many exported v1 images, got " + count);

                int verified = 0;
                foreach (DataDisk disk in game.DataDisks)
                {
                    var df = disk.Tree as ScummV3OldBundleDataFile;
                    int roomNo;
                    if (df == null || !int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                    string png = Path.Combine(dir, "Room#" + roomNo + ".png");
                    if (!File.Exists(png)) continue;

                    using (Bitmap expected = decoder.DecodeBackground(new ScummV1Room(df.RawContent)))
                    using (var actual = (Bitmap)Image.FromFile(png))
                    {
                        Assert.NotNull(expected);
                        Assert.True(BitmapsEqual(expected, actual),
                            "batch-exported background differs from the v1 per-node decode (room " + roomNo + ") - wrong codec");
                    }
                    verified++;
                    if (verified >= 8) break;
                }
                Assert.True(verified > 0, "no exported v1 background was found to verify");
            }
            finally { TryDelete(dir); }
        }

        /// <summary>Exporting then re-importing the UNMODIFIED PNGs changes nothing and reports no errors.</summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1BatchExportThenImportIsACleanNoOp(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            string dir = FreshTempDir("v1gfx_roundtrip");
            try
            {
                int exported = ScummV1Graphics.Export(game, dir, new ScummV4GraphicsBatch.ExportOptions(), null, null);
                Assert.True(exported > 0, "nothing exported");

                ScummV4GraphicsBatch.ImportReport report = ScummV1Graphics.Import(game, dir, null);
                Assert.True(report.Found > 0, "import found no PNGs");
                Assert.Empty(report.Errors);
                Assert.Equal(0, report.Imported); // unmodified PNGs are recognised as unchanged and skipped
            }
            finally { TryDelete(dir); }
        }

        /// <summary>
        /// Refutes the "stale frame count" review finding: editing a v1 costume frame must NOT change the
        /// costume's frame count (the count is fixed by the animation structure, not the byte size), and
        /// every later frame must still decode after the edit - so the batch import loop never skips frames.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1CostumeFrameEditPreservesFrameCount(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Skip.If(index == null || index.CostumeDirectory == null, "no costume directory");
            byte[] palette = CostumeImageDecoderV1.DefaultPalette(isManiac);
            var decoder = new CostumeImageDecoderV1();

            var byRoom = new Dictionary<int, ScummV3OldBundleDataFile>();
            foreach (DataDisk disk in game.DataDisks)
            {
                var df0 = disk.Tree as ScummV3OldBundleDataFile;
                int rn;
                if (df0 != null && int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out rn)) byRoom[rn] = df0;
            }

            V3OldResourceDirectory dir = index.CostumeDirectory;
            bool tested = false;
            for (int c = 0; c < dir.Count && !tested; c++)
            {
                int offset = dir.Offsets[c];
                if (offset == 0xFFFF || offset == 0) continue;
                int roomNo = dir.RoomNumbers[c];
                ScummV3OldBundleDataFile df;
                if (!byRoom.TryGetValue(roomNo, out df)) continue;

                CostumeV3Old costume;
                try { costume = new CostumeV3Old(df.RawContent, offset); }
                catch { continue; }
                int n = costume.Frames.Count;
                if (n < 2) continue;

                using (Bitmap frame0 = decoder.Decode(costume.Frames[0], palette))
                {
                    if (frame0 == null) continue;
                    string err;
                    bool ok = OldBundleCostumeImporter.ImportFrame(df, index, roomNo, true, offset, 0, frame0, out err);
                    Assert.True(ok, "frame 0 import failed: " + err);
                }

                var after = new CostumeV3Old(df.RawContent, dir.Offsets[c]); // offset stays stable for the edited costume
                Assert.Equal(n, after.Frames.Count); // count unchanged -> the import loop bound never under-counts
                for (int k = 0; k < after.Frames.Count; k++)
                    using (Bitmap f = decoder.Decode(after.Frames[k], palette))
                        Assert.NotNull(f); // every later frame still decodes (not skipped/corrupted)
                tested = true;
            }
            Assert.True(tested, "no multi-frame v1 costume found to test");
        }

        private static string FreshTempDir(string name)
        {
            string dir = Path.Combine(Path.GetTempPath(), name);
            TryDelete(dir);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void TryDelete(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }

        private static bool BitmapsEqual(Bitmap a, Bitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) return false;
            return true;
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
