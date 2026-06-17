using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer/editor for a SCUMM v3 charset (9N.LFL / CharsetV3): shows the header and an atlas of
    /// every 8x8 glyph (scaled up, with its hex slot id), so translators can see which characters the
    /// font provides. The PNG buttons export/import an editable 16x16 glyph atlas (via
    /// CharsetV3PngCodec) so the accented glyphs a translation needs can be drawn in an image editor.
    ///
    /// CharsetV3 is a standalone font file, not a BlockBase, so this is a plain UserControl driven by
    /// SetData (like the SOU audio viewers) rather than a BlockBaseControl in the block-type map.
    /// </summary>
    public class CharsetV3Control : UserControl
    {
        private const int Columns = 16;
        private const int Scale = 3;

        private readonly Label _header;
        private readonly PictureBox _picture;
        private readonly Button _exportButton;
        private readonly Button _importButton;
        private CharsetV3 _charset;

        public CharsetV3Control()
        {
            _header = new Label { Dock = DockStyle.Top, Height = 22, TextAlign = ContentAlignment.MiddleLeft };

            var topBar = new Panel { Dock = DockStyle.Top, Height = 30 };
            _exportButton = new Button { Text = "Export PNG", Width = 90, Left = 3, Top = 3, Enabled = false };
            _exportButton.Click += ExportClick;
            _importButton = new Button { Text = "Import PNG", Width = 90, Left = 99, Top = 3, Enabled = false };
            _importButton.Click += ImportClick;
            topBar.Controls.Add(_exportButton);
            topBar.Controls.Add(_importButton);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            _picture = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
            scroll.Controls.Add(_picture);

            Controls.Add(scroll);
            Controls.Add(topBar);
            Controls.Add(_header);
        }

        public void SetData(CharsetV3 charset)
        {
            _charset = charset;
            if (_picture.Image != null) { _picture.Image.Dispose(); _picture.Image = null; }

            if (_charset == null)
            {
                _header.Text = "(no font)";
                _exportButton.Enabled = false;
                _importButton.Enabled = false;
                return;
            }

            _header.Text = string.Format("{0} chars   ·   font height {1}", _charset.NumChars, _charset.FontHeight);
            _picture.Image = BuildAtlas(_charset);
            _exportButton.Enabled = true;
            _importButton.Enabled = true;
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_charset == null) return;

            using (var dlg = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = "charset.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string guidePath = Path.Combine(Path.GetDirectoryName(dlg.FileName) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(dlg.FileName) + ".guide.png");
                    CharsetV3PngCodec.ExportPng(_charset, dlg.FileName, guidePath);
                    MessageBox.Show(this,
                        "Font exported to:\n" + dlg.FileName +
                        "\n\nGuide image with the slot ids:\n" + guidePath +
                        "\n\nDraw the glyphs in the main PNG (indexed mode, 0 = background, 1 = ink) using the guide as a reference layer.",
                        "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Export failed: " + ex.Message, "Export PNG", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportClick(object sender, EventArgs e)
        {
            if (_charset == null) return;

            using (var dlg = new OpenFileDialog { Filter = "PNG image (*.png)|*.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string report = CharsetV3PngCodec.ImportPng(_charset, dlg.FileName);
                    SetData(_charset); // refresh header + atlas
                    MessageBox.Show(this, report + "\n\nUse \"Save changes\" to write it back to the game files.",
                        "Import PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Import failed: " + ex.Message, "Import PNG", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static Bitmap BuildAtlas(CharsetV3 charset)
        {
            int cellW = 8 * Scale + 6;
            int cellH = 8 * Scale + 14; // extra room for the slot label
            int rows = (256 + Columns - 1) / Columns;

            var bitmap = new Bitmap(Columns * cellW, rows * cellH, PixelFormat.Format32bppArgb);
            using (Graphics gfx = Graphics.FromImage(bitmap))
            using (var grid = new Pen(Color.FromArgb(230, 230, 230)))
            using (var labelFont = new Font("Consolas", 6f))
            using (var labelBrush = new SolidBrush(Color.FromArgb(140, 140, 140)))
            using (var inkBrush = new SolidBrush(Color.Black))
            {
                gfx.Clear(Color.White);
                gfx.InterpolationMode = InterpolationMode.NearestNeighbor;
                gfx.PixelOffsetMode = PixelOffsetMode.Half;

                for (int slot = 0; slot < 256; slot++)
                {
                    int cx = (slot % Columns) * cellW;
                    int cy = (slot / Columns) * cellH;
                    gfx.DrawRectangle(grid, cx, cy, cellW - 1, cellH - 1);
                    gfx.DrawString(slot.ToString("X2"), labelFont, labelBrush, cx + 1, cy + cellH - 12);

                    if (!charset.HasGlyph(slot)) continue;
                    using (Bitmap glyph = charset.RenderGlyph(slot, Color.White, Color.Black))
                    {
                        if (glyph != null)
                            gfx.DrawImage(glyph, new Rectangle(cx + 3, cy + 2, 8 * Scale, 8 * Scale));
                    }
                }
            }
            return bitmap;
        }
    }
}
