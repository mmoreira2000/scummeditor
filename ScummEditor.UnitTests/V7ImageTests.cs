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
    /// SCUMM v7 (The Dig, Full Throttle) images: the RMIM/OBIM/PALS blocks are typed (Phase C), and the
    /// SMAP strip codec, z-planes and APAL palette are the same as v5/v6 - only the headers (RMHD, IMHD)
    /// carry the v7 layout. These tests confirm room backgrounds and object images decode, and that the
    /// codec round-trips an image's pixels (decode -> encode -> decode is identical), so editing works.
    /// </summary>
    public class V7ImageTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void RoomBackgroundsAndObjectsDecode(string path)
        {
            Skip.If(GameLibrary.Folder(path) == null, "not present: " + path);
            ScummGameData game = GameLibrary.Load(path);

            int backgrounds = 0, objects = 0, zplanes = 0;
            var errors = new List<string>();

            List<DiskBlock> disks = game.DataFile.GetLFLFs();
            for (int i = 0; i < disks.Count; i++)
            {
                RoomBlock room = disks[i].GetROOM();
                try
                {
                    Bitmap bg = ImageResourceCodec.Decode(room, null, ImageType.Background, 0, 0, 0, 0, false);
                    if (bg != null) { backgrounds++; bg.Dispose(); }

                    List<ZPlane> bgz = room.GetRMIM().GetIM00().GetZPlanes();
                    for (int z = 0; z < bgz.Count; z++)
                    {
                        Bitmap zp = ImageResourceCodec.Decode(room, null, ImageType.ZPlane, 0, 0, z, 0, false);
                        if (zp != null) { zplanes++; zp.Dispose(); }
                    }

                    List<ObjectImage> obims = room.GetOBIMs();
                    for (int j = 0; j < obims.Count; j++)
                    {
                        List<ImageData> imgs = obims[j].GetIMxx();
                        for (int k = 0; k < imgs.Count; k++)
                        {
                            Bitmap img = ImageResourceCodec.Decode(room, null, ImageType.Object, j, k, 0, 0, false);
                            if (img != null) { objects++; img.Dispose(); }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add("room " + i + ": " + ex.GetType().Name + " " + ex.Message);
                }
            }

            Assert.True(errors.Count == 0, "decode errors:\n" + string.Join("\n", errors));
            Assert.True(backgrounds >= 50, "too few room backgrounds decoded: " + backgrounds);
            Assert.True(objects >= 20, "too few object images decoded: " + objects);
        }

        [SkippableFact]
        public void BackgroundCodecRoundTripsPixels()
        {
            Skip.If(GameLibrary.Folder(GameLibrary.TheDig) == null, "The Dig not present");
            ScummGameData game = GameLibrary.Load(GameLibrary.TheDig);

            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                RoomBlock room = disk.GetROOM();
                Bitmap first = ImageResourceCodec.Decode(room, null, ImageType.Background, 0, 0, 0, 0, false);
                if (first == null) continue;

                byte[,] before = IndexedImageHelper.GetIndexMatrix(first);

                // Re-encode the decoded image and decode again: a lossless codec yields identical indices.
                ImageResourceCodec.Encode(room, null, ImageType.Background, 0, 0, 0, first, ImageEncoder.EncodeTypeSettings.AutoDetect);
                first.Dispose();

                Bitmap second = ImageResourceCodec.Decode(room, null, ImageType.Background, 0, 0, 0, 0, false);
                Assert.NotNull(second);
                byte[,] after;
                using (second) after = IndexedImageHelper.GetIndexMatrix(second);

                Assert.Equal(before.GetLength(0), after.GetLength(0));
                Assert.Equal(before.GetLength(1), after.GetLength(1));
                for (int x = 0; x < before.GetLength(0); x++)
                    for (int y = 0; y < before.GetLength(1); y++)
                        if (before[x, y] != after[x, y])
                            Assert.Fail(string.Format("pixel ({0},{1}) differs after re-encode: {2} != {3}", x, y, before[x, y], after[x, y]));

                return; // one decodable room is enough to prove the codec round-trips pixels
            }

            Assert.Fail("no decodable background found");
        }

        [SkippableFact]
        public void GraphicsBatchExportImportRoundTrips()
        {
            Skip.If(GameLibrary.Folder(GameLibrary.TheDig) == null, "The Dig not present");
            ScummGameData game = GameLibrary.Load(GameLibrary.TheDig);

            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "v7_gfx_batch");
            if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true);
            System.IO.Directory.CreateDirectory(dir);

            var opt = new ScummV5V6GraphicsBatch.ExportOptions
            { Backgrounds = true, Objects = true, Costumes = true, BackgroundZPlanes = true, ObjectZPlanes = true };

            int exported = ScummV5V6GraphicsBatch.Export(game.DataFile, dir, opt, null, null);
            Assert.True(exported >= 50, "too few images exported: " + exported);

            // Re-import the editor's own export: backgrounds/objects/z-planes re-encode cleanly. v7 has no
            // COST costumes (AKOS, Phase D), so none were exported and none are imported - no errors.
            ScummV5V6GraphicsBatch.ImportReport report = ScummV5V6GraphicsBatch.Import(game.DataFile, dir, null);
            Assert.True(report.Errors.Count == 0, "import errors:\n" + string.Join("\n", report.Errors));
            Assert.True(report.Imported >= 50, "too few images imported: " + report.Imported);

            // The edited game must recompute offsets and save without throwing.
            Assert.Null(Record.Exception(() => game.PostProcessChanges()));

            try { System.IO.Directory.Delete(dir, true); } catch { }
        }
    }
}
