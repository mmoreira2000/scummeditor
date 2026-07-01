using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ScummEditor.Engine.Encoders
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
        /// Perturbs duplicate palette entries in place so every entry has a unique ARGB, changing a duplicate
        /// by the smallest possible amount (a +1 walk on B, then G, then R). Keeps alpha (so the transparent
        /// entry stays transparent and never collides with an opaque entry of the same RGB). With &lt;=256
        /// entries in a 24-bit space, uniqueness is always reachable with tiny tweaks.
        /// </summary>
        private static void MakePaletteEntriesUnique(Color[] entries)
        {
            var used = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < entries.Length; i++)
            {
                Color c = entries[i];
                if (used.Add(c.ToArgb())) continue;

                // Duplicate: nudge it to the nearest free colour of the SAME alpha (never change alpha - an
                // alpha &lt; 255 here would add a PNG tRNS chunk, which makes GDI+ reload the indexed PNG as
                // 32bpp and lose the indices). Search a small neighbourhood on B, then G, then R.
                int a = c.A, r = c.R, g = c.G, b = c.B;
                Color chosen = c;
                bool placed = false;
                for (int d = 1; d <= 255 && !placed; d++)
                {
                    placed = TryUse(used, a, r, g, b + d, ref chosen)
                          || TryUse(used, a, r, g, b - d, ref chosen)
                          || TryUse(used, a, r, g + d, b, ref chosen)
                          || TryUse(used, a, r, g - d, b, ref chosen)
                          || TryUse(used, a, r + d, g, b, ref chosen)
                          || TryUse(used, a, r - d, g, b, ref chosen);
                }
                entries[i] = chosen; // if nothing free was found (a full 16.7M-colour palette can't happen at <=256), keep as-is
            }
        }

        private static bool TryUse(System.Collections.Generic.HashSet<int> used, int a, int r, int g, int b, ref Color chosen)
        {
            if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255) return false;
            int argb = Color.FromArgb(a, r, g, b).ToArgb();
            if (!used.Add(argb)) return false;
            chosen = Color.FromArgb(argb);
            return true;
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
            // Force every palette entry to a DISTINCT color. The pixel bytes already carry the true index, so
            // this display palette is cosmetic (the game never reads it - it renders through its own AKPL/RGBS),
            // but an external editor that re-derives a pixel's index from its COLOR on save (e.g. IDraw3) would,
            // with duplicate palette colors, collapse two indices that share a colour into the first one -
            // silently changing the image. Making each entry unique (an imperceptible +-1 tweak on a duplicate)
            // gives every index its own colour, so such an editor round-trips the indices faithfully. Duplicates
            // are usually unused padding slots, so this changes nothing a pixel actually uses.
            MakePaletteEntriesUnique(bitmapPalette.Entries);
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
