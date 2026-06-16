using System.Drawing;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
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
