using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for CHAR (charset/font) blocks: shows the font header and an atlas of every
    /// glyph (rendered 2x, with its character index), so translators can see which characters
    /// the font provides. The PNG buttons export/import an editable glyph atlas so missing
    /// characters can be drawn in an image editor.
    /// </summary>
    public partial class CharsetControl : BlockBaseControl
    {
        private const int Columns = 16;
        private const int Scale = 2;

        private Charset _charset;

        public CharsetControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _charset = (Charset)blockBase;
            header.Text = string.Format("{0} chars ({1} present)   ·   {2} bits/pixel   ·   font height {3}",
                _charset.NumChars, _charset.PresentGlyphCount(), _charset.BitsPerPixel, _charset.FontHeight);

            if (atlas.Image != null) { atlas.Image.Dispose(); atlas.Image = null; }
            atlas.Image = BuildAtlas(_charset);
        }

        private void exportPngButton_Click(object sender, EventArgs e)
        {
            if (_charset == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "PNG image (*.png)|*.png",
                FileName = "charset.png"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string guidePath = GuidePathFor(dlg.FileName);
                CharsetPngCodec.ExportPng(_charset, dlg.FileName, guidePath);
                MessageBox.Show(this,
                    "Font exported to:\n" + dlg.FileName +
                    "\n\nGuide image with the slot ids:\n" + guidePath +
                    "\n\nDraw the glyphs in the main PNG (indexed mode) using the guide as a reference layer.",
                    "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Export PNG",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void importPngButton_Click(object sender, EventArgs e)
        {
            if (_charset == null) return;

            var dlg = new OpenFileDialog { Filter = "PNG image (*.png)|*.png" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                string report = CharsetPngCodec.ImportPng(_charset, dlg.FileName);
                SetAndRefreshData(_charset); // refresh header and atlas
                MessageBox.Show(this, report, "Import PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Import failed: " + ex.Message, "Import PNG",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string GuidePathFor(string pngPath)
        {
            return Path.Combine(Path.GetDirectoryName(pngPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(pngPath) + ".guide.png");
        }

        private static Bitmap BuildAtlas(Charset charset)
        {
            int maxW = 8, maxH = 8;
            foreach (Glyph g in charset.Glyphs)
            {
                if (!g.Present) continue;
                if (g.Width > maxW) maxW = g.Width;
                if (g.Height > maxH) maxH = g.Height;
            }

            int cellW = maxW * Scale + 8;
            int cellH = maxH * Scale + 16; // extra room for the index label
            int count = charset.Glyphs.Count;
            int rows = (count + Columns - 1) / Columns;
            if (rows < 1) rows = 1;

            var bitmap = new Bitmap(Columns * cellW, rows * cellH, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics gfx = Graphics.FromImage(bitmap))
            using (var grid = new Pen(Color.FromArgb(230, 230, 230)))
            using (var labelFont = new Font("Consolas", 6f))
            using (var labelBrush = new SolidBrush(Color.FromArgb(140, 140, 140)))
            {
                gfx.Clear(Color.White);
                gfx.InterpolationMode = InterpolationMode.NearestNeighbor;
                gfx.PixelOffsetMode = PixelOffsetMode.Half;

                for (int i = 0; i < count; i++)
                {
                    int cx = (i % Columns) * cellW;
                    int cy = (i / Columns) * cellH;

                    gfx.DrawRectangle(grid, cx, cy, cellW - 1, cellH - 1);
                    gfx.DrawString(i.ToString(), labelFont, labelBrush, cx + 1, cy + cellH - 13);

                    Glyph glyph = charset.Glyphs[i];
                    if (!glyph.Present) continue;

                    using (Bitmap rendered = charset.RenderGlyph(glyph))
                    {
                        if (rendered != null)
                            gfx.DrawImage(rendered, new Rectangle(cx + 4, cy + 2, rendered.Width * Scale, rendered.Height * Scale));
                    }
                }
            }
            return bitmap;
        }
    }
}
