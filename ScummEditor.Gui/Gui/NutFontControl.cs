using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer/editor for a SCUMM v7 external .NUT SMUSH font (The Dig, Full Throttle): the glyphs listed
    /// on the left, the decoded glyph on the right. Glyphs are decoded by NutImageDecoder (codec 1/3 BOMP,
    /// 21/44 skip-copy). A NUT carries no palette of its own (its pixels are runtime palette indices), so a
    /// palette combobox controls how the glyph is previewed: a high-contrast "Glyph shape" silhouette (the
    /// default - font ink is often a near-white palette index that would be invisible on a light background),
    /// a literal grayscale-by-index ramp, or one of the game's room palettes. This is only a preview;
    /// editing is index-based (the exported PNG carries the raw indices). "Export/Import PNG" edits the
    /// selected glyph; "Export/Import font" round-trips the whole font as one atlas. A NutFont is a
    /// standalone file, not a BlockBase, so this is a plain UserControl driven by SetData.
    /// </summary>
    public class NutFontControl : UserControl
    {
        private readonly SplitContainer _split;
        private readonly TreeView _tree;
        private readonly Panel _scroll;
        private readonly PictureBox _picture;
        private readonly Label _info;
        private readonly ComboBox _paletteBox;
        private readonly Button _exportGlyphButton;
        private readonly Button _importGlyphButton;
        private readonly Button _exportFontButton;
        private readonly Button _importFontButton;

        private NutFontResource _resource;
        private List<Color[]> _gamePalettes;
        private int _currentGlyph = -1;
        private bool _splitterApplied;

        /// <summary>A combobox palette choice: the colours used to preview a glyph on screen, and the
        /// colours written as the PLTE of an exported PNG (kept index-distinguishable so the file is
        /// editable even when the preview is a flat silhouette).</summary>
        private class PaletteChoice
        {
            public string Name;
            public Color[] Preview;
            public Color[] Export;
            public override string ToString() { return Name; }
        }

        public NutFontControl()
        {
            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            _tree.AfterSelect += TreeAfterSelect;
            _split.Panel1.Controls.Add(_tree);

            _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.LightGray };
            _picture = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
            _scroll.Controls.Add(_picture);

            var topBar = new Panel { Dock = DockStyle.Top, Height = 60 };
            _exportGlyphButton = new Button { Text = "Export PNG", Width = 90, Left = 3, Top = 3, Enabled = false };
            _exportGlyphButton.Click += ExportGlyphClick;
            _importGlyphButton = new Button { Text = "Import PNG", Width = 90, Left = 99, Top = 3, Enabled = false };
            _importGlyphButton.Click += ImportGlyphClick;
            _exportFontButton = new Button { Text = "Export font", Width = 90, Left = 201, Top = 3, Enabled = false };
            _exportFontButton.Click += ExportFontClick;
            _importFontButton = new Button { Text = "Import font", Width = 90, Left = 297, Top = 3, Enabled = false };
            _importFontButton.Click += ImportFontClick;
            var paletteLabel = new Label { Text = "Palette:", AutoSize = true, Left = 3, Top = 38 };
            _paletteBox = new ComboBox { Left = 54, Top = 34, Width = 237, DropDownStyle = ComboBoxStyle.DropDownList };
            _paletteBox.SelectedIndexChanged += (s, e) => RenderCurrent();
            _info = new Label { Left = 297, Top = 38, AutoSize = true, Text = string.Empty };
            topBar.Controls.Add(_exportGlyphButton);
            topBar.Controls.Add(_importGlyphButton);
            topBar.Controls.Add(_exportFontButton);
            topBar.Controls.Add(_importFontButton);
            topBar.Controls.Add(paletteLabel);
            topBar.Controls.Add(_paletteBox);
            topBar.Controls.Add(_info);

            _split.Panel2.Controls.Add(_scroll);
            _split.Panel2.Controls.Add(topBar);

            Controls.Add(_split);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (_split != null && !_splitterApplied && _split.Width > 200)
            {
                _split.SplitterDistance = 150;
                _splitterApplied = true;
            }
        }

        /// <summary>Shows a NUT font. <paramref name="gamePalettes"/> are optional game room palettes offered
        /// in the combobox (beside the shape/grayscale views) so a glyph can be previewed in real colours.</summary>
        public void SetData(NutFontResource resource, List<Color[]> gamePalettes)
        {
            _resource = resource;
            _gamePalettes = gamePalettes;
            _tree.Nodes.Clear();
            ClearImage();
            _currentGlyph = -1;
            _exportGlyphButton.Enabled = false;
            _importGlyphButton.Enabled = false;
            _exportFontButton.Enabled = false;
            _importFontButton.Enabled = false;
            _info.Text = string.Empty;
            if (_resource == null || _resource.Font == null) return;

            NutFont font = _resource.Font;
            for (int i = 0; i < font.Glyphs.Count; i++)
            {
                NutGlyph g = font.Glyphs[i];
                string text = g.HasPixels
                    ? string.Format("Glyph {0} ({1}x{2})", i, g.Width, g.Height)
                    : string.Format("Glyph {0} - empty", i);
                var node = _tree.Nodes.Add(text);
                node.Tag = i;
            }

            BuildPaletteChoices(gamePalettes);
            _exportFontButton.Enabled = font.IsValid;
            _importFontButton.Enabled = font.IsValid;

            if (_tree.Nodes.Count > 0)
            {
                _tree.SelectedNode = _tree.Nodes[0];
            }
            else
            {
                _info.Text = font.IsValid ? "(no glyphs)" : "(not a parseable NUT font)";
            }
        }

        private void BuildPaletteChoices(List<Color[]> gamePalettes)
        {
            _paletteBox.Items.Clear();
            Color[] gray = GrayscalePalette();

            // Default: a flat dark silhouette - always clearly visible whatever palette index the font's ink
            // uses (font ink is commonly a near-white index that vanishes on the light background in grayscale).
            _paletteBox.Items.Add(new PaletteChoice { Name = "Glyph shape", Preview = SilhouettePalette(), Export = gray });
            _paletteBox.Items.Add(new PaletteChoice { Name = "Grayscale (by index)", Preview = gray, Export = gray });
            if (gamePalettes != null)
            {
                for (int i = 0; i < gamePalettes.Count; i++)
                {
                    _paletteBox.Items.Add(new PaletteChoice
                    {
                        Name = gamePalettes.Count > 1 ? "Game palette " + i : "Game palette",
                        Preview = gamePalettes[i],
                        Export = gamePalettes[i],
                    });
                }
            }
            _paletteBox.SelectedIndex = 0;
        }

        private PaletteChoice SelectedChoice()
        {
            return _paletteBox.SelectedItem as PaletteChoice;
        }

        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            _currentGlyph = (e.Node != null && e.Node.Tag is int) ? (int)e.Node.Tag : -1;
            RenderCurrent();
        }

        private void RenderCurrent()
        {
            ClearImage();
            _exportGlyphButton.Enabled = false;
            _importGlyphButton.Enabled = false;
            if (_resource == null || _resource.Font == null || _currentGlyph < 0)
            {
                return;
            }

            NutFont font = _resource.Font;
            NutGlyph g = font.Glyphs[_currentGlyph];
            string baseInfo = string.Format("codec {0}  |  glyph {1}: {2}x{3}", g.Codec, _currentGlyph, g.Width, g.Height);
            if (!g.HasPixels)
            {
                _info.Text = baseInfo + " - empty (no frame object)";
                return;
            }

            try
            {
                byte[,] indices = NutImageDecoder.DecodeGlyphIndices(font, _currentGlyph);
                if (indices == null)
                {
                    _info.Text = baseInfo + " - not decodable (unsupported codec)";
                    return;
                }

                int transparency = NutImageDecoder.TransparencyIndex(g.Codec);
                PaletteChoice choice = SelectedChoice();
                Color[] preview = choice != null ? choice.Preview : GrayscalePalette();
                _picture.Image = IndexedImageHelper.FromIndexMatrix(indices, preview, transparency);

                _exportGlyphButton.Enabled = true;
                _importGlyphButton.Enabled = NutImageEncoder.CanEncode(g.Codec);
                _info.Text = baseInfo + (HasInk(indices, transparency) ? string.Empty : "  (blank - no ink, e.g. a space or placeholder)");
            }
            catch (Exception ex)
            {
                _info.Text = baseInfo + " - decode failed: " + ex.Message;
            }
        }

        private void ExportGlyphClick(object sender, EventArgs e)
        {
            if (_resource == null || _currentGlyph < 0) return;
            using (var dialog = new SaveFileDialog
            {
                Filter = "PNG Files|*.png",
                FileName = string.Format("{0} glyph{1}.png", FontName(), _currentGlyph)
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    NutFontPngCodec.ExportGlyphPng(_resource.Font, _currentGlyph, dialog.FileName, ExportPalette());
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ImportGlyphClick(object sender, EventArgs e)
        {
            if (_resource == null || _currentGlyph < 0) return;
            using (var dialog = new OpenFileDialog { Filter = "PNG Files|*.png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                int reselect = _currentGlyph;
                try
                {
                    NutFontPngCodec.ImportGlyphPng(_resource.Font, _currentGlyph, dialog.FileName);
                }
                catch (ImageEncodeException ex)
                {
                    MessageBox.Show(ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SetData(_resource, _gamePalettes);
                if (reselect < _tree.Nodes.Count) _tree.SelectedNode = _tree.Nodes[reselect];
                MessageBox.Show("Glyph imported. Use \"Save changes\" to write it back to the game files.",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ExportFontClick(object sender, EventArgs e)
        {
            if (_resource == null) return;
            using (var dialog = new SaveFileDialog { Filter = "PNG Files|*.png", FileName = FontName() + ".png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    string guidePath = Path.Combine(Path.GetDirectoryName(dialog.FileName) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(dialog.FileName) + ".guide.png");
                    NutFontPngCodec.ExportPng(_resource.Font, dialog.FileName, guidePath, ExportPalette());
                    MessageBox.Show("Font exported as one atlas (all glyphs in a grid), plus a .guide.png with the " +
                        "cell grid and glyph indices. Edit the atlas as an INDEXED PNG, then Import font.",
                        "Export font", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void ImportFontClick(object sender, EventArgs e)
        {
            if (_resource == null) return;
            using (var dialog = new OpenFileDialog { Filter = "PNG Files|*.png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try
                {
                    NutFontPngCodec.ImportPng(_resource.Font, dialog.FileName);
                }
                catch (ImageEncodeException ex)
                {
                    MessageBox.Show(ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                int reselect = _currentGlyph;
                SetData(_resource, _gamePalettes);
                if (reselect >= 0 && reselect < _tree.Nodes.Count) _tree.SelectedNode = _tree.Nodes[reselect];
                MessageBox.Show("Font imported. Use \"Save changes\" to write it back to the game files.",
                    "Import font", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private Color[] ExportPalette()
        {
            PaletteChoice choice = SelectedChoice();
            return choice != null ? choice.Export : GrayscalePalette();
        }

        private static bool HasInk(byte[,] indices, int transparency)
        {
            for (int x = 0; x < indices.GetLength(0); x++)
                for (int y = 0; y < indices.GetLength(1); y++)
                    if (indices[x, y] != transparency) return true;
            return false;
        }

        private static Color[] GrayscalePalette()
        {
            var palette = new Color[256];
            for (int i = 0; i < 256; i++) palette[i] = Color.FromArgb(i, i, i);
            return palette;
        }

        private static Color[] SilhouettePalette()
        {
            var palette = new Color[256];
            for (int i = 0; i < 256; i++) palette[i] = Color.FromArgb(32, 32, 32); // ink is drawn dark; the transparent index is made transparent
            return palette;
        }

        private string FontName()
        {
            return _resource != null && _resource.FilePath != null
                ? Path.GetFileNameWithoutExtension(_resource.FilePath)
                : "font";
        }

        private void ClearImage()
        {
            if (_picture.Image != null)
            {
                _picture.Image.Dispose();
                _picture.Image = null;
            }
        }
    }
}
