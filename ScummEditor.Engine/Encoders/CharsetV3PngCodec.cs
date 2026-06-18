using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /*
    Exports a SCUMM v3 charset (9N.LFL / CharsetV3) to an editable PNG atlas and imports it back, so
    translators can draw the accented glyphs a Portuguese/European translation needs.

    v3 glyphs are far simpler than the v4/v5/v6 CHAR block: every glyph is a FIXED 8x8 1-bpp bitmap
    (bit 7 = leftmost) plus a one-byte cursor-advance width. So the atlas is a plain 16 x 16 grid of
    8x8 cells (128 x 128 px); slot id = row*16 + column, which IS the text byte / glyph index.

    Atlas conventions:
      - Always 256 slots, even when the font declares fewer chars - drawing in a slot beyond numChars
        extends the font on import (the width table and glyph table are rebuilt).
      - The PNG is 8bpp indexed: pixel 0 = background, 1 = ink. It must stay indexed when edited.
      - A companion guide PNG (scaled up, with the grid, hex slot ids and current glyphs) is written
        as a reference layer.

    Import safety: a cell whose pixels equal what export produces for the current glyph keeps the
    original glyph bytes + width verbatim, so an unedited atlas round-trips byte-for-byte and only the
    edited/added slots change. A new glyph's advance width is its ink width + 1 (clamped to 8).
    */
    public static class CharsetV3PngCodec
    {
        private const int Columns = 16;
        private const int Rows = 16;       // always 256 slots so fonts can be extended
        private const int GlyphSize = 8;   // v3 glyphs are a fixed 8x8 1-bpp bitmap
        private const int GlyphBytes = 8;  // 8 rows, one byte each
        private const int HeaderSize = 8;  // size+reserved+reserved+numChars+fontHeight
        private const int GuideScale = 6;  // guide atlas is drawn this many times larger

        // ---------------------------------------------------------------------
        // Export
        // ---------------------------------------------------------------------

        public static void ExportPng(CharsetV3 charset, string pngPath, string guidePath)
        {
            int width = Columns * GlyphSize, height = Rows * GlyphSize;
            var matrix = new byte[width, height];

            for (int slot = 0; slot < 256; slot++)
            {
                if (!charset.HasGlyph(slot))
                {
                    continue;
                }
                byte[,] values = ReadGlyphValues(charset, slot);
                int cellX = (slot % Columns) * GlyphSize;
                int cellY = (slot / Columns) * GlyphSize;
                for (int y = 0; y < GlyphSize; y++)
                {
                    for (int x = 0; x < GlyphSize; x++)
                    {
                        if (values[x, y] != 0)
                        {
                            matrix[cellX + x, cellY + y] = 1;
                        }
                    }
                }
            }

            using (Bitmap bitmap = IndexedImageHelper.FromIndexMatrix(matrix, BuildEditPalette(), -1))
            {
                bitmap.Save(pngPath, ImageFormat.Png);
            }

            using (Bitmap guide = BuildGuide(charset))
            {
                guide.Save(guidePath, ImageFormat.Png);
            }
        }

        /// <summary>Palette for the editable atlas: 0 = background (black), 1 = ink (white).</summary>
        public static Color[] BuildEditPalette()
        {
            var palette = new Color[256];
            palette[0] = Color.Black;
            palette[1] = Color.White;
            for (int v = 2; v < 256; v++)
            {
                palette[v] = Color.Magenta; // painting with these would be invalid
            }
            return palette;
        }

        private static Bitmap BuildGuide(CharsetV3 charset)
        {
            int cell = GlyphSize * GuideScale;
            var bitmap = new Bitmap(Columns * cell, Rows * cell, PixelFormat.Format24bppRgb);
            using (Graphics gfx = Graphics.FromImage(bitmap))
            using (var extensionBrush = new SolidBrush(Color.FromArgb(255, 250, 220)))
            using (var gridPen = new Pen(Color.FromArgb(190, 190, 190)))
            using (var glyphBrush = new SolidBrush(Color.FromArgb(120, 120, 120)))
            using (var idFont = new Font("Consolas", 7f))
            using (var idBrush = new SolidBrush(Color.Red))
            {
                gfx.Clear(Color.White);
                for (int slot = 0; slot < 256; slot++)
                {
                    int cx = (slot % Columns) * cell;
                    int cy = (slot / Columns) * cell;

                    if (slot >= charset.NumChars)
                    {
                        gfx.FillRectangle(extensionBrush, cx, cy, cell, cell);
                    }
                    gfx.DrawRectangle(gridPen, cx, cy, cell - 1, cell - 1);

                    if (charset.HasGlyph(slot))
                    {
                        byte[,] values = ReadGlyphValues(charset, slot);
                        for (int y = 0; y < GlyphSize; y++)
                        {
                            for (int x = 0; x < GlyphSize; x++)
                            {
                                if (values[x, y] != 0)
                                {
                                    gfx.FillRectangle(glyphBrush, cx + x * GuideScale, cy + y * GuideScale, GuideScale, GuideScale);
                                }
                            }
                        }
                    }

                    gfx.DrawString(slot.ToString("X2"), idFont, idBrush, cx + 1, cy + 1);
                }
            }
            return bitmap;
        }

        // ---------------------------------------------------------------------
        // Batch (every charset of the game at once)
        // ---------------------------------------------------------------------

        /// <summary>Exports every v3 charset as charset_N.png + charset_N.guide.png into a folder.</summary>
        public static string ExportAll(List<CharsetV3> charsets, string folder)
        {
            for (int i = 0; i < charsets.Count; i++)
            {
                ExportPng(charsets[i],
                    Path.Combine(folder, "charset_" + i + ".png"),
                    Path.Combine(folder, "charset_" + i + ".guide.png"));
            }
            return charsets.Count + " fonts exported to:\n" + folder;
        }

        /// <summary>Imports every charset_N.png found in the folder back into the given charsets (export order).</summary>
        public static string ImportAll(List<CharsetV3> charsets, string folder)
        {
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
            report.Append(string.Format("{0} font(s) processed, {1} without a file, {2} with errors.", imported, missing, failed));
            return report.ToString();
        }

        // ---------------------------------------------------------------------
        // Import
        // ---------------------------------------------------------------------

        private class SlotPlan
        {
            public bool Present;
            public bool KeepOriginal; // copy the original glyph bytes + width verbatim
            public byte[] Glyph;      // 8 bytes (8 rows of 1-bpp pixels)
            public int Width;         // cursor advance
        }

        public static string ImportPng(CharsetV3 charset, string pngPath)
        {
            byte[,] pixels;
            int width, height;
            using (var loaded = (Bitmap)Image.FromFile(pngPath))
            {
                if (!IndexedImageHelper.IsIndexed(loaded))
                {
                    throw new InvalidDataException(
                        "The PNG must be in indexed color mode (8 bits). Use the file exported by the editor and keep its color mode.");
                }
                pixels = IndexedImageHelper.GetIndexMatrix(loaded);
                width = loaded.Width;
                height = loaded.Height;
            }

            if (width != Columns * GlyphSize || height != Rows * GlyphSize)
            {
                throw new InvalidDataException(string.Format(
                    "Invalid dimensions {0}x{1}: a v3 font atlas must be exactly {2}x{3} (a 16x16 grid of 8x8 glyphs).",
                    width, height, Columns * GlyphSize, Rows * GlyphSize));
            }

            var plans = new SlotPlan[256];
            var badValues = new List<string>();
            int changed = 0, added = 0, removed = 0;

            for (int slot = 0; slot < 256; slot++)
            {
                int cellX = (slot % Columns) * GlyphSize;
                int cellY = (slot / Columns) * GlyphSize;

                var values = new byte[GlyphSize, GlyphSize];
                bool anyPixel = false;
                int inkRight = -1;
                for (int y = 0; y < GlyphSize; y++)
                {
                    for (int x = 0; x < GlyphSize; x++)
                    {
                        byte v = pixels[cellX + x, cellY + y];
                        if (v == 0) continue;
                        if (v > 1 && badValues.Count < 8)
                        {
                            badValues.Add(string.Format("slot 0x{0:X2}: value {1} (only 0 and 1 are valid)", slot, v));
                        }
                        values[x, y] = 1;
                        anyPixel = true;
                        if (x > inkRight) inkRight = x;
                    }
                }

                bool origPresent = charset.HasGlyph(slot);

                if (CellMatchesOriginal(charset, slot, values))
                {
                    if (origPresent) plans[slot] = new SlotPlan { Present = true, KeepOriginal = true };
                    continue; // unchanged
                }

                if (!anyPixel)
                {
                    if (origPresent) removed++;
                    continue; // cleared cell -> glyph absent
                }

                // New/edited glyph: keep the original advance if it had one, else ink width + 1.
                int advance = origPresent ? charset.CharWidth(slot) : Math.Min(GlyphSize, inkRight + 2);
                if (advance <= 0) advance = Math.Min(GlyphSize, inkRight + 2);

                plans[slot] = new SlotPlan { Present = true, Glyph = PackGlyph(values), Width = advance };
                if (origPresent) changed++; else added++;
            }

            if (badValues.Count > 0)
            {
                throw new InvalidDataException("Pixels with invalid values (a v3 font is 1-bit):\n  "
                    + string.Join("\n  ", badValues.ToArray()));
            }

            if (changed + added + removed == 0)
            {
                return "No changes found - the font was not modified.";
            }

            RebuildRawContent(charset, plans);

            var report = new StringBuilder();
            report.AppendLine(string.Format("Glyphs changed: {0}, added: {1}, removed: {2}.", changed, added, removed));
            report.Append(string.Format("numChars: {0} (use 'Save Changes' to write it to the game files).", charset.NumChars));
            return report.ToString();
        }

        /// <summary>True when the cell pixels equal what ExportPng would draw for the current glyph.</summary>
        private static bool CellMatchesOriginal(CharsetV3 charset, int slot, byte[,] cellValues)
        {
            if (!charset.HasGlyph(slot))
            {
                // absent glyph: matches only an all-zero cell
                for (int y = 0; y < GlyphSize; y++)
                    for (int x = 0; x < GlyphSize; x++)
                        if (cellValues[x, y] != 0) return false;
                return true;
            }

            byte[,] original = ReadGlyphValues(charset, slot);
            for (int y = 0; y < GlyphSize; y++)
                for (int x = 0; x < GlyphSize; x++)
                    if (cellValues[x, y] != original[x, y]) return false;
            return true;
        }

        /// <summary>
        /// Rebuilds the whole charset file from the slot plans: [8-byte header][numChars width bytes]
        /// [numChars x 8 glyph bytes]. numChars grows to cover the highest present slot; gaps get a
        /// width of 0 and a blank 8-byte glyph (a valid, if unused, entry).
        /// </summary>
        private static void RebuildRawContent(CharsetV3 charset, SlotPlan[] plans)
        {
            byte[] old = charset.RawContent;

            int newNumChars = charset.NumChars;
            for (int slot = 0; slot < 256; slot++)
            {
                if (plans[slot] != null && plans[slot].Present && slot + 1 > newNumChars)
                {
                    newNumChars = slot + 1;
                }
            }

            int widthTableStart = HeaderSize;
            int glyphTableStart = HeaderSize + newNumChars;
            var raw = new byte[glyphTableStart + newNumChars * GlyphBytes];

            // Header: keep the original 8 bytes, then patch numChars (kept fields like the 0x0163
            // reserved word survive). If the original was somehow shorter, the rest stays zero.
            Array.Copy(old, raw, Math.Min(HeaderSize, old.Length));
            raw[6] = (byte)newNumChars;

            for (int slot = 0; slot < newNumChars; slot++)
            {
                SlotPlan plan = plans[slot];
                int glyphPos = glyphTableStart + slot * GlyphBytes;

                if (plan != null && plan.Present && !plan.KeepOriginal)
                {
                    raw[widthTableStart + slot] = (byte)plan.Width;
                    Array.Copy(plan.Glyph, 0, raw, glyphPos, GlyphBytes);
                }
                else if (charset.HasGlyph(slot))
                {
                    // unchanged (or kept) glyph: copy the original width + 8 glyph bytes verbatim
                    raw[widthTableStart + slot] = (byte)charset.CharWidth(slot);
                    byte[] original = OriginalGlyphBytes(charset, slot);
                    Array.Copy(original, 0, raw, glyphPos, GlyphBytes);
                }
                // else: gap slot -> width 0, blank glyph (already zero)
            }

            // The leading size word is "file length + 1" in the games observed; preserve that relation.
            int sizeWord = raw.Length + 1;
            raw[0] = (byte)sizeWord;
            raw[1] = (byte)(sizeWord >> 8);

            charset.RawContent = raw;
            charset.Reparse();
        }

        // ---------------------------------------------------------------------
        // Glyph pixel helpers
        // ---------------------------------------------------------------------

        private static byte[,] ReadGlyphValues(CharsetV3 charset, int slot)
        {
            byte[] glyph = OriginalGlyphBytes(charset, slot);
            var values = new byte[GlyphSize, GlyphSize];
            for (int row = 0; row < GlyphSize; row++)
            {
                byte bits = glyph[row];
                for (int col = 0; col < GlyphSize; col++)
                {
                    values[col, row] = (byte)((bits >> (7 - col)) & 1);
                }
            }
            return values;
        }

        private static byte[] OriginalGlyphBytes(CharsetV3 charset, int slot)
        {
            var glyph = new byte[GlyphBytes];
            byte[] raw = charset.RawContent;
            int baseOffset = HeaderSize + charset.NumChars + slot * GlyphBytes;
            for (int i = 0; i < GlyphBytes; i++)
            {
                int p = baseOffset + i;
                glyph[i] = (p >= 0 && p < raw.Length) ? raw[p] : (byte)0;
            }
            return glyph;
        }

        private static byte[] PackGlyph(byte[,] values)
        {
            var glyph = new byte[GlyphBytes];
            for (int row = 0; row < GlyphSize; row++)
            {
                byte bits = 0;
                for (int col = 0; col < GlyphSize; col++)
                {
                    if (values[col, row] != 0)
                    {
                        bits |= (byte)(1 << (7 - col));
                    }
                }
                glyph[row] = bits;
            }
            return glyph;
        }
    }
}
