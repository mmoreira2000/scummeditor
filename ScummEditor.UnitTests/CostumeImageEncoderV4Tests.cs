using System.Drawing;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// The v4 costume encoder's input guards: it requires an indexed (palette-based) bitmap, and the
    /// size-checked overload (added in Stage 2d so the rule lives in the engine, not the GUI) rejects
    /// a bitmap whose dimensions differ from the original frame.
    /// </summary>
    public class CostumeImageEncoderV4Tests
    {
        private static Bitmap MakeIndexed(int width, int height, int paletteSize)
        {
            var palette = new Color[paletteSize];
            for (int i = 0; i < paletteSize; i++)
            {
                int v = (i * 255) / (paletteSize - 1);
                palette[i] = Color.FromArgb(v, v, v);
            }

            var indices = new byte[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    indices[x, y] = (byte)((x + y) % paletteSize);
                }
            }

            return IndexedImageHelper.FromIndexMatrix(indices, palette, -1);
        }

        [Fact]
        public void EncodeRejectsNonIndexedBitmap()
        {
            using (var truecolor = new Bitmap(8, 8)) // default 32bpp ARGB - not indexed
            {
                var encoder = new CostumeImageEncoderV4();
                Assert.Throws<ImageEncodeException>(() => encoder.Encode(truecolor, 16));
            }
        }

        /// <summary>
        /// The v4 costume codec (reused for v2/v3/v4 at 16 colours and v5/v6 at 16/32) is pixel-lossless:
        /// encode -> decode reproduces the exact palette indices, for both the 4/4-bit (16) and 5/3-bit (32)
        /// bit-stream packings. Guards against RLE / bit-packing regressions the size-only tests miss.
        /// </summary>
        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        public void EncodeDecodeRoundTripIsLossless(int paletteSize)
        {
            var palette = new Color[paletteSize];
            for (int i = 0; i < paletteSize; i++)
            {
                int v = (i * 255) / (paletteSize - 1); // distinct greys, so the index is recoverable
                palette[i] = Color.FromArgb(v, v, v);
            }

            int w = 16, h = 24;
            var indices = new byte[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    indices[x, y] = (byte)((x + (y / 4)) % paletteSize); // vertical runs exercise the column-major RLE

            using (Bitmap src = IndexedImageHelper.FromIndexMatrix(indices, palette, -1))
            {
                byte[] rle = new CostumeImageEncoderV4().Encode(src, paletteSize);
                var data = new CostumeImageData { Width = (ushort)w, Height = (ushort)h, ImageData = rle };

                using (Bitmap back = new CostumeImageDecoderV4().Decode(data, paletteSize, palette, false))
                {
                    Assert.NotNull(back);
                    Assert.Equal(w, back.Width);
                    Assert.Equal(h, back.Height);
                    byte[,] a = IndexedImageHelper.GetIndexMatrix(src);
                    byte[,] b = IndexedImageHelper.GetIndexMatrix(back);
                    for (int x = 0; x < w; x++)
                        for (int y = 0; y < h; y++)
                            Assert.True(a[x, y] == b[x, y], "costume codec not lossless at (" + x + "," + y + ")");
                }
            }
        }

        [Fact]
        public void EncodeProducesBytesForAnIndexedBitmap()
        {
            using (Bitmap bitmap = MakeIndexed(8, 8, 16))
            {
                var encoder = new CostumeImageEncoderV4();
                byte[] rle = encoder.Encode(bitmap, 16);

                Assert.NotNull(rle);
                Assert.True(rle.Length > 0);
            }
        }

        [Fact]
        public void SizeCheckedOverloadMatchesPlainEncodeWhenSizeIsCorrect()
        {
            using (Bitmap bitmap = MakeIndexed(8, 8, 16))
            {
                var encoder = new CostumeImageEncoderV4();
                byte[] plain = encoder.Encode(bitmap, 16);
                byte[] checked_ = encoder.Encode(bitmap, 16, 8, 8);

                Assert.Equal(plain, checked_);
            }
        }

        [Fact]
        public void SizeCheckedOverloadRejectsAWrongSizedBitmap()
        {
            using (Bitmap bitmap = MakeIndexed(8, 8, 16))
            {
                var encoder = new CostumeImageEncoderV4();
                var ex = Assert.Throws<ImageEncodeException>(() => encoder.Encode(bitmap, 16, 9, 8));

                // The message names both the expected (original) and the actual size.
                Assert.Contains("9x8", ex.Message);
                Assert.Contains("8x8", ex.Message);
            }
        }
    }
}
