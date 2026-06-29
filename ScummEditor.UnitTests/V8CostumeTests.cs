using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) costumes. v8 uses the SAME AKOS format as v7, so the AKOS
    /// blocks are already typed as CostumeAkos by the v8 container walk and the v7 AkosImageDecoder/Encoder
    /// decode and re-encode them unchanged. This verifies real COMI cels decode and a cel re-encode is
    /// lossless (decode -> ReplaceCel -> decode is pixel-identical).
    /// </summary>
    public class V8CostumeTests
    {
        private readonly ITestOutputHelper _out;
        public V8CostumeTests(ITestOutputHelper o) { _out = o; }

        private static List<BlockBase> AkosBlocks(ScummGameData game)
        {
            var akos = new List<BlockBase>();
            foreach (DataDisk disk in game.DataDisks)
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                    akos.AddRange(lflf.Childrens.Where(c => c.BlockType == "AKOS"));
            return akos;
        }

        [SkippableFact]
        public void CelsDecode()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            List<BlockBase> akos = AkosBlocks(game);
            Assert.True(akos.Count > 0, "no v8 AKOS costumes found");

            int decoded = 0, content = 0;
            foreach (BlockBase a in akos)
            {
                int cels = AkosImageDecoder.GetCelCount(a);
                for (int k = 0; k < cels; k++)
                {
                    Size sz = AkosImageDecoder.GetCelSize(a, k);
                    if (sz.Width * sz.Height <= 4) continue; // placeholder slot
                    Bitmap cel = AkosImageDecoder.DecodeCel(a, k);
                    if (cel == null) continue;
                    decoded++;
                    if (HasContent(cel)) content++;
                    cel.Dispose();
                }
                if (decoded >= 200) break; // a sample is enough
            }
            _out.WriteLine("v8 AKOS cels decoded: {0} ({1} with content)", decoded, content);
            Assert.True(decoded > 0, "no v8 AKOS cels decoded");
            Assert.True(content >= decoded * 0.8, "most v8 cels should have content");
        }

        [SkippableFact]
        public void CelReEncodeIsLossless()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            int checkd = 0;
            foreach (BlockBase a in AkosBlocks(game))
            {
                int cels = AkosImageDecoder.GetCelCount(a);
                for (int k = 0; k < cels; k++)
                {
                    Size sz = AkosImageDecoder.GetCelSize(a, k);
                    if (sz.Width * sz.Height <= 4) continue;

                    byte[,] before = AkosImageDecoder.DecodeCelIndices(a, k);
                    AkosImageEncoder.ReplaceCel(a, k, before);
                    byte[,] after = AkosImageDecoder.DecodeCelIndices(a, k);

                    Assert.Equal(before.GetLength(0), after.GetLength(0));
                    Assert.Equal(before.GetLength(1), after.GetLength(1));
                    for (int y = 0; y < before.GetLength(1); y++)
                        for (int x = 0; x < before.GetLength(0); x++)
                            Assert.Equal(before[x, y], after[x, y]);

                    if (++checkd >= 30) break;
                }
                if (checkd >= 30) break;
            }
            _out.WriteLine("v8 AKOS cels re-encoded losslessly: {0}", checkd);
            Assert.True(checkd > 0, "no v8 cel to re-encode");
        }

        [SkippableFact]
        public void CostumeBatchRoundTrips()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "comi_v8_cost_batch");
            if (System.IO.Directory.Exists(folder)) System.IO.Directory.Delete(folder, true);

            var options = new ScummV8GraphicsBatch.ExportOptions { Backgrounds = false, Objects = false, Costumes = true };
            int exported = ScummV8GraphicsBatch.Export(game, folder, options, null);
            Assert.True(exported > 0, "batch exported no costume cels");

            ScummV4GraphicsBatch.ImportReport report = ScummV8GraphicsBatch.Import(game, folder, null);
            System.IO.Directory.Delete(folder, true);

            _out.WriteLine("costume batch: exported {0}, found {1}, imported {2}, errors {3}", exported, report.Found, report.Imported, report.Errors.Count);
            Assert.Empty(report.Errors);
            Assert.True(report.Imported > 0 && report.Imported == report.Found, "costume batch did not map all cels back");
        }

        private static bool HasContent(Bitmap bmp)
        {
            var seen = new HashSet<int>();
            for (int y = 0; y < bmp.Height; y += System.Math.Max(1, bmp.Height / 32))
                for (int x = 0; x < bmp.Width; x += System.Math.Max(1, bmp.Width / 32))
                {
                    seen.Add(bmp.GetPixel(x, y).ToArgb());
                    if (seen.Count > 1) return true;
                }
            return false;
        }
    }
}
