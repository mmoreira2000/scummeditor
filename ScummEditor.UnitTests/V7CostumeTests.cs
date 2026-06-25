using System;
using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 (The Dig, Full Throttle) AKOS costumes: each costume's cels are decoded from its AKOS
    /// sub-blocks (AKHD/AKOF/AKCI/AKCD/AKPL/RGBS). The Dig and Full Throttle use cel codec 1 (BYLE RLE,
    /// the v5/v6 column-RLE scheme). This proves the AKOS cels decode to bitmaps without errors.
    /// </summary>
    public class V7CostumeTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void AkosCostumeCelsDecode(string path)
        {
            Skip.If(GameLibrary.Folder(path) == null, "not present: " + path);
            ScummGameData game = GameLibrary.Load(path);

            int akosCount = 0, totalCels = 0, decoded = 0;
            var celsByCodec = new Dictionary<int, int>();
            var decodedByCodec = new Dictionary<int, int>();
            var errors = new List<string>();

            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                foreach (BlockBase child in disk.Childrens)
                {
                    if (child.BlockType != "AKOS") continue;
                    akosCount++;
                    int codec = AkosImageDecoder.GetCodec(child);

                    int cels = AkosImageDecoder.GetCelCount(child);
                    for (int i = 0; i < cels; i++)
                    {
                        totalCels++;
                        celsByCodec[codec] = celsByCodec.GetValueOrDefault(codec) + 1;
                        try
                        {
                            using (Bitmap bmp = AkosImageDecoder.DecodeCel(child, i))
                            {
                                if (bmp != null)
                                {
                                    decoded++;
                                    decodedByCodec[codec] = decodedByCodec.GetValueOrDefault(codec) + 1;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (errors.Count < 10) errors.Add("akos#" + akosCount + " cel#" + i + " codec" + codec + ": " + ex.GetType().Name + " " + ex.Message);
                        }
                    }
                }
            }

            Assert.True(akosCount > 5, "too few AKOS costumes found: " + akosCount);
            Assert.True(totalCels > 50, "too few cels: " + totalCels);
            Assert.True(errors.Count == 0, "cel decode errors:\n" + string.Join("\n", errors));

            // The Dig and Full Throttle use three cel codecs (1 = BYLE RLE, 5 = BOMP, 16 = MAJMIN); each
            // must be present and decode every cel. This guards the regression where only codec 1 was
            // handled, so codec 5/16 costumes (e.g. Full Throttle LFLF 10 and 19) showed blank.
            foreach (int codec in new[] { 1, 5, 16 })
            {
                int cels = celsByCodec.GetValueOrDefault(codec);
                int ok = decodedByCodec.GetValueOrDefault(codec);
                Assert.True(cels > 0, "no codec-" + codec + " cels found");
                Assert.True(ok == cels, string.Format("codec {0}: only {1}/{2} cels decoded", codec, ok, cels));
            }
            Assert.True(decoded == totalCels, string.Format("only {0}/{1} cels decoded overall", decoded, totalCels));

            // Pixel-correctness guard for the codec-1 BYLE-RLE bit split. A 64-colour AKPL costume uses 6
            // colour bits, so its cels can reach palette indices > 31 (=> > 32 distinct colours). The old
            // binary 5/3 split could only reach indices 0-31, capping distinct colours at 32, so this fails
            // unless the 64 -> 6/2 split is correct. (Decode-returns-non-null alone never caught this.)
            int richestCodec1 = 0;
            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                foreach (BlockBase child in disk.Childrens)
                {
                    if (child.BlockType != "AKOS") continue;
                    if (AkosImageDecoder.GetCodec(child) != 1 || AkosImageDecoder.GetColorCount(child) != 64) continue;

                    int n = AkosImageDecoder.GetCelCount(child);
                    for (int i = 0; i < n && richestCodec1 <= 32; i++)
                    {
                        using (Bitmap bmp = AkosImageDecoder.DecodeCel(child, i))
                        {
                            if (bmp != null) richestCodec1 = Math.Max(richestCodec1, DistinctColors(bmp, 40));
                        }
                    }
                    if (richestCodec1 > 32) break;
                }
                if (richestCodec1 > 32) break;
            }
            Assert.True(richestCodec1 > 32,
                "codec-1 64-colour cels never exceed 32 distinct colours - the 64 -> 6/2 bit split is wrong (only 32 palette indices reachable)");
        }

        /// <summary>Counts distinct ARGB colours in the bitmap, stopping once <paramref name="cap"/> is reached.</summary>
        private static int DistinctColors(Bitmap bmp, int cap)
        {
            var seen = new HashSet<int>();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    seen.Add(bmp.GetPixel(x, y).ToArgb());
                    if (seen.Count >= cap) return seen.Count;
                }
            }
            return seen.Count;
        }
    }
}
