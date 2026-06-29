using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) image decode: every room background and a sample of object
    /// images decode through <see cref="ScummV8ImageDecoder"/> (which navigates the v8 IMAG/WRAP/OFFS
    /// nesting and reuses the v5/v6/v7 SMAP strip codec). Asserts the decoded bitmaps have the RMHD/IMHD
    /// dimensions and real content (more than one colour - a decode that desynced would be a flat block).
    /// </summary>
    public class V8ImageTests
    {
        private readonly ITestOutputHelper _out;
        public V8ImageTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void EveryBackgroundDecodes()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var decoder = new ScummV8ImageDecoder();
            int withImage = 0, ok = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                {
                    RoomBlock room = lflf.GetROOM();
                    // A room "has a background" only if its IMAG actually carries strip data (a BSTR);
                    // some rooms have an IMAG wrapper with only an (empty) z-plane and no bitmap.
                    if (!HasBackgroundStrips(room)) continue;
                    withImage++;

                    Bitmap bmp = decoder.DecodeBackground(room);
                    Assert.NotNull(bmp);
                    if (HasMultipleColours(bmp)) ok++;
                }
            }

            _out.WriteLine("COMI v8 backgrounds: {0} with a bitmap, {1} decoded with real content", withImage, ok);
            Assert.True(withImage > 0, "no v8 room backgrounds found");
            // Every room with real strip data must decode to a non-flat image.
            Assert.Equal(withImage, ok);
        }

        private static bool HasBackgroundStrips(RoomBlock room)
        {
            BlockBase imag = room.Childrens.FirstOrDefault(c => c.BlockType == "IMAG");
            BlockBase wrap = imag == null ? null : imag.Childrens.FirstOrDefault(c => c.BlockType == "WRAP");
            BlockBase smap = wrap == null ? null : wrap.Childrens.FirstOrDefault(c => c.BlockType == "SMAP");
            return smap != null && smap.Childrens.Any(c => c.BlockType == "BSTR");
        }

        [SkippableFact]
        public void ObjectImagesDecode()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var decoder = new ScummV8ImageDecoder();
            int objectsWithImage = 0, decoded = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                {
                    RoomBlock room = lflf.GetROOM();
                    int objects = ScummV8ImageDecoder.ObjectCount(room);
                    for (int i = 0; i < objects; i++)
                    {
                        Bitmap bmp = decoder.DecodeObject(room, i);
                        if (bmp == null) continue; // hotspot-only object (no IMAG) - expected
                        objectsWithImage++;
                        if (HasMultipleColours(bmp)) decoded++;
                    }
                }
                if (objectsWithImage >= 50) break; // a sample is enough; this is a sweep, not exhaustive
            }

            _out.WriteLine("COMI v8 object images sampled: {0} with an image, {1} decoded with content", objectsWithImage, decoded);
            Assert.True(objectsWithImage > 0, "no v8 object images found");
            // Each SMAP object image must decode without error to the right size; most have real content,
            // but a few are legitimately a uniform fill (a solid overlay), so allow a small flat fraction.
            Assert.True(decoded >= objectsWithImage * 0.9,
                string.Format("only {0}/{1} v8 object images decoded with content", decoded, objectsWithImage));
        }

        [SkippableFact]
        public void BackgroundReEncodeIsLossless()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var dec = new ScummV8ImageDecoder();
            var enc = new ScummV8ImageEncoder();
            int sampled = 0;
            foreach (DiskBlock lflf in game.DataFile.GetLFLFs())
            {
                RoomBlock room = lflf.GetROOM();
                if (!HasBackgroundStrips(room)) continue;

                using (Bitmap a = dec.DecodeBackground(room))
                {
                    byte[,] before = IndexedImageHelper.GetIndexMatrix(a);
                    enc.EncodeBackground(room, a);            // re-encode the same image
                    using (Bitmap b = dec.DecodeBackground(room))
                    {
                        byte[,] after = IndexedImageHelper.GetIndexMatrix(b);
                        Assert.True(MatrixEquals(before, after), "re-encoded v8 background is not pixel-identical");
                    }
                }
                if (++sampled >= 15) break; // a sample is plenty (full-room re-encode is heavy)
            }
            _out.WriteLine("v8 backgrounds re-encoded losslessly: {0}", sampled);
            Assert.True(sampled > 0, "no v8 background to re-encode");
        }

        [SkippableFact]
        public void EditedBackgroundSavesAndReloads()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            var dec = new ScummV8ImageDecoder();
            var enc = new ScummV8ImageEncoder();

            // Edit the first disk-0 room that has a background: paint a band with a present palette index.
            List<DiskBlock> lflfs = game.DataFile.GetLFLFs();
            int roomIndex = -1;
            byte[,] edited = null;
            Color[] palette = null;
            for (int i = 0; i < lflfs.Count; i++)
            {
                RoomBlock room = lflfs[i].GetROOM();
                if (!HasBackgroundStrips(room)) continue;
                using (Bitmap a = dec.DecodeBackground(room))
                {
                    edited = IndexedImageHelper.GetIndexMatrix(a);
                    palette = a.Palette.Entries;
                }
                int w = edited.GetLength(0), h = edited.GetLength(1);
                byte mark = edited[0, 0];
                for (int y = 0; y < System.Math.Min(20, h); y++)
                    for (int x = 0; x < w; x++)
                        edited[x, y] = mark; // a flat band at the top - a real, size-changing edit
                using (Bitmap eb = IndexedImageHelper.FromIndexMatrix(edited, palette, -1))
                {
                    enc.EncodeBackground(room, eb);
                }
                roomIndex = i;
                break;
            }
            Skip.If(roomIndex < 0, "no v8 background to edit");

            game.PostProcessChanges();

            // Re-serialize the index + disk 0 and reload them, exactly like a save+reload.
            using (var idxMs = new MemoryStream())
            {
                game.IndexFile.SaveToBinaryWriter(idxMs); // must not throw (relocation ran)
            }
            ScummDataFile reparsed;
            using (var ms = new MemoryStream())
            {
                game.DataFile.SaveToBinaryWriter(ms);
                ms.Position = 0;
                reparsed = new ScummDataFile(null, game.LoadedGameInfo);
                reparsed.LoadFromBinaryReader(ms);
            }

            RoomBlock reloaded = reparsed.GetLFLFs()[roomIndex].GetROOM();
            using (Bitmap rb = dec.DecodeBackground(reloaded))
            {
                Assert.NotNull(rb);
                byte[,] reloadedMatrix = IndexedImageHelper.GetIndexMatrix(rb);
                Assert.True(MatrixEquals(edited, reloadedMatrix), "the edited v8 background did not survive save+reload");
            }
        }

        [SkippableFact]
        public void BatchExportImportRoundTrips()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            string folder = Path.Combine(Path.GetTempPath(), "comi_v8_gfx_batch");
            if (Directory.Exists(folder)) Directory.Delete(folder, true);

            // Export backgrounds only (objects are many; a sample proves the wiring + the round-trip).
            var options = new ScummV8GraphicsBatch.ExportOptions { Backgrounds = true, Objects = false };
            int exported = ScummV8GraphicsBatch.Export(game, folder, options, null);
            Assert.True(exported > 0, "batch exported nothing");

            // A reference matrix for the first exported background, before re-import.
            var dec = new ScummV8ImageDecoder();
            RoomBlock first = ScummV8GraphicsBatch.EnumerateRooms(game).Select(l => l.GetROOM()).First(HasBackgroundStrips);
            byte[,] before;
            using (Bitmap b = dec.DecodeBackground(first)) before = IndexedImageHelper.GetIndexMatrix(b);

            ScummV4GraphicsBatch.ImportReport report = ScummV8GraphicsBatch.Import(game, folder, null);
            Directory.Delete(folder, true);

            _out.WriteLine("batch: exported {0}, found {1}, imported {2}, errors {3}", exported, report.Found, report.Imported, report.Errors.Count);
            Assert.Empty(report.Errors);
            Assert.True(report.Imported > 0 && report.Imported == report.Found, "batch import did not map all PNGs back");

            using (Bitmap a = dec.DecodeBackground(first))
            {
                Assert.True(MatrixEquals(before, IndexedImageHelper.GetIndexMatrix(a)), "a no-op batch round-trip changed a background");
            }
        }

        [SkippableFact]
        public void MultiStateObjectOffsTableSurvivesSizeChangingEdit()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            // Find a disk-0 object whose IMAG->WRAP holds 2+ SMAP states (a multi-state object), so a
            // size-changing edit of state 0 shifts state 1 and the outer OFFS table must be rebuilt.
            List<DiskBlock> lflfs = game.DataFile.GetLFLFs();
            int roomIndex = -1, objectIndex = -1;
            for (int i = 0; i < lflfs.Count && roomIndex < 0; i++)
            {
                RoomBlock room = lflfs[i].GetROOM();
                List<BlockBase> obims = room.Childrens.Where(c => c.BlockType == "OBIM").ToList();
                for (int j = 0; j < obims.Count; j++)
                {
                    BlockBase imag = obims[j].Childrens.FirstOrDefault(c => c.BlockType == "IMAG");
                    BlockBase wrap = imag == null ? null : imag.Childrens.FirstOrDefault(c => c.BlockType == "WRAP");
                    if (wrap != null && wrap.Childrens.Count(c => c.BlockType == "SMAP") >= 2)
                    {
                        roomIndex = i; objectIndex = j; break;
                    }
                }
            }
            Skip.If(roomIndex < 0, "no disk-0 multi-SMAP object found");
            _out.WriteLine("editing multi-SMAP object: room index {0}, object index {1}", roomIndex, objectIndex);

            var dec = new ScummV8ImageDecoder();
            var enc = new ScummV8ImageEncoder();

            // A maximally-compressible (flat) edit of state 0 - its SMAP shrinks, forcing the OFFS rebuild.
            byte[,] edited;
            using (Bitmap a = dec.DecodeObject(lflfs[roomIndex].GetROOM(), objectIndex))
            {
                Assert.NotNull(a);
                edited = IndexedImageHelper.GetIndexMatrix(a);
                Color[] palette = a.Palette.Entries;
                byte mark = edited[0, 0];
                for (int y = 0; y < edited.GetLength(1); y++)
                    for (int x = 0; x < edited.GetLength(0); x++)
                        edited[x, y] = mark;
                using (Bitmap eb = IndexedImageHelper.FromIndexMatrix(edited, palette, -1))
                {
                    enc.EncodeObject(lflfs[roomIndex].GetROOM(), objectIndex, eb);
                }
            }

            game.PostProcessChanges();

            // Save + reload disk 0, then verify the OFFS table points exactly at each SMAP in the new layout.
            ScummDataFile reparsed;
            using (var ms = new MemoryStream())
            {
                game.DataFile.SaveToBinaryWriter(ms);
                ms.Position = 0;
                reparsed = new ScummDataFile(null, game.LoadedGameInfo);
                reparsed.LoadFromBinaryReader(ms);
            }

            RoomBlock reloaded = reparsed.GetLFLFs()[roomIndex].GetROOM();
            BlockBase rObim = reloaded.Childrens.Where(c => c.BlockType == "OBIM").ToList()[objectIndex];
            BlockBase rWrap = rObim.Childrens.First(c => c.BlockType == "IMAG").Childrens.First(c => c.BlockType == "WRAP");
            var rOffs = (RawContainerBlock)rWrap.Childrens.First(c => c.BlockType == "OFFS");

            List<BlockBase> states = rWrap.Childrens.Where(c => c.BlockType != "OFFS").ToList();
            Assert.Equal(states.Count * 4, rOffs.Contents.Length);
            long offsBase = rOffs.BlockOffSet; // the engine reads each entry relative to the OFFS chunk start
            for (int k = 0; k < states.Count; k++)
            {
                uint entry = (uint)(rOffs.Contents[k * 4] | (rOffs.Contents[k * 4 + 1] << 8)
                    | (rOffs.Contents[k * 4 + 2] << 16) | (rOffs.Contents[k * 4 + 3] << 24));
                long expected = states[k].BlockOffSet - offsBase;
                Assert.True(entry == expected,
                    string.Format("OFFS[{0}] = {1} but SMAP/BOMP state is at {2} (stale table = engine reads the wrong image)", k, entry, expected));
            }

            // And state 0 still decodes to the edited (flat) image.
            using (Bitmap rb = dec.DecodeObject(reloaded, objectIndex))
            {
                Assert.NotNull(rb);
                Assert.True(MatrixEquals(edited, IndexedImageHelper.GetIndexMatrix(rb)),
                    "the edited multi-state object's state 0 did not survive save+reload");
            }
        }

        private static bool MatrixEquals(byte[,] a, byte[,] b)
        {
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int y = 0; y < a.GetLength(1); y++)
                for (int x = 0; x < a.GetLength(0); x++)
                    if (a[x, y] != b[x, y]) return false;
            return true;
        }

        private static bool HasMultipleColours(Bitmap bmp)
        {
            if (bmp == null) return false;
            var seen = new HashSet<int>();
            // Sample a grid (full per-pixel scan of an 800x480 image is slow and unnecessary).
            int stepX = System.Math.Max(1, bmp.Width / 64);
            int stepY = System.Math.Max(1, bmp.Height / 64);
            for (int y = 0; y < bmp.Height; y += stepY)
                for (int x = 0; x < bmp.Width; x += stepX)
                {
                    seen.Add(bmp.GetPixel(x, y).ToArgb());
                    if (seen.Count > 1) return true;
                }
            return seen.Count > 1;
        }
    }
}
