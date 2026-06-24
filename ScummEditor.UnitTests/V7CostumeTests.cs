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

            int akosCount = 0, totalCels = 0, decoded = 0, codec1 = 0;
            var errors = new List<string>();

            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                foreach (BlockBase child in disk.Childrens)
                {
                    if (child.BlockType != "AKOS") continue;
                    akosCount++;
                    if (AkosImageDecoder.GetCodec(child) == 1) codec1++;

                    int cels = AkosImageDecoder.GetCelCount(child);
                    for (int i = 0; i < cels; i++)
                    {
                        totalCels++;
                        try
                        {
                            using (Bitmap bmp = AkosImageDecoder.DecodeCel(child, i))
                            {
                                if (bmp != null) decoded++;
                            }
                        }
                        catch (Exception ex)
                        {
                            if (errors.Count < 10) errors.Add("akos#" + akosCount + " cel#" + i + ": " + ex.GetType().Name + " " + ex.Message);
                        }
                    }
                }
            }

            Assert.True(akosCount > 5, "too few AKOS costumes found: " + akosCount);
            Assert.True(codec1 > 0, "no codec-1 AKOS costumes found");
            Assert.True(totalCels > 50, "too few cels: " + totalCels);
            Assert.True(errors.Count == 0, "cel decode errors:\n" + string.Join("\n", errors));
            Assert.True(decoded > 50, string.Format("only {0}/{1} cels decoded", decoded, totalCels));
        }
    }
}
