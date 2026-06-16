using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// The image codec selection extracted in Stage 2a (ImageResourceCodec): decoding a room
    /// background, re-encoding it with auto-detected compression, and decoding again must reproduce
    /// the exact same palette indexes. The exhaustive per-image pixel sweep lives in the harness.
    /// </summary>
    public class ImageCodecTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland2Floppy)]    // v5
        [InlineData(GameLibrary.DayOfTheTentacleFloppy)] // v6
        public void BackgroundDecodeEncodeDecodeIsPixelIdentical(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.NotNull(game);

            List<DiskBlock> lflfs = game.DataFile.GetLFLFs();
            Assert.NotEmpty(lflfs);
            RoomBlock room = lflfs[0].GetROOM();
            Assert.NotNull(room);

            Bitmap first = ImageResourceCodec.Decode(room, null, ImageType.Background, 0, 0, 0, 0, false);
            Assert.NotNull(first);

            ImageResourceCodec.Encode(room, null, ImageType.Background, 0, 0, 0, first, ImageEncoder.EncodeTypeSettings.AutoDetect);

            Bitmap second = ImageResourceCodec.Decode(room, null, ImageType.Background, 0, 0, 0, 0, false);
            Assert.NotNull(second);

            Assert.Equal(first.Width, second.Width);
            Assert.Equal(first.Height, second.Height);

            byte[,] a = IndexedImageHelper.GetIndexMatrix(first);
            byte[,] b = IndexedImageHelper.GetIndexMatrix(second);

            int mismatches = 0;
            for (int x = 0; x < first.Width; x++)
            {
                for (int y = 0; y < first.Height; y++)
                {
                    if (a[x, y] != b[x, y]) mismatches++;
                }
            }

            Assert.Equal(0, mismatches);
        }
    }
}
