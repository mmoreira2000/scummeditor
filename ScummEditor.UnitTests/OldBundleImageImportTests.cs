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
    /// Real-data tests for the per-node v2 / v3-old image import (OldBundleImageImporter): re-importing an
    /// image's own decoded pixels round-trips losslessly through encode + ApplyEdit + re-decode, and the
    /// room stays loadable. Operates on the in-memory game copy (Load reads bytes into RawContent), so it
    /// never touches the on-disk games. Skips when the GameData library is absent.
    /// </summary>
    public class OldBundleImageImportTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga)]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void BackgroundImportRoundTripsLossless(string rel)
        {
            Run(rel, OldBundleImageKind.Background, 0, requireObject: false, requireZPlane: false);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga)]
        [InlineData(GameLibrary.ManiacV2)]
        public void ObjectImportRoundTripsLossless(string rel)
        {
            Run(rel, OldBundleImageKind.Object, 0, requireObject: true, requireZPlane: false);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga)]
        [InlineData(GameLibrary.ManiacV2)]
        public void BackgroundZPlaneImportRoundTripsLossless(string rel)
        {
            Run(rel, OldBundleImageKind.BackgroundZPlane, 0, requireObject: false, requireZPlane: true);
        }

        private static void Run(string rel, OldBundleImageKind kind, int unusedObj, bool requireObject, bool requireZPlane)
        {
            Skip.IfNot(GameLibrary.Available, "GameData library not present");
            ScummGameData game = GameLibrary.Load(rel);
            Skip.If(game == null, "game folder missing");
            var index = (ScummV3OldBundleIndexFile)game.IndexFile;
            bool isV2 = game.LoadedGameInfo.ScummVersion <= 2;

            int tested = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;

                int objectIndex = 0;
                Bitmap original = DecodeTarget(df, isV2, kind, ref objectIndex, requireObject, requireZPlane);
                if (original == null) continue;

                using (original)
                {
                    string error;
                    bool ok = OldBundleImageImporter.Import(df, index, roomNo, isV2, kind, objectIndex, original, out error);
                    Assert.True(ok, rel + " room " + roomNo + ": import failed: " + error);

                    using (Bitmap reDecoded = DecodeTarget(df, isV2, kind, ref objectIndex, requireObject, requireZPlane))
                    {
                        Assert.NotNull(reDecoded); // the room must still decode after the edit
                        Assert.True(PixelEqual(original, reDecoded),
                            rel + " room " + roomNo + ": re-import did not round-trip the pixels");
                    }
                }

                if (++tested >= 3) break; // a few rooms per game is enough to prove the path
            }

            Assert.True(tested > 0, rel + ": no room exercised this image kind");
        }

        /// <summary>Decodes the requested image; for Object it advances objectIndex to the first decodable object.</summary>
        private static Bitmap DecodeTarget(ScummV3OldBundleDataFile df, bool isV2, OldBundleImageKind kind,
            ref int objectIndex, bool requireObject, bool requireZPlane)
        {
            byte[] data = df.RawContent;
            if (isV2)
            {
                var room = new ScummV2Room(data);
                var dec = new ScummV2ImageDecoder();
                if (kind == OldBundleImageKind.Background) return dec.DecodeBackground(room);
                if (kind == OldBundleImageKind.BackgroundZPlane) return dec.DecodeBackgroundZPlane(room);
                if (kind == OldBundleImageKind.Object)
                {
                    for (int j = 0; j < room.NumObjects; j++)
                    {
                        Bitmap b = dec.DecodeObject(room, j);
                        if (b != null) { objectIndex = j; return b; }
                    }
                }
                return null;
            }
            else
            {
                var room = new ScummV3OldRoom(data);
                var dec = new ScummV3OldImageDecoder();
                if (kind == OldBundleImageKind.Background) return dec.DecodeBackground(room);
                if (kind == OldBundleImageKind.BackgroundZPlane) return dec.DecodeBackgroundZPlane(room);
                if (kind == OldBundleImageKind.Object)
                {
                    for (int j = 0; j < room.NumObjects; j++)
                    {
                        Bitmap b = dec.DecodeObject(room, j);
                        if (b != null) { objectIndex = j; return b; }
                    }
                }
                return null;
            }
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
    }
}
