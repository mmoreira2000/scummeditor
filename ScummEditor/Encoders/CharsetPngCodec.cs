using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    /*
    Exports a CHAR (charset/font) block to an editable PNG atlas and imports it back,
    so designers can draw missing glyphs (accents) in Photoshop.

    Atlas conventions:
      - Always 16 x 16 = 256 slots, even when the font declares fewer chars - drawing in a
        slot beyond numChars extends the font on import (e.g. adding 'ã' at slot 0xE3 to a
        185-char font). Slot id = row * 16 + column, which IS the text byte / glyph index.
      - Every cell has the glyph origin at (8, 8); the glyph's x/y offsets are baked into
        its position. On import the offsets are recovered from the pixel bounding box
        relative to that origin, so a glyph drawn anywhere in the cell keeps its alignment.
      - The PNG is 8bpp indexed and each pixel byte is the raw glyph pixel value
        (0 = transparent, 1..2^bpp-1 = ink), exactly like the room-image export pipeline.
        The file must stay in indexed mode when edited.
      - A companion guide PNG (same size) shows the grid, the hex slot ids, the origin
        marks and the current glyphs, to be used as a reference layer in Photoshop.

    Import safety: a cell whose pixels are identical to what export produces for the
    current font keeps the original glyph bytes verbatim (so blank-but-present glyphs like
    the space survive). When nothing changed at all, RawContent is left untouched.
    */
    public static class CharsetPngCodec
    {
        private const int Columns = 16;
        private const int Rows = 16;     // always 256 slots so fonts can be extended
        // The glyph origin inside each cell sits at (marginX, marginY). The margins are at
        // least 8 pixels and grow when the font has glyphs with offsets below -8 (some
        // European MI2 fonts do). Import recomputes the same margins from the same font,
        // so the convention stays self-describing.
        private const int MinimumMargin = 8;
        private const int GridIndex = 255; // palette slot used for the cell divider lines (ignored on import)
        private const int OffsetBase = 0x15;
        private const int TableStart = 0x19;

        // ---------------------------------------------------------------------
        // Export
        // ---------------------------------------------------------------------

        public static void ExportPng(Charset charset, string pngPath, string guidePath)
        {
            int marginX, marginY, cellW, cellH;
            ComputeLayout(charset, out marginX, out marginY, out cellW, out cellH);
            int width = Columns * cellW, height = Rows * cellH;

            var matrix = new byte[width, height];

            // divider lines on the top/left edge of every cell (GridIndex pixels are ignored on import)
            for (int x = 0; x < width; x += cellW)
                for (int y = 0; y < height; y++) matrix[x, y] = GridIndex;
            for (int y = 0; y < height; y += cellH)
                for (int x = 0; x < width; x++) matrix[x, y] = GridIndex;

            for (int slot = 0; slot < charset.Glyphs.Count && slot < 256; slot++)
            {
                DrawGlyph(charset, charset.Glyphs[slot], matrix,
                    (slot % Columns) * cellW, (slot / Columns) * cellH, marginX, marginY);
            }

            // No tRNS chunk: GDI+ reloads a paletted PNG with transparency as 32bpp ARGB,
            // which would break the lossless indexed round-trip. Background stays opaque black.
            using (Bitmap bitmap = IndexedImageHelper.FromIndexMatrix(matrix, BuildEditPalette(charset.BitsPerPixel), -1))
            {
                bitmap.Save(pngPath, ImageFormat.Png);
            }

            using (Bitmap guide = BuildGuide(charset, cellW, cellH, marginX, marginY))
            {
                guide.Save(guidePath, ImageFormat.Png);
            }
        }

        /// <summary>
        /// Palette for the editable atlas: 0 = background, 1..maxVal = ink shades,
        /// 255 = cell divider lines (ignored on import), rest = magenta (invalid).
        /// </summary>
        public static Color[] BuildEditPalette(int bitsPerPixel)
        {
            int maxVal = (1 << bitsPerPixel) - 1;
            var palette = new Color[256];
            palette[0] = Color.Black;
            for (int v = 1; v < 256; v++)
            {
                if (v <= maxVal)
                {
                    int level = bitsPerPixel == 1 ? 255 : 255 - (255 * v / maxVal); // same ramp as the glyph viewer
                    if (level < 40) level = 40;                                     // keep every ink value visible on black
                    palette[v] = Color.FromArgb(level, level, level);
                }
                else
                {
                    palette[v] = Color.Magenta; // painting with these would be invalid
                }
            }
            palette[GridIndex] = Color.FromArgb(60, 80, 160); // divider lines
            return palette;
        }

        /// <summary>
        /// Per-font atlas layout: the margins grow to fit the most negative glyph offsets,
        /// and the cell fits the largest glyph extent plus a fixed right/bottom editing room.
        /// </summary>
        private static void ComputeLayout(Charset charset, out int marginX, out int marginY, out int cellW, out int cellH)
        {
            marginX = MinimumMargin;
            marginY = MinimumMargin;
            int maxX = 8, maxY = Math.Max(8, charset.FontHeight);

            foreach (Glyph g in charset.Glyphs)
            {
                if (!g.Present) continue;
                if (-g.XOffset > marginX) marginX = -g.XOffset;
                if (-g.YOffset > marginY) marginY = -g.YOffset;
                if (g.XOffset + g.Width > maxX) maxX = g.XOffset + g.Width;
                if (g.YOffset + g.Height > maxY) maxY = g.YOffset + g.Height;
            }
            cellW = marginX + maxX + MinimumMargin;
            cellH = marginY + maxY + MinimumMargin;
        }

        private static void DrawGlyph(Charset charset, Glyph glyph, byte[,] matrix, int cellX, int cellY,
                                      int marginX, int marginY)
        {
            if (glyph == null || !glyph.Present || glyph.Width <= 0 || glyph.Height <= 0) return;

            int width = matrix.GetLength(0), height = matrix.GetLength(1);
            byte[,] values = ReadGlyphValues(charset, glyph);
            for (int y = 0; y < glyph.Height; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    if (values[x, y] == 0) continue;
                    int px = cellX + marginX + glyph.XOffset + x;
                    int py = cellY + marginY + glyph.YOffset + y;
                    if (px >= 0 && px < width && py >= 0 && py < height) matrix[px, py] = values[x, y];
                }
            }
        }

        private static Bitmap BuildGuide(Charset charset, int cellW, int cellH, int marginX, int marginY)
        {
            var bitmap = new Bitmap(Columns * cellW, Rows * cellH, PixelFormat.Format24bppRgb);
            using (Graphics gfx = Graphics.FromImage(bitmap))
            using (var extensionBrush = new SolidBrush(Color.FromArgb(255, 250, 220)))
            using (var gridPen = new Pen(Color.FromArgb(190, 190, 190)))
            using (var originPen = new Pen(Color.FromArgb(120, 170, 255)))
            using (var glyphBrush = new SolidBrush(Color.FromArgb(150, 150, 150)))
            using (var idFont = new Font("Consolas", 6f))
            using (var idBrush = new SolidBrush(Color.Red))
            {
                gfx.Clear(Color.White);

                for (int slot = 0; slot < 256; slot++)
                {
                    int cx = (slot % Columns) * cellW;
                    int cy = (slot / Columns) * cellH;

                    // tint the cells beyond the current numChars (extension area)
                    if (slot >= charset.NumChars)
                        gfx.FillRectangle(extensionBrush, cx, cy, cellW, cellH);

                    gfx.DrawRectangle(gridPen, cx, cy, cellW - 1, cellH - 1);

                    // origin marks: the glyph origin is at (marginX, marginY) in each cell
                    gfx.DrawLine(originPen, cx + marginX, cy + marginY - 3, cx + marginX, cy + marginY + 3);
                    gfx.DrawLine(originPen, cx + marginX - 3, cy + marginY, cx + marginX + 3, cy + marginY);

                    // current glyph, as editing context
                    if (slot < charset.Glyphs.Count)
                    {
                        Glyph glyph = charset.Glyphs[slot];
                        if (glyph.Present && glyph.Width > 0 && glyph.Height > 0)
                        {
                            byte[,] values = ReadGlyphValues(charset, glyph);
                            for (int y = 0; y < glyph.Height; y++)
                                for (int x = 0; x < glyph.Width; x++)
                                    if (values[x, y] != 0)
                                        gfx.FillRectangle(glyphBrush,
                                            cx + marginX + glyph.XOffset + x, cy + marginY + glyph.YOffset + y, 1, 1);
                        }
                    }

                    // slot id (hex) - this is the byte value used in the game texts
                    gfx.DrawString(slot.ToString("X2"), idFont, idBrush, cx, cy);
                }
            }
            return bitmap;
        }

        // ---------------------------------------------------------------------
        // Batch (all charsets of the game at once)
        // ---------------------------------------------------------------------

        /// <summary>Charsets of the game in tree (file) order - the index defines the file names.</summary>
        public static List<Charset> CollectCharsets(ScummV6DataFile dataFile)
        {
            var result = new List<Charset>();
            Collect(dataFile, result);
            return result;
        }

        private static void Collect(BlockBase block, List<Charset> result)
        {
            var charset = block as Charset;
            if (charset != null) result.Add(charset);
            foreach (BlockBase child in block.Childrens) Collect(child, result);
        }

        /// <summary>Exports every charset as charset_N.png + charset_N.guide.png into a folder.</summary>
        public static string ExportAll(ScummV6DataFile dataFile, string folder)
        {
            List<Charset> charsets = CollectCharsets(dataFile);
            for (int i = 0; i < charsets.Count; i++)
            {
                ExportPng(charsets[i],
                    Path.Combine(folder, "charset_" + i + ".png"),
                    Path.Combine(folder, "charset_" + i + ".guide.png"));
            }
            return charsets.Count + " fonts exported to:\n" + folder
                + "\n\nFiles: charset_0.png ... charset_" + (charsets.Count - 1)
                + ".png (+ the .guide.png reference images).";
        }

        /// <summary>Imports every charset_N.png found in the folder back into the game's charsets.</summary>
        public static string ImportAll(ScummV6DataFile dataFile, string folder)
        {
            List<Charset> charsets = CollectCharsets(dataFile);
            var report = new StringBuilder();
            int imported = 0, missing = 0, failed = 0;

            for (int i = 0; i < charsets.Count; i++)
            {
                string file = "charset_" + i + ".png";
                string path = Path.Combine(folder, file);
                if (!File.Exists(path))
                {
                    missing++;
                    report.AppendLine(file + ": not found (skipped)");
                    continue;
                }

                try
                {
                    string result = ImportPng(charsets[i], path);
                    imported++;
                    report.AppendLine(file + ": " + result.Replace(Environment.NewLine, " "));
                }
                catch (Exception ex)
                {
                    failed++;
                    report.AppendLine(file + ": ERROR - " + ex.Message);
                }
            }

            report.AppendLine();
            report.Append(string.Format("{0} font(s) processed, {1} without a file, {2} with errors.",
                imported, missing, failed));
            return report.ToString();
        }

        // ---------------------------------------------------------------------
        // Import
        // ---------------------------------------------------------------------

        private class SlotPlan
        {
            public bool Present;
            public Glyph KeepOriginal; // non-null: copy the original glyph bytes verbatim
            public byte[,] Values;     // new glyph pixels (bbox-cropped)
            public int W, H, XOff, YOff;
        }

        public static string ImportPng(Charset charset, string pngPath)
        {
            byte[,] pixels;
            int width, height;
            using (var loaded = (Bitmap)Image.FromFile(pngPath))
            {
                if (!IndexedImageHelper.IsIndexed(loaded))
                    throw new InvalidDataException(
                        "The PNG must be in indexed color mode (8 bits). Use the file exported by the editor and keep its color mode.");
                pixels = IndexedImageHelper.GetIndexMatrix(loaded);
                width = loaded.Width;
                height = loaded.Height;
            }

            if (width % Columns != 0 || height % Rows != 0)
                throw new InvalidDataException(string.Format(
                    "Invalid dimensions {0}x{1}: they must be multiples of {2}x{3} (the 256-slot grid).",
                    width, height, Columns, Rows));
            int cellW = width / Columns, cellH = height / Rows;
            int maxVal = (1 << charset.BitsPerPixel) - 1;

            // the divider lines are decoration only - drop them before any analysis
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (pixels[x, y] == GridIndex) pixels[x, y] = 0;

            // Same per-font margins the export used (recomputed from the same font).
            int marginX, marginY, layoutCellW, layoutCellH;
            ComputeLayout(charset, out marginX, out marginY, out layoutCellW, out layoutCellH);

            // Stored glyph boxes are usually larger than the ink (empty right column = cursor
            // advance, empty top rows = line position). New glyphs get the font's typical
            // (modal) padding so a glyph drawn like its neighbours also measures like them.
            int slackRight, slackBottom;
            ComputeModalSlacks(charset, out slackRight, out slackBottom);

            var plans = new SlotPlan[256];
            var badValues = new List<string>();
            int changed = 0, added = 0, removed = 0;

            for (int slot = 0; slot < 256; slot++)
            {
                int cellX = (slot % Columns) * cellW;
                int cellY = (slot / Columns) * cellH;

                // pixel bounding box of the cell
                int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
                for (int y = 0; y < cellH; y++)
                {
                    for (int x = 0; x < cellW; x++)
                    {
                        byte v = pixels[cellX + x, cellY + y];
                        if (v == 0) continue;
                        if (v > maxVal && badValues.Count < 8)
                            badValues.Add(string.Format("slot 0x{0:X2}: value {1} (maximum {2})", slot, v, maxVal));
                        if (x < left) left = x;
                        if (y < top) top = y;
                        if (x > right) right = x;
                        if (y > bottom) bottom = y;
                    }
                }
                bool anyPixel = right >= 0;

                Glyph original = slot < charset.Glyphs.Count ? charset.Glyphs[slot] : null;
                bool origPresent = original != null && original.Present;

                if (CellMatchesOriginal(charset, original, pixels, cellX, cellY, cellW, cellH, marginX, marginY))
                {
                    if (origPresent)
                        plans[slot] = new SlotPlan { Present = true, KeepOriginal = original };
                    continue; // unchanged (absent stays absent, blank-but-present keeps its bytes)
                }

                if (!anyPixel)
                {
                    if (origPresent) removed++;
                    continue; // cleared cell -> glyph absent
                }

                // glyph box in cell coordinates
                int boxL, boxT, boxR, boxB;
                if (origPresent && original.Width > 0 && original.Height > 0)
                {
                    // edited glyph: keep the original box (metrics/advance), grow only if the ink leaks out
                    boxL = marginX + original.XOffset;
                    boxT = marginY + original.YOffset;
                    boxR = boxL + original.Width - 1;
                    boxB = boxT + original.Height - 1;
                    if (left < boxL) boxL = left;
                    if (top < boxT) boxT = top;
                    if (right > boxR) boxR = right;
                    if (bottom > boxB) boxB = bottom;
                }
                else
                {
                    // new glyph: box anchored at the origin, plus the font's modal padding
                    boxL = Math.Min(left, marginX);
                    boxT = Math.Min(top, marginY);
                    boxR = right + slackRight;
                    boxB = bottom + slackBottom;
                }

                int w = boxR - boxL + 1, h = boxB - boxT + 1;
                int xoff = boxL - marginX, yoff = boxT - marginY;
                if (w > 255 || h > 255 || xoff < -128 || xoff > 127 || yoff < -128 || yoff > 127)
                    throw new InvalidDataException(string.Format(
                        "slot 0x{0:X2}: glyph {1}x{2} with offset ({3},{4}) outside the format limits.",
                        slot, w, h, xoff, yoff));

                var values = new byte[w, h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int px = boxL + x, py = boxT + y;
                        values[x, y] = px >= 0 && px < cellW && py >= 0 && py < cellH
                            ? pixels[cellX + px, cellY + py]
                            : (byte)0;
                    }
                }

                plans[slot] = new SlotPlan { Present = true, Values = values, W = w, H = h, XOff = xoff, YOff = yoff };
                if (origPresent) changed++; else added++;
            }

            if (badValues.Count > 0)
                throw new InvalidDataException("Pixels with invalid values for " + charset.BitsPerPixel
                    + " bpp:\n  " + string.Join("\n  ", badValues.ToArray()));

            if (changed + added + removed == 0)
                return "No changes found - the font was not modified.";

            RebuildRawContent(charset, plans);

            var report = new StringBuilder();
            report.AppendLine(string.Format("Glyphs changed: {0}, added: {1}, removed: {2}.", changed, added, removed));
            report.Append(string.Format("numChars: {0} (use 'Save Changes' to write it to the game files).", charset.NumChars));
            return report.ToString();
        }

        /// <summary>
        /// Most common (modal) padding between the ink and the stored box edge across the font's
        /// glyphs: right padding doubles as cursor advance, bottom padding as line spacing.
        /// </summary>
        private static void ComputeModalSlacks(Charset charset, out int slackRight, out int slackBottom)
        {
            var histRight = new int[16];
            var histBottom = new int[16];
            foreach (Glyph g in charset.Glyphs)
            {
                if (!g.Present || g.Width <= 0 || g.Height <= 0) continue;
                byte[,] values = ReadGlyphValues(charset, g);
                int right = -1, bottom = -1;
                for (int y = 0; y < g.Height; y++)
                    for (int x = 0; x < g.Width; x++)
                        if (values[x, y] != 0) { if (x > right) right = x; if (y > bottom) bottom = y; }
                if (right < 0) continue; // blank glyph

                int sr = g.Width - 1 - right;
                int sb = g.Height - 1 - bottom;
                if (sr < histRight.Length) histRight[sr]++;
                if (sb < histBottom.Length) histBottom[sb]++;
            }
            slackRight = ModeOf(histRight);
            slackBottom = ModeOf(histBottom);
        }

        private static int ModeOf(int[] histogram)
        {
            int best = 0;
            for (int i = 1; i < histogram.Length; i++)
                if (histogram[i] > histogram[best]) best = i;
            return best;
        }

        /// <summary>True when the cell pixels are exactly what ExportPng would produce for this glyph.</summary>
        private static bool CellMatchesOriginal(Charset charset, Glyph original, byte[,] pixels,
                                                int cellX, int cellY, int cellW, int cellH,
                                                int marginX, int marginY)
        {
            var expected = new byte[cellW, cellH];
            if (original != null && original.Present && original.Width > 0 && original.Height > 0)
            {
                byte[,] values = ReadGlyphValues(charset, original);
                for (int y = 0; y < original.Height; y++)
                {
                    for (int x = 0; x < original.Width; x++)
                    {
                        if (values[x, y] == 0) continue;
                        int px = marginX + original.XOffset + x;
                        int py = marginY + original.YOffset + y;
                        if (px < 0 || px >= cellW || py < 0 || py >= cellH) return false; // does not fit -> not comparable
                        expected[px, py] = values[x, y];
                    }
                }
            }

            for (int y = 0; y < cellH; y++)
                for (int x = 0; x < cellW; x++)
                    if (pixels[cellX + x, cellY + y] != expected[x, y]) return false;
            return true;
        }

        private static void RebuildRawContent(Charset charset, SlotPlan[] plans)
        {
            int newNumChars = charset.NumChars;
            for (int slot = 0; slot < 256; slot++)
                if (plans[slot] != null && plans[slot].Present && slot >= newNumChars) newNumChars = slot + 1;

            byte[] old = charset.RawContent;
            int bpp = charset.BitsPerPixel;
            int tableBytes = newNumChars * 4;
            int dataStartRel = TableStart + tableBytes - OffsetBase;

            var blob = new List<byte>();
            var offsets = new uint[newNumChars];
            for (int slot = 0; slot < newNumChars; slot++)
            {
                SlotPlan plan = slot < plans.Length ? plans[slot] : null;
                if (plan == null || !plan.Present) continue;

                offsets[slot] = (uint)(dataStartRel + blob.Count);
                if (plan.KeepOriginal != null)
                {
                    Glyph g = plan.KeepOriginal;
                    int length = 4 + (g.Width * g.Height * bpp + 7) / 8;
                    if (g.DataOffset + length > old.Length) length = old.Length - g.DataOffset;
                    for (int i = 0; i < length; i++) blob.Add(old[g.DataOffset + i]);
                }
                else
                {
                    blob.Add((byte)plan.W);
                    blob.Add((byte)plan.H);
                    blob.Add((byte)(sbyte)plan.XOff);
                    blob.Add((byte)(sbyte)plan.YOff);
                    WriteGlyphBits(blob, plan.Values, plan.W, plan.H, bpp);
                }
            }

            var raw = new byte[TableStart + tableBytes + blob.Count];
            Array.Copy(old, raw, Math.Min(TableStart, old.Length));
            raw[0x17] = (byte)(newNumChars & 0xFF);
            raw[0x18] = (byte)(newNumChars >> 8);
            for (int i = 0; i < newNumChars; i++)
            {
                uint off = offsets[i];
                int p = TableStart + i * 4;
                raw[p] = (byte)off;
                raw[p + 1] = (byte)(off >> 8);
                raw[p + 2] = (byte)(off >> 16);
                raw[p + 3] = (byte)(off >> 24);
            }
            blob.CopyTo(raw, TableStart + tableBytes);

            // The leading dword tracks the data size; preserve its original relation to the length.
            uint origInternal = (uint)(old[0] | (old[1] << 8) | (old[2] << 16) | (old[3] << 24));
            uint newInternal = (uint)(origInternal + (raw.Length - old.Length));
            raw[0] = (byte)newInternal;
            raw[1] = (byte)(newInternal >> 8);
            raw[2] = (byte)(newInternal >> 16);
            raw[3] = (byte)(newInternal >> 24);

            charset.RawContent = raw;
            charset.Reparse();
        }

        // ---------------------------------------------------------------------
        // Glyph pixel helpers
        // ---------------------------------------------------------------------

        private static byte[,] ReadGlyphValues(Charset charset, Glyph glyph)
        {
            byte[] raw = charset.RawContent;
            int bpp = charset.BitsPerPixel;
            var values = new byte[glyph.Width, glyph.Height];
            int bitPos = (glyph.DataOffset + 4) * 8;

            for (int y = 0; y < glyph.Height; y++)
            {
                for (int x = 0; x < glyph.Width; x++)
                {
                    int value = 0;
                    for (int i = 0; i < bpp; i++)
                    {
                        int bytePos = bitPos >> 3;
                        int bit = 7 - (bitPos & 7);
                        int b = bytePos < raw.Length ? ((raw[bytePos] >> bit) & 1) : 0;
                        value = (value << 1) | b;
                        bitPos++;
                    }
                    values[x, y] = (byte)value;
                }
            }
            return values;
        }

        private static void WriteGlyphBits(List<byte> output, byte[,] values, int w, int h, int bpp)
        {
            int acc = 0, bits = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    acc = (acc << bpp) | (values[x, y] & ((1 << bpp) - 1));
                    bits += bpp;
                    while (bits >= 8)
                    {
                        output.Add((byte)(acc >> (bits - 8)));
                        bits -= 8;
                        acc &= (1 << bits) - 1;
                    }
                }
            }
            if (bits > 0) output.Add((byte)(acc << (8 - bits)));
        }
    }
}
