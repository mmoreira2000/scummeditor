using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScummEditor.Engine;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Batch graphics IMPORT for SCUMM v2 (Maniac Mansion, Zak McKracken) through the same
    /// ScummV2Graphics.Export/Import path the GUI uses. v2 differs from v3old/v4 in that a background
    /// image and its walk-behind mask share ONE region, so the importer merges an edited background and
    /// an edited z-plane for the same room into a single re-encode.
    /// </summary>
    public class V2GraphicsImportTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2GraphicsExportThenNoOpImportIsByteIdentical(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            WithExport(game, dir =>
            {
                var before = SnapshotAll(game);
                ScummV4GraphicsBatch.ImportReport report = ScummV2Graphics.Import(game, dir, null);
                Assert.Empty(report.Errors);
                foreach (DataDisk disk in game.DataDisks)
                    Assert.True(BytesEqual(before[disk.FilePath], Save(disk.Tree)),
                        "no-op import changed " + Path.GetFileName(disk.FilePath));
            });
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2EditedBackgroundRoundTripsAndPreservesMaskAndOtherRooms(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            int diskIdx, roomNo;
            Skip.If(!FindRoomWithZPlane(game, out diskIdx, out roomNo), "no v2 room with a z-plane found");

            WithExport(game, dir =>
            {
                var snap = SnapshotOthers(game, diskIdx);
                byte[,] maskBefore = DecodeMask(game, diskIdx);

                // Edit only the background PNG (toggle pixel 0,0); drop the rest so only this applies.
                byte expected = 0;
                KeepOnly(dir, string.Format("Room#{0:D3}.png", roomNo), path => expected = ToggleFirstPixel(path));

                ScummV4GraphicsBatch.ImportReport report = ScummV2Graphics.Import(game, dir, null);
                Assert.Empty(report.Errors);

                var df = (ScummV3OldBundleDataFile)game.DataDisks[diskIdx].Tree;
                var room = new ScummV2Room(df.RawContent);
                byte[,] bg = ScummV2ImageDecoder.DecodeRle(df.RawContent, room.ImageOffset, room.Width, room.Height);
                Assert.NotNull(bg);
                Assert.Equal(expected, (byte)(bg[0, 0] & 0x0F)); // the edit landed

                AssertMaskEqual(maskBefore, DecodeMask(game, diskIdx)); // an image edit must not touch the mask
                AssertOthersUnchanged(game, diskIdx, snap);
            });
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2EditedZPlaneRoundTripsAndPreservesBackgroundAndOtherRooms(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            int diskIdx, roomNo;
            Skip.If(!FindRoomWithZPlane(game, out diskIdx, out roomNo), "no v2 room with a z-plane found");

            WithExport(game, dir =>
            {
                var snap = SnapshotOthers(game, diskIdx);
                byte[,] bgBefore = DecodeBackground(game, diskIdx);
                byte[,] maskBefore = DecodeMask(game, diskIdx);

                // Edit only the z-plane PNG (invert it); drop the rest.
                KeepOnly(dir, string.Format("Room#{0:D3} ZPlane#000.png", roomNo), InvertMaskFile);

                ScummV4GraphicsBatch.ImportReport report = ScummV2Graphics.Import(game, dir, null);
                Assert.Empty(report.Errors);

                byte[,] maskAfter = DecodeMask(game, diskIdx);
                Assert.NotNull(maskAfter);
                // The edit must round-trip EXACTLY to the inverted mask (a no-op or corruption fails here;
                // this also guards the EncodeMask 127-literal-cap boundary for a dense inverted mask).
                AssertMaskEqual(InvertMatrix(maskBefore), maskAfter);

                AssertMatrixEqual(bgBefore, DecodeBackground(game, diskIdx)); // a mask edit must not touch the pixels
                AssertOthersUnchanged(game, diskIdx, snap);
            });
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2EditedObjectRoundTripsAndPreservesOtherRooms(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            // Find a room + object with a decodable image.
            int diskIdx = -1, roomNo = -1, objIndex = -1;
            for (int i = 0; i < game.DataDisks.Count && diskIdx < 0; i++)
            {
                var df = game.DataDisks[i].Tree as ScummV3OldBundleDataFile;
                int rn;
                if (df == null || !int.TryParse(Path.GetFileNameWithoutExtension(game.DataDisks[i].FilePath), out rn)) continue;
                var room = new ScummV2Room(df.RawContent);
                for (int j = 0; j < room.NumObjects; j++)
                {
                    using (Bitmap o = new ScummV2ImageDecoder().DecodeObject(room, j))
                    {
                        if (o == null) continue;
                        diskIdx = i; roomNo = rn; objIndex = j; break;
                    }
                }
            }
            Skip.If(diskIdx < 0, "no v2 object image found");

            WithExport(game, dir =>
            {
                var snap = SnapshotOthers(game, diskIdx);
                byte expected = 0;
                KeepOnly(dir, string.Format("Room#{0:D3} Obj#{1:D3} Img#000.png", roomNo, objIndex), path => expected = ToggleFirstPixel(path));

                ScummV4GraphicsBatch.ImportReport report = ScummV2Graphics.Import(game, dir, null);
                Assert.Empty(report.Errors);

                var df = (ScummV3OldBundleDataFile)game.DataDisks[diskIdx].Tree;
                var room = new ScummV2Room(df.RawContent);
                using (Bitmap o = new ScummV2ImageDecoder().DecodeObject(room, objIndex))
                {
                    Assert.NotNull(o);
                    byte[,] m = IndexedImageHelper.GetIndexMatrix(o);
                    Assert.Equal(expected, (byte)(m[0, 0] & 0x0F));
                }
                AssertOthersUnchanged(game, diskIdx, snap);
            });
        }

        /// <summary>
        /// A long literal run (no two adjacent strip bytes equal) must NOT overflow the GdiV2 mask
        /// control byte's 0x80 bit. EncodeMask caps literals at 127; a cap of 128+ would set 0x80 and the
        /// decoder would misread the run as a repeat. (Adversarial-review blocker.)
        /// </summary>
        [Fact]
        public void V2EncodeMaskRoundTripsLongLiteralRun()
        {
            int w = 8, h = 200; // one strip, 200 rows alternating 0x00/0xFF => a single 200-byte literal run
            var mask = new byte[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    mask[x, y] = (byte)(y & 1);

            byte[] encoded = ScummV2ImageEncoder.EncodeMask(mask, w, h);
            byte[,] decoded = ScummV2ImageDecoder.DecodeMaskRle(encoded, 0, w, h);

            Assert.NotNull(decoded);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Assert.Equal(mask[x, y] & 1, decoded[x, y] & 1);
        }

        /// <summary>
        /// A v2 object whose OBIM points at a code (OBCD) block has no real image and must be neither
        /// decoded nor treated as editable, or a re-encode would splice over another object's code.
        /// (Adversarial-review blocker, verified to exist in real Maniac/Zak rooms.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2ImagelessObjectsAreNotOwnedNorDecoded(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV2ImageDecoder();
            int imageless = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                var room = new ScummV2Room(df.RawContent);
                var codeOffsets = new HashSet<int>();
                for (int j = 0; j < room.NumObjects; j++) codeOffsets.Add(room.ObjectCodeOffset(j));

                for (int j = 0; j < room.NumObjects; j++)
                {
                    int obim = room.ObjectImageOffset(j);
                    if (obim <= 0 || !codeOffsets.Contains(obim)) continue; // only the imageless (OBIM == an OBCD)
                    imageless++;
                    Assert.False(ScummV2ImageDecoder.ObjectOwnsImage(room, j), "imageless object reported as owning an image");
                    using (Bitmap o = decoder.DecodeObject(room, j))
                        Assert.Null(o); // must not decode garbage from a code block
                }
            }
            Assert.True(imageless > 0, "expected at least one imageless object (OBIM pointing at an OBCD)");
        }

        /// <summary>
        /// A non-indexed (RGB) PNG carries no palette indexes, so reading it as one would silently corrupt
        /// the region. The batch importer must REJECT it (report an error) and leave the game byte-identical,
        /// matching the per-node OldBundleImageImporter. (Adversarial-review follow-up.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2NonIndexedPngIsRejectedAndLeavesGameUnchanged(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            WithExport(game, dir =>
            {
                string keep = FirstBackgroundPng(dir);
                Skip.If(keep == null, "no plain background PNG was exported");

                var before = SnapshotAll(game);
                KeepOnly(dir, keep, MakeRgbCopy); // overwrite the kept PNG as truecolor RGB

                ScummV4GraphicsBatch.ImportReport report = ScummV2Graphics.Import(game, dir, null);

                Assert.Contains(report.Errors, e => e.Contains("indexed"));
                Assert.Equal(0, report.Imported);
                foreach (DataDisk disk in game.DataDisks)
                    Assert.True(BytesEqual(before[disk.FilePath], Save(disk.Tree)),
                        "a rejected RGB import still changed " + Path.GetFileName(disk.FilePath));
            });
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The first plain "Room#N.png" background (no space = not an object / z-plane file), or null.</summary>
        private static string FirstBackgroundPng(string dir)
        {
            foreach (string f in Directory.GetFiles(dir, "Room#*.png"))
            {
                string name = Path.GetFileName(f);
                if (!name.Contains(" ")) return name;
            }
            return null;
        }

        /// <summary>Rewrites an indexed PNG in place as a truecolor (24bpp RGB) PNG with the same pixels.</summary>
        private static void MakeRgbCopy(string path)
        {
            Bitmap rgb;
            using (var src = (Bitmap)Image.FromFile(path))
            {
                rgb = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(rgb)) g.DrawImage(src, 0, 0, src.Width, src.Height);
            }
            rgb.Save(path, ImageFormat.Png);
            rgb.Dispose();
        }

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
        }

        /// <summary>Exports all graphics to a fresh temp dir, runs <paramref name="body"/>, then deletes the dir.</summary>
        private static void WithExport(ScummGameData game, Action<string> body)
        {
            string dir = Path.Combine(Path.GetTempPath(), "v2gfximp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                int exported = ScummV2Graphics.Export(game, dir, new ScummV4GraphicsBatch.ExportOptions(), null, null);
                Assert.True(exported > 0, "nothing exported");
                body(dir);
            }
            finally { Directory.Delete(dir, true); }
        }

        private static bool FindRoomWithZPlane(ScummGameData game, out int diskIdx, out int roomNo)
        {
            var decoder = new ScummV2ImageDecoder();
            for (int i = 0; i < game.DataDisks.Count; i++)
            {
                var df = game.DataDisks[i].Tree as ScummV3OldBundleDataFile;
                int rn;
                if (df == null || !int.TryParse(Path.GetFileNameWithoutExtension(game.DataDisks[i].FilePath), out rn)) continue;
                var room = new ScummV2Room(df.RawContent);
                if (room.Width <= 0 || room.Height <= 0) continue;
                using (Bitmap z = decoder.DecodeBackgroundZPlane(room))
                {
                    if (z != null && AnyWhite(z)) { diskIdx = i; roomNo = rn; return true; }
                }
            }
            diskIdx = -1; roomNo = -1;
            return false;
        }

        /// <summary>Deletes every PNG except <paramref name="keep"/>, then runs <paramref name="edit"/> on it.</summary>
        private static void KeepOnly(string dir, string keep, Action<string> edit)
        {
            foreach (string f in Directory.GetFiles(dir, "*.png"))
                if (Path.GetFileName(f) != keep) File.Delete(f);
            string path = Path.Combine(dir, keep);
            Assert.True(File.Exists(path), "expected exported PNG missing: " + keep);
            edit(path);
        }

        /// <summary>Toggles the index of pixel (0,0) of an indexed PNG in place; returns the new index.</summary>
        private static byte ToggleFirstPixel(string path)
        {
            byte[,] mtx;
            Color[] palette;
            using (var bmp = (Bitmap)Image.FromFile(path))
            {
                mtx = IndexedImageHelper.GetIndexMatrix(bmp);
                palette = bmp.Palette.Entries;
            }
            byte updated = (byte)((mtx[0, 0] + 1) & 0x0F);
            mtx[0, 0] = updated;
            using (Bitmap edited = IndexedImageHelper.FromIndexMatrix(mtx, palette, -1))
                edited.Save(path, ImageFormat.Png);
            return updated;
        }

        /// <summary>Rewrites a v2 mask PNG in place as its inverse (white &lt;-&gt; black).</summary>
        private static void InvertMaskFile(string path)
        {
            Bitmap outBmp;
            using (var src = (Bitmap)Image.FromFile(path))
            {
                outBmp = new Bitmap(src.Width, src.Height);
                for (int y = 0; y < src.Height; y++)
                    for (int x = 0; x < src.Width; x++)
                        outBmp.SetPixel(x, y, src.GetPixel(x, y).R > 127 ? Color.Black : Color.White);
            }
            outBmp.Save(path, ImageFormat.Png);
            outBmp.Dispose();
        }

        private static byte[,] DecodeBackground(ScummGameData game, int diskIdx)
        {
            var df = (ScummV3OldBundleDataFile)game.DataDisks[diskIdx].Tree;
            var room = new ScummV2Room(df.RawContent);
            return ScummV2ImageDecoder.DecodeRle(df.RawContent, room.ImageOffset, room.Width, room.Height);
        }

        private static byte[,] DecodeMask(ScummGameData game, int diskIdx)
        {
            var df = (ScummV3OldBundleDataFile)game.DataDisks[diskIdx].Tree;
            var room = new ScummV2Room(df.RawContent);
            int gfxLen = ScummV2ImageDecoder.GraphicsRleLength(df.RawContent, room.ImageOffset, room.Width, room.Height);
            return ScummV2ImageDecoder.DecodeMaskRle(df.RawContent, room.ImageOffset + gfxLen, room.Width, room.Height);
        }

        private static Dictionary<string, byte[]> SnapshotAll(ScummGameData game)
        {
            var map = new Dictionary<string, byte[]>();
            foreach (DataDisk disk in game.DataDisks) map[disk.FilePath] = Save(disk.Tree);
            return map;
        }

        private static Dictionary<int, byte[]> SnapshotOthers(ScummGameData game, int diskIdx)
        {
            var map = new Dictionary<int, byte[]>();
            for (int i = 0; i < game.DataDisks.Count; i++)
                if (i != diskIdx) map[i] = Save(game.DataDisks[i].Tree);
            return map;
        }

        private static void AssertOthersUnchanged(ScummGameData game, int diskIdx, Dictionary<int, byte[]> snap)
        {
            for (int i = 0; i < game.DataDisks.Count; i++)
                if (i != diskIdx)
                    Assert.True(BytesEqual(snap[i], Save(game.DataDisks[i].Tree)),
                        "another room changed: " + Path.GetFileName(game.DataDisks[i].FilePath));
        }

        private static bool AnyWhite(Bitmap b)
        {
            for (int y = 0; y < b.Height; y++)
                for (int x = 0; x < b.Width; x++)
                    if (b.GetPixel(x, y).R > 127) return true;
            return false;
        }

        private static byte[,] InvertMatrix(byte[,] mask)
        {
            var m = new byte[mask.GetLength(0), mask.GetLength(1)];
            for (int x = 0; x < mask.GetLength(0); x++)
                for (int y = 0; y < mask.GetLength(1); y++)
                    m[x, y] = (byte)((mask[x, y] & 1) ^ 1);
            return m;
        }

        private static void AssertMatrixEqual(byte[,] a, byte[,] b)
        {
            Assert.NotNull(a); Assert.NotNull(b);
            Assert.Equal(a.GetLength(0), b.GetLength(0));
            Assert.Equal(a.GetLength(1), b.GetLength(1));
            for (int x = 0; x < a.GetLength(0); x++)
                for (int y = 0; y < a.GetLength(1); y++)
                    Assert.Equal(a[x, y] & 0x0F, b[x, y] & 0x0F);
        }

        private static void AssertMaskEqual(byte[,] a, byte[,] b)
        {
            Assert.NotNull(a); Assert.NotNull(b);
            Assert.Equal(a.GetLength(0), b.GetLength(0));
            Assert.Equal(a.GetLength(1), b.GetLength(1));
            for (int x = 0; x < a.GetLength(0); x++)
                for (int y = 0; y < a.GetLength(1); y++)
                    Assert.Equal(a[x, y] & 1, b[x, y] & 1);
        }

        private static byte[] Save(BlockBase tree)
        {
            using (var ms = new MemoryStream()) { tree.SaveToBinaryWriter(ms); return ms.ToArray(); }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
