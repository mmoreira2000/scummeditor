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
    /// Real-data tests for (1) the per-node v2/v3-old costume frame import (OldBundleCostumeImporter): a
    /// frame's own decoded pixels round-trip losslessly through encode + ApplyEdit + re-decode; and (2) the
    /// v3 batch font export/import (CharsetV3PngCodec.ExportAll/ImportAll), the path the GUI font menu now
    /// uses for v3old + GF_OLD256. In-memory only (no on-disk game is modified). Skips without the library.
    /// </summary>
    public class CostumeAndFontBatchTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga)]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void CostumeFrameImportRoundTripsLossless(string rel)
        {
            Skip.IfNot(GameLibrary.Available, "GameData library not present");
            ScummGameData game = GameLibrary.Load(rel);
            Skip.If(game == null, "game folder missing");
            var index = (ScummV3OldBundleIndexFile)game.IndexFile;
            bool isV2 = game.LoadedGameInfo.ScummVersion <= 2;
            var ega = new Color[16];
            System.Array.Copy(EgaColorTable.Colors256, ega, 16);

            Dictionary<int, ScummV3OldBundleDataFile> byRoom = MapRooms(game);
            V3OldResourceDirectory dir = index.CostumeDirectory;
            Skip.If(dir == null, "no costume directory");

            int tested = 0;
            for (int c = 0; c < dir.Count && tested < 3; c++)
            {
                ScummV3OldBundleDataFile df;
                if (!byRoom.TryGetValue(dir.RoomNumbers[c], out df)) continue;
                int offset = dir.Offsets[c];
                if (offset == 0xFFFF || offset == 0) continue;
                int roomNo = dir.RoomNumbers[c];

                CostumeV3Old costume;
                try { costume = new CostumeV3Old(df.RawContent, offset); } catch { continue; }
                if (costume.Frames.Count == 0) continue;

                using (Bitmap before = new CostumeImageDecoderV4().Decode(costume.Frames[0], 16, ega, false))
                {
                    if (before == null) continue;

                    string error;
                    bool ok = OldBundleCostumeImporter.ImportFrame(df, index, roomNo, isV2, offset, 0, before, out error);
                    Assert.True(ok, rel + " costume " + c + ": import failed: " + error);

                    var after = new CostumeV3Old(df.RawContent, offset); // offset of an edited resource does not move
                    using (Bitmap reDecoded = new CostumeImageDecoderV4().Decode(after.Frames[0], 16, ega, false))
                    {
                        Assert.NotNull(reDecoded);
                        Assert.True(PixelEqual(before, reDecoded), rel + " costume " + c + ": frame did not round-trip");
                    }
                }
                tested++;
            }
            Assert.True(tested > 0, rel + ": no costume exercised");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga)]    // v3 old-bundle
        [InlineData(GameLibrary.Indy3Ega)]   // v3 old-bundle
        [InlineData(GameLibrary.Indy3Vga)]   // v3 GF_OLD256
        public void V3FontBatchExportThenNoOpImportIsByteIdentical(string rel)
        {
            Skip.IfNot(GameLibrary.Available, "GameData library not present");
            ScummGameData game = GameLibrary.Load(rel);
            Skip.If(game == null, "game folder missing");
            Skip.If(game.V3Charsets == null || game.V3Charsets.Count == 0, "no v3 charsets");

            var before = new List<byte[]>();
            foreach (CharsetV3 ch in game.V3Charsets) before.Add((byte[])ch.RawContent.Clone());

            string dir = Path.Combine(Path.GetTempPath(), "v3fontbatch_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string exportReport = CharsetV3PngCodec.ExportAll(game.V3Charsets, dir);
                Assert.True(Directory.GetFiles(dir, "*.png").Length > 0, rel + ": export wrote no PNGs (" + exportReport + ")");

                CharsetV3PngCodec.ImportAll(game.V3Charsets, dir); // no-op re-import
                for (int i = 0; i < game.V3Charsets.Count; i++)
                    Assert.True(BytesEqual(before[i], game.V3Charsets[i].RawContent),
                        rel + ": no-op font import changed charset " + i);
            }
            finally { Directory.Delete(dir, true); }
        }

        private static Dictionary<int, ScummV3OldBundleDataFile> MapRooms(ScummGameData game)
        {
            var byRoom = new Dictionary<int, ScummV3OldBundleDataFile>();
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int n;
                if (int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out n)) byRoom[n] = df;
            }
            return byRoom;
        }

        private static bool PixelEqual(Bitmap a, Bitmap b)
        {
            if (a == null || b == null || a.Width != b.Width || a.Height != b.Height) return false;
            byte[,] ma = IndexedImageHelper.GetIndexMatrix(a);
            byte[,] mb = IndexedImageHelper.GetIndexMatrix(b);
            for (int x = 0; x < a.Width; x++)
                for (int y = 0; y < a.Height; y++)
                    if ((ma[x, y] & 0x0F) != (mb[x, y] & 0x0F)) return false;
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
