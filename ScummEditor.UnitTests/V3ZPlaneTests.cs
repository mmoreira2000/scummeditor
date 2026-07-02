using System.Collections.Generic;
using System.Drawing;
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
    /// Walk-behind (z-plane) export/import round-trips for the two SCUMM v3 sub-families. These lock the
    /// two format bugs found by the verification pass:
    ///  - v3small (GF_OLD256): the per-strip offset table sits at z-plane base +4 with a LE32 size header
    ///    (ScummVM gfx.cpp:2615), NOT the v4 base +2. The on-disk layout is pinned by
    ///    V3SmallRebuiltZPlaneUsesOld256Layout (it FAILS on the pre-fix +2 code); the round-trip test is a
    ///    looser end-to-end / background-intact check (it is self-consistent across decode+encode, so it
    ///    does NOT by itself distinguish +4 from +2 - that is the layout test's job).
    ///  - v3old (GF_OLD_BUNDLE EGA): the z-plane was not exported/imported at all; it lives at
    ///    imageOffset + smapLen with a base +0 offset table (gfx.cpp:2612-2613).
    /// The v4 tests keep the shared base +2 working on BOTH the read (decode) and write (RebuildZPlane) paths.
    /// </summary>
    public class V3ZPlaneTests
    {
        // ---------------------------------------------------------------- v3small (GF_OLD256)

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Vga)]
        [InlineData(GameLibrary.Indy3FmTowns)]
        [InlineData(GameLibrary.ZakFmTowns)]
        [InlineData(GameLibrary.LoomFmTowns)]
        public void V3SmallEditedBackgroundZPlaneRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV4ImageDecoder();
            var encoder = new ScummV4ImageEncoder();

            int tested = 0;
            foreach (IScummRoomContainer container in ScummV4GraphicsBatch.EnumerateRooms(game))
            {
                ScummV4RoomBlock room = container.GetRoom();
                if (room == null) continue;
                RoomHeader hd = room.GetHD();
                if (hd == null || room.GetBM() == null || hd.Width == 0 || hd.Width % 8 != 0 || hd.Height == 0) continue;
                if (decoder.CountBackgroundZPlanes(room) == 0) continue;

                using (Bitmap before = decoder.DecodeBackgroundZPlane(room, 0))
                {
                    if (before == null || !HasMaskedPixel(before)) continue;
                    byte[,] bgBefore = BackgroundMatrix(decoder, room);

                    using (Bitmap edited = Invert(before))
                    {
                        encoder.EncodeBackgroundZPlane(room, 0, edited);
                        using (Bitmap after = decoder.DecodeBackgroundZPlane(room, 0))
                        {
                            Assert.NotNull(after);
                            AssertMaskEqual(edited, after); // the edit round-trips (decode/encode self-consistent)
                        }
                    }

                    // The background image shares the BM block; a z-plane edit must not disturb it.
                    AssertMatrixEqual(bgBefore, BackgroundMatrix(decoder, room));
                }

                if (++tested >= 5) break;
            }
            Assert.True(tested > 0, "no v3small room with a non-empty background z-plane found");
        }

        /// <summary>
        /// After an edit, the rebuilt v3small z-plane must use the GF_OLD256 layout: a LE32 size header
        /// equal to the region length, with the per-strip offset table at +4. This is the invariant a
        /// no-op round-trip cannot check (it never rewrites the bytes).
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Vga)]
        [InlineData(GameLibrary.LoomFmTowns)]
        public void V3SmallRebuiltZPlaneUsesOld256Layout(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV4ImageDecoder();
            var encoder = new ScummV4ImageEncoder();

            int tested = 0;
            foreach (IScummRoomContainer container in ScummV4GraphicsBatch.EnumerateRooms(game))
            {
                ScummV4RoomBlock room = container.GetRoom();
                if (room == null) continue;
                RoomHeader hd = room.GetHD();
                ScummV4ImageBlock bm = room.GetBM();
                if (hd == null || bm == null || hd.Width == 0 || hd.Width % 8 != 0 || hd.Height == 0) continue;
                if (decoder.CountBackgroundZPlanes(room) == 0) continue;

                using (Bitmap before = decoder.DecodeBackgroundZPlane(room, 0))
                {
                    if (before == null || !HasMaskedPixel(before)) continue;
                    using (Bitmap edited = Invert(before))
                    {
                        encoder.EncodeBackgroundZPlane(room, 0, edited);
                    }
                }

                byte[] c = bm.Contents;
                int numStrips = hd.Width / 8;
                int baseIndex = bm.StripTableStart;
                long smapLen = ReadU32(c, baseIndex);
                int zp = (int)(baseIndex + smapLen);

                // Header is a LE32 size word equal to the region length (the z-plane is the block's tail).
                long header = ReadU32(c, zp);
                Assert.Equal(c.Length - zp, header);

                // The per-strip offset table is at +4, and every masked strip has a non-zero offset that
                // points inside the region (past the header + table) - i.e. NOT the v4 base +2.
                bool anyNonZero = false;
                for (int n = 0; n < numStrips; n++)
                {
                    int off = ReadU16(c, zp + 4 + n * 2);
                    if (off != 0) { anyNonZero = true; Assert.InRange(off, 4 + numStrips * 2, (int)header); }
                }
                Assert.True(anyNonZero, "an inverted mask must produce at least one masked strip");

                if (++tested >= 3) break;
            }
            Assert.True(tested > 0, "no v3small room with a non-empty background z-plane found");
        }

        // ---------------------------------------------------------------- v3old (GF_OLD_BUNDLE EGA)

        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga)]
        [InlineData(GameLibrary.Indy3Ega)]
        public void V3OldEditedBackgroundZPlaneRoundTripsAndPreservesEverythingElse(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            var decoder = new ScummV3OldImageDecoder();

            int tested = 0;
            for (int i = 0; i < game.DataDisks.Count; i++)
            {
                var df = game.DataDisks[i].Tree as ScummV3OldBundleDataFile;
                int roomNo;
                if (df == null || !int.TryParse(Path.GetFileNameWithoutExtension(game.DataDisks[i].FilePath), out roomNo)) continue;
                var room = new ScummV3OldRoom(df.RawContent);
                if (decoder.CountBackgroundZPlanes(room) == 0) continue;

                using (Bitmap original = decoder.DecodeBackgroundZPlane(room))
                {
                    if (original == null || !HasMaskedPixel(original)) continue;
                    byte[,] bgBefore = BackgroundMatrix(decoder, room);

                    // Snapshot every OTHER room to prove the edit is local.
                    var others = new Dictionary<int, byte[]>();
                    for (int k = 0; k < game.DataDisks.Count; k++)
                        if (k != i) others[k] = Save(game.DataDisks[k].Tree);

                    int smapLen = ReadU16(df.RawContent, room.ImageOffset);
                    int zbase = room.ImageOffset + smapLen;
                    int regionEnd = room.NextStructuralOffsetAbove(room.ImageOffset);

                    using (Bitmap edited = Invert(original))
                    {
                        byte[] newRegion = ScummV3OldZPlaneEncoder.Encode(room.Width, room.Height, edited);
                        ScummV3OldWriter.ApplyEdit(df, index, roomNo, zbase, regionEnd - zbase, newRegion, -1);

                        var room2 = new ScummV3OldRoom(df.RawContent);
                        using (Bitmap after = decoder.DecodeBackgroundZPlane(room2))
                        {
                            Assert.NotNull(after);
                            AssertMaskEqual(edited, after);
                        }
                        AssertMatrixEqual(bgBefore, BackgroundMatrix(decoder, room2));
                    }

                    for (int k = 0; k < game.DataDisks.Count; k++)
                        if (k != i) Assert.True(BytesEqual(others[k], Save(game.DataDisks[k].Tree)), "another room changed");
                }

                if (++tested >= 4) break;
            }
            Assert.True(tested > 0, "no v3old room with a non-empty background z-plane found");
        }

        /// <summary>A full export then unedited re-import through the batch pipeline must leave every room byte-identical.</summary>
        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga)]
        [InlineData(GameLibrary.Indy3Ega)]
        public void V3OldZPlaneExportThenNoOpImportIsByteIdentical(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            string dir = Path.Combine(Path.GetTempPath(), "v3oldz_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var options = new ScummV4GraphicsBatch.ExportOptions();
                int exported = ScummV3OldGraphics.Export(game, dir, options, null, null);
                int zpPngs = Directory.GetFiles(dir, "*ZP#000.png").Length;
                Assert.True(zpPngs > 0, "no z-plane PNGs were exported");

                var before = new Dictionary<string, byte[]>();
                foreach (DataDisk disk in game.DataDisks) before[disk.FilePath] = Save(disk.Tree);

                ScummV4GraphicsBatch.ImportReport report = ScummV3OldGraphics.Import(game, dir, null);
                Assert.Empty(report.Errors);

                foreach (DataDisk disk in game.DataDisks)
                    Assert.True(BytesEqual(before[disk.FilePath], Save(disk.Tree)),
                        "no-op import changed " + Path.GetFileName(disk.FilePath));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// Multiple v3old objects can point at ONE OBIM (one shared image + z-plane region). Importing
        /// CONFLICTING edits for two such objects cannot store both; the importer must REPORT the conflict
        /// rather than silently dropping one and claiming success. (Adversarial-review finding.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3OldConflictingSharedObjectZPlaneEditsAreReportedNotSilentlyDropped(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV3OldImageDecoder();

            // Find a room with two distinct objects that share one OBIM offset and both carry a z-plane.
            int roomIndex = -1, ja = -1, jb = -1;
            for (int i = 0; i < game.DataDisks.Count && roomIndex < 0; i++)
            {
                var df = game.DataDisks[i].Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                var room = new ScummV3OldRoom(df.RawContent);
                var byOffset = new Dictionary<int, int>();
                for (int j = 0; j < room.NumObjects; j++)
                {
                    if (decoder.CountObjectZPlanes(room, j) == 0) continue;
                    int obim = room.ObjectImageOffset(j);
                    int prev;
                    if (byOffset.TryGetValue(obim, out prev)) { roomIndex = i; ja = prev; jb = j; break; }
                    byOffset[obim] = j;
                }
            }
            Skip.If(roomIndex < 0, "no shared-OBIM object pair with a z-plane found");

            string dir = Path.Combine(Path.GetTempPath(), "v3oldzc_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var options = new ScummV4GraphicsBatch.ExportOptions { Backgrounds = false, Objects = false, Costumes = false, BackgroundZPlanes = false, ObjectZPlanes = true };
                ScummV3OldGraphics.Export(game, dir, options, null, null);

                string pa = Path.Combine(dir, string.Format("Room#{0:D3} Obj#{1:D3} Img#000 ZP#000.png", roomIndex, ja));
                string pb = Path.Combine(dir, string.Format("Room#{0:D3} Obj#{1:D3} Img#000 ZP#000.png", roomIndex, jb));
                Skip.If(!File.Exists(pa) || !File.Exists(pb), "shared-object z-plane PNGs were not exported");

                // pa = full invert (differs from the original); pb = full invert with one pixel toggled
                // (differs from the original AND from pa) -> two conflicting edits at the same shared region.
                InvertMaskFile(pa, false);
                InvertMaskFile(pb, true);

                ScummV4GraphicsBatch.ImportReport report = ScummV3OldGraphics.Import(game, dir, null);

                Assert.Contains(report.Errors, e => e.Contains("shared") && e.Contains("conflicting"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // ---------------------------------------------------------------- v4 regression guard

        /// <summary>
        /// The v3small fix is in code shared with v4. v4 must keep the GF_SMALL_HEADER base +2: its
        /// z-planes still decode and a no-op re-encode stays byte-identical.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1FloppyVga)]
        public void V4BackgroundZPlanesStillDecodeAndNoOpReEncodeIsByteIdentical(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV4ImageDecoder();
            var encoder = new ScummV4ImageEncoder();

            int tested = 0;
            foreach (IScummRoomContainer container in ScummV4GraphicsBatch.EnumerateRooms(game))
            {
                ScummV4RoomBlock room = container.GetRoom();
                if (room == null) continue;
                RoomHeader hd = room.GetHD();
                ScummV4ImageBlock bm = room.GetBM();
                if (hd == null || bm == null || hd.Width == 0 || hd.Width % 8 != 0 || hd.Height == 0) continue;
                int count = decoder.CountBackgroundZPlanes(room);
                if (count == 0) continue;

                byte[] contentsBefore = (byte[])bm.Contents.Clone();
                for (int z = 0; z < count; z++)
                {
                    using (Bitmap mask = decoder.DecodeBackgroundZPlane(room, z))
                    {
                        Assert.NotNull(mask);
                        encoder.EncodeBackgroundZPlane(room, z, mask); // no-op: same mask
                    }
                }
                Assert.True(BytesEqual(contentsBefore, bm.Contents), "a no-op v4 z-plane re-encode changed the block");

                if (++tested >= 5) break;
            }
            Assert.True(tested > 0, "no v4 room with a z-plane found");
        }

        /// <summary>
        /// Exercises the v4 z-plane WRITE path (RebuildZPlane), which the no-op test skips via the
        /// MaskUnchanged short-circuit. An edited (inverted) mask must round-trip AND the rebuilt plane
        /// must keep the GF_SMALL_HEADER layout: a LE16 size header with the per-strip offset table at +2
        /// (the strip data starts right after a 2-byte-header table, NOT the v3 GF_OLD256 4-byte one).
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1FloppyVga)]
        public void V4EditedBackgroundZPlaneRoundTripsAndKeepsBasePlus2(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV4ImageDecoder();
            var encoder = new ScummV4ImageEncoder();

            int tested = 0;
            foreach (IScummRoomContainer container in ScummV4GraphicsBatch.EnumerateRooms(game))
            {
                ScummV4RoomBlock room = container.GetRoom();
                if (room == null) continue;
                RoomHeader hd = room.GetHD();
                ScummV4ImageBlock bm = room.GetBM();
                if (hd == null || bm == null || hd.Width == 0 || hd.Width % 8 != 0 || hd.Height == 0) continue;
                if (decoder.CountBackgroundZPlanes(room) == 0) continue;

                using (Bitmap before = decoder.DecodeBackgroundZPlane(room, 0))
                {
                    if (before == null || !HasMaskedPixel(before)) continue;
                    using (Bitmap edited = Invert(before))
                    {
                        encoder.EncodeBackgroundZPlane(room, 0, edited); // invert != original => RebuildZPlane runs
                        using (Bitmap after = decoder.DecodeBackgroundZPlane(room, 0))
                        {
                            Assert.NotNull(after);
                            AssertMaskEqual(edited, after);
                        }
                    }
                }

                int numStrips = hd.Width / 8;
                var regions = bm.GetZPlaneRegions(numStrips, room.IsEga);
                Assert.NotEmpty(regions);
                int zp = regions[0].Start;
                Assert.Equal(regions[0].Length, ReadU16(bm.Contents, zp)); // LE16 size header
                int minOff = int.MaxValue;
                for (int n = 0; n < numStrips; n++)
                {
                    int off = ReadU16(bm.Contents, zp + 2 + n * 2); // base +2 (v4 GF_SMALL_HEADER)
                    if (off != 0 && off < minOff) minOff = off;
                }
                Assert.NotEqual(int.MaxValue, minOff); // an inverted mask must produce a masked strip
                Assert.Equal(2 + numStrips * 2, minOff); // data starts right after the +2 table, not a +4 one

                if (++tested >= 3) break;
            }
            Assert.True(tested > 0, "no v4 room with a non-empty background z-plane found");
        }

        // ---------------------------------------------------------------- helpers

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
        }

        private static byte[,] BackgroundMatrix(ScummV4ImageDecoder decoder, ScummV4RoomBlock room)
        {
            using (Bitmap bg = decoder.DecodeBackground(room)) return IndexedImageHelper.GetIndexMatrix(bg);
        }

        private static byte[,] BackgroundMatrix(ScummV3OldImageDecoder decoder, ScummV3OldRoom room)
        {
            using (Bitmap bg = decoder.DecodeBackground(room)) return IndexedImageHelper.GetIndexMatrix(bg);
        }

        private static bool HasMaskedPixel(Bitmap mask)
        {
            for (int y = 0; y < mask.Height; y++)
                for (int x = 0; x < mask.Width; x++)
                    if (IsMasked(mask.GetPixel(x, y))) return true;
            return false;
        }

        /// <summary>A mask whose masked/unmasked pixels are swapped (so the edit is guaranteed non-trivial).</summary>
        private static Bitmap Invert(Bitmap mask)
        {
            var result = new Bitmap(mask.Width, mask.Height);
            for (int y = 0; y < mask.Height; y++)
                for (int x = 0; x < mask.Width; x++)
                    result.SetPixel(x, y, IsMasked(mask.GetPixel(x, y)) ? Color.White : Color.Black);
            return result;
        }

        private static bool IsMasked(Color p)
        {
            return p.A != 0 && p.R == 0 && p.G == 0 && p.B == 0;
        }

        /// <summary>Rewrites a mask PNG in place as the inverse of its current pixels, optionally toggling pixel (0,0).</summary>
        private static void InvertMaskFile(string path, bool toggleFirstPixel)
        {
            Bitmap outBmp;
            using (var src = (Bitmap)Image.FromFile(path))
            {
                outBmp = new Bitmap(src.Width, src.Height);
                for (int y = 0; y < src.Height; y++)
                    for (int x = 0; x < src.Width; x++)
                        outBmp.SetPixel(x, y, IsMasked(src.GetPixel(x, y)) ? Color.White : Color.Black);
            }
            if (toggleFirstPixel)
                outBmp.SetPixel(0, 0, IsMasked(outBmp.GetPixel(0, 0)) ? Color.White : Color.Black);
            outBmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            outBmp.Dispose();
        }

        private static void AssertMaskEqual(Bitmap expected, Bitmap actual)
        {
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            for (int y = 0; y < expected.Height; y++)
                for (int x = 0; x < expected.Width; x++)
                    Assert.Equal(IsMasked(expected.GetPixel(x, y)), IsMasked(actual.GetPixel(x, y)));
        }

        private static void AssertMatrixEqual(byte[,] a, byte[,] b)
        {
            Assert.Equal(a.GetLength(0), b.GetLength(0));
            Assert.Equal(a.GetLength(1), b.GetLength(1));
            for (int x = 0; x < a.GetLength(0); x++)
                for (int y = 0; y < a.GetLength(1); y++)
                    Assert.Equal(a[x, y] & 0x0F, b[x, y] & 0x0F);
        }

        private static byte[] Save(BlockBase tree)
        {
            using (var ms = new MemoryStream())
            {
                tree.SaveToBinaryWriter(ms);
                return ms.ToArray();
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static int ReadU16(byte[] d, int p) { return d[p] | (d[p + 1] << 8); }
        private static long ReadU32(byte[] d, int p) { return (long)d[p] | ((long)d[p + 1] << 8) | ((long)d[p + 2] << 16) | ((long)d[p + 3] << 24); }
    }
}
