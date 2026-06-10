using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Helpers to build and read true 8bpp indexed bitmaps where each pixel value
    /// IS the raw palette/codec index (not a color).
    ///
    /// This is the lossless path for export/import: when the index is stored directly
    /// (instead of being reconstructed from the pixel color), duplicate colors in the
    /// palette no longer collapse into the wrong index. As a consequence the same image
    /// renders correctly under any alternate palette (APAL), because the stored indexes
    /// are palette-agnostic.
    /// </summary>
    public static class IndexedImageHelper
    {
        /// <summary>
        /// True when the bitmap carries per-pixel palette indexes that can be read back
        /// losslessly (1/4/8 bpp indexed formats).
        /// </summary>
        public static bool IsIndexed(Bitmap bitmap)
        {
            return bitmap.PixelFormat == PixelFormat.Format8bppIndexed
                   || bitmap.PixelFormat == PixelFormat.Format4bppIndexed
                   || bitmap.PixelFormat == PixelFormat.Format1bppIndexed;
        }

        /// <summary>
        /// Reads the raw palette indexes of an indexed bitmap into a [width, height] matrix.
        /// Supports 8bpp, 4bpp and 1bpp indexed bitmaps so images edited/saved by external
        /// tools (which may pick a smaller bit depth) are still read correctly.
        /// </summary>
        public static byte[,] GetIndexMatrix(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            var result = new byte[width, height];

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                int stride = data.Stride;
                byte[] buffer = new byte[stride * height];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

                int bpp;
                switch (bitmap.PixelFormat)
                {
                    case PixelFormat.Format1bppIndexed: bpp = 1; break;
                    case PixelFormat.Format4bppIndexed: bpp = 4; break;
                    default: bpp = 8; break;
                }

                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        byte index;
                        if (bpp == 8)
                        {
                            index = buffer[row + x];
                        }
                        else if (bpp == 4)
                        {
                            byte packed = buffer[row + (x >> 1)];
                            index = (x & 1) == 0 ? (byte)(packed >> 4) : (byte)(packed & 0x0F);
                        }
                        else // 1bpp
                        {
                            byte packed = buffer[row + (x >> 3)];
                            index = (byte)((packed >> (7 - (x & 7))) & 1);
                        }
                        result[x, y] = index;
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return result;
        }

        /// <summary>
        /// Builds an 8bpp indexed bitmap from an index matrix and a palette.
        /// The pixel bytes are the indexes themselves; <paramref name="palette"/> is only
        /// used for display (and written as the PNG PLTE chunk on Save).
        /// </summary>
        /// <param name="indices">[width, height] matrix of palette indexes.</param>
        /// <param name="palette">Display colors. Up to 256 entries are used.</param>
        /// <param name="transparentIndex">
        /// Palette entry to mark fully transparent (writes a PNG tRNS chunk), or -1 for none.
        /// </param>
        public static Bitmap FromIndexMatrix(byte[,] indices, Color[] palette, int transparentIndex)
        {
            int width = indices.GetLength(0);
            int height = indices.GetLength(1);

            var bitmap = new Bitmap(width, height, PixelFormat.Format8bppIndexed);

            ColorPalette bitmapPalette = bitmap.Palette; // a working copy with 256 entries
            for (int i = 0; i < bitmapPalette.Entries.Length; i++)
            {
                Color color = i < palette.Length ? palette[i] : Color.Black;
                if (i == transparentIndex)
                {
                    bitmapPalette.Entries[i] = Color.FromArgb(0, color.R, color.G, color.B);
                }
                else
                {
                    bitmapPalette.Entries[i] = Color.FromArgb(255, color.R, color.G, color.B);
                }
            }
            bitmap.Palette = bitmapPalette; // must reassign for changes to take effect

            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            try
            {
                int stride = data.Stride;
                byte[] buffer = new byte[stride * height];
                for (int y = 0; y < height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        buffer[row + x] = indices[x, y];
                    }
                }
                Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return bitmap;
        }
    }
}
