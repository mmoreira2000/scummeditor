using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Exports a SCUMM v7 .NUT SMUSH font to an editable indexed PNG and imports it back. Two granularities
    /// are offered: a single glyph (for the per-glyph GUI Export/Import) and a whole-font atlas (a grid of
    /// all glyphs, used by the batch "game fonts" dialog). The PNG is 8bpp indexed and carries the raw
    /// palette indices, so the round-trip is palette-independent (the displayed colours are only a preview);
    /// re-import re-encodes each glyph through <see cref="NutImageEncoder"/>, keeping every glyph's size.
    /// </summary>
    public static class NutFontPngCodec
    {
        private const int AtlasColumns = 16;

        // ---- single glyph (per-node GUI) ----

        public static void ExportGlyphPng(NutFont font, int index, string pngPath, Color[] palette)
        {
            byte[,] indices = NutImageDecoder.DecodeGlyphIndices(font, index);
            if (indices == null)
            {
                throw new ImageEncodeException("NUT glyph #" + index + " has no decodable pixels");
            }
            SaveIndexed(indices, palette ?? Grayscale(), pngPath);
        }

        public static void ImportGlyphPng(NutFont font, int index, string pngPath)
        {
            using (var bitmap = (Bitmap)Image.FromFile(pngPath))
            {
                RequireIndexed(bitmap);
                NutImageEncoder.ReplaceGlyph(font, index, IndexedImageHelper.GetIndexMatrix(bitmap));
            }
        }

        // ---- whole-font atlas (batch) ----

        /// <summary>Exports every decodable glyph as a grid (16 columns) of fixed cells sized to the font's
        /// largest glyph; each glyph sits at the top-left of its cell, the rest left transparent. When
        /// <paramref name="guidePath"/> is given, a companion guide image (cell grid + glyph-index labels +
        /// a faint reference of each glyph) is written too, like the CHAR/v3 font exporters.</summary>
        public static void ExportPng(NutFont font, string pngPath, string guidePath, Color[] palette)
        {
            int cellW, cellH, cols, rows, transparency;
            List<int> drawable = AtlasLayout(font, out cellW, out cellH, out cols, out rows, out transparency);
            if (drawable.Count == 0)
            {
                throw new ImageEncodeException("NUT font has no decodable glyphs to export");
            }

            var atlas = new byte[cols * cellW, rows * cellH];
            if (transparency != 0)
            {
                for (int x = 0; x < atlas.GetLength(0); x++)
                    for (int y = 0; y < atlas.GetLength(1); y++)
                        atlas[x, y] = (byte)transparency;
            }

            for (int slot = 0; slot < drawable.Count; slot++)
            {
                int gi = drawable[slot];
                byte[,] g = NutImageDecoder.DecodeGlyphIndices(font, gi);
                if (g == null) continue;
                int ox = (slot % cols) * cellW;
                int oy = (slot / cols) * cellH;
                for (int x = 0; x < g.GetLength(0); x++)
                    for (int y = 0; y < g.GetLength(1); y++)
                        atlas[ox + x, oy + y] = g[x, y];
            }

            SaveIndexed(atlas, palette ?? Grayscale(), pngPath);

            if (guidePath != null)
            {
                using (Bitmap guide = BuildGuide(font, drawable, cellW, cellH, cols, rows))
                {
                    guide.Save(guidePath, ImageFormat.Png);
                }
            }
        }

        /// <summary>
        /// A companion reference image (RGB, same grid as the atlas): each cell outlined, labelled with the
        /// glyph index, and showing a faint copy of the original glyph - so a translator knows which cell is
        /// which glyph and where the ink sits. Mirrors the CHAR/v3 font exporters' .guide.png.
        /// </summary>
        private static Bitmap BuildGuide(NutFont font, List<int> drawable, int cellW, int cellH, int cols, int rows)
        {
            var bitmap = new Bitmap(cols * cellW, rows * cellH, PixelFormat.Format24bppRgb);
            using (Graphics gfx = Graphics.FromImage(bitmap))
            using (var gridPen = new Pen(Color.FromArgb(60, 80, 160)))
            using (var idFont = new Font("Consolas", 6f))
            using (var idBrush = new SolidBrush(Color.Red))
            {
                gfx.Clear(Color.White);

                // A flat-gray palette renders each glyph as a faint reference under the grid/labels.
                var refPalette = new Color[256];
                for (int i = 0; i < 256; i++) refPalette[i] = Color.FromArgb(150, 150, 150);

                for (int slot = 0; slot < drawable.Count; slot++)
                {
                    int gi = drawable[slot];
                    int cx = (slot % cols) * cellW;
                    int cy = (slot / cols) * cellH;

                    using (Bitmap glyph = NutImageDecoder.DecodeGlyph(font, gi, refPalette))
                    {
                        if (glyph != null) gfx.DrawImageUnscaled(glyph, cx, cy);
                    }

                    gfx.DrawRectangle(gridPen, cx, cy, cellW - 1, cellH - 1);
                    gfx.DrawString(gi.ToString(), idFont, idBrush, cx + 1, cy + 1);
                }
            }
            return bitmap;
        }

        /// <summary>Imports a whole-font atlas: each decodable glyph is read from the top-left of its cell
        /// (at the glyph's stored width x height) and re-encoded. The atlas must match the export layout.</summary>
        public static void ImportPng(NutFont font, string pngPath)
        {
            int cellW, cellH, cols, rows, transparency;
            List<int> drawable = AtlasLayout(font, out cellW, out cellH, out cols, out rows, out transparency);
            if (drawable.Count == 0)
            {
                throw new ImageEncodeException("NUT font has no decodable glyphs to import");
            }

            using (var bitmap = (Bitmap)Image.FromFile(pngPath))
            {
                RequireIndexed(bitmap);
                if (bitmap.Width < cols * cellW || bitmap.Height < rows * cellH)
                {
                    throw new ImageEncodeException(string.Format(
                        "atlas is {0}x{1}; this font needs at least {2}x{3}",
                        bitmap.Width, bitmap.Height, cols * cellW, rows * cellH));
                }

                byte[,] atlas = IndexedImageHelper.GetIndexMatrix(bitmap);
                for (int slot = 0; slot < drawable.Count; slot++)
                {
                    int gi = drawable[slot];
                    NutGlyph glyph = font.Glyphs[gi];
                    int ox = (slot % cols) * cellW;
                    int oy = (slot / cols) * cellH;

                    var cell = new byte[glyph.Width, glyph.Height];
                    for (int x = 0; x < glyph.Width; x++)
                        for (int y = 0; y < glyph.Height; y++)
                            cell[x, y] = atlas[ox + x, oy + y];

                    NutImageEncoder.ReplaceGlyph(font, gi, cell);
                }
            }
        }

        // ---- batch over many fonts ----

        public static string ExportAll(List<NutFontResource> fonts, string folder)
        {
            int count = 0;
            var errors = new List<string>();
            for (int i = 0; i < fonts.Count; i++)
            {
                string name = BatchFileName(fonts[i], i);
                string guide = Path.ChangeExtension(name, null) + ".guide.png";
                try
                {
                    ExportPng(fonts[i].Font, Path.Combine(folder, name), Path.Combine(folder, guide), null);
                    count++;
                }
                catch (ImageEncodeException ex)
                {
                    errors.Add(name + ": " + ex.Message);
                }
            }
            return Report("Exported", count, fonts.Count, errors);
        }

        public static string ImportAll(List<NutFontResource> fonts, string folder)
        {
            int count = 0;
            var errors = new List<string>();
            for (int i = 0; i < fonts.Count; i++)
            {
                string name = BatchFileName(fonts[i], i);
                string path = Path.Combine(folder, name);
                if (!File.Exists(path))
                {
                    continue; // not every font has to be edited
                }
                try
                {
                    ImportPng(fonts[i].Font, path);
                    count++;
                }
                catch (ImageEncodeException ex)
                {
                    errors.Add(name + ": " + ex.Message);
                }
            }
            return Report("Imported", count, fonts.Count, errors);
        }

        /// <summary>The batch PNG name for a font: an index prefix keeps it unique (a game can ship two NUT
        /// files with the same base name in different subfolders, e.g. Full Throttle's TITLFNT.NUT), while
        /// the base name keeps it readable. Import reconstructs the same name from the font list order.</summary>
        private static string BatchFileName(NutFontResource resource, int index)
        {
            return string.Format("nutfont_{0:D2}_{1}.png", index, Path.GetFileNameWithoutExtension(resource.FilePath));
        }

        // ---- helpers ----

        /// <summary>Computes the atlas grid: the list of glyph indexes that have pixels, the cell size
        /// (the largest glyph), the column/row counts and the transparent index.</summary>
        private static List<int> AtlasLayout(NutFont font, out int cellW, out int cellH, out int cols, out int rows, out int transparency)
        {
            var drawable = new List<int>();
            cellW = 1; cellH = 1; transparency = 0;
            for (int i = 0; i < font.Glyphs.Count; i++)
            {
                NutGlyph g = font.Glyphs[i];
                if (!g.HasPixels || !NutImageDecoder.IsSupportedCodec(g.Codec)) continue;
                drawable.Add(i);
                if (g.Width > cellW) cellW = g.Width;
                if (g.Height > cellH) cellH = g.Height;
                transparency = NutImageDecoder.TransparencyIndex(g.Codec); // one codec per NUT file
            }
            cols = drawable.Count < AtlasColumns ? (drawable.Count == 0 ? 1 : drawable.Count) : AtlasColumns;
            rows = drawable.Count == 0 ? 1 : (drawable.Count + cols - 1) / cols;
            return drawable;
        }

        private static void SaveIndexed(byte[,] indices, Color[] palette, string pngPath)
        {
            // Save as a plain opaque 8bpp indexed PNG (no tRNS): a transparent palette entry makes GDI+
            // reload the file as 32bpp, which would lose the raw indices. Transparent pixels keep their
            // index value (0, or 2 for codec 44) and read back losslessly; the encoder treats those indices
            // as transparent again. (The on-screen preview applies real transparency separately.)
            using (Bitmap bitmap = IndexedImageHelper.FromIndexMatrix(indices, palette, -1))
            {
                bitmap.Save(pngPath, ImageFormat.Png);
            }
        }

        private static void RequireIndexed(Bitmap bitmap)
        {
            if (!IndexedImageHelper.IsIndexed(bitmap))
            {
                throw new ImageEncodeException("NUT font PNG must be an indexed image (re-export it and edit without converting to RGB)");
            }
        }

        private static Color[] Grayscale()
        {
            var palette = new Color[256];
            for (int i = 0; i < 256; i++) palette[i] = Color.FromArgb(i, i, i);
            return palette;
        }

        private static string Report(string verb, int count, int total, List<string> errors)
        {
            string report = verb + " " + count + " of " + total + " NUT font(s).";
            if (errors.Count > 0)
            {
                report += "\r\n\r\nSkipped:\r\n" + string.Join("\r\n", errors);
            }
            return report;
        }
    }
}
