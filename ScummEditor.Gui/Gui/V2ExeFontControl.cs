using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>Tree-node payload for the v2 EXE-embedded font: just the path to MANIAC.EXE / ZAK.EXE.</summary>
    public class V2ExeFontRef
    {
        public string ExePath;
    }

    /// <summary>
    /// Viewer/editor for the SCUMM v2 font that lives inside the game executable (MANIAC.EXE / ZAK.EXE)
    /// rather than in the LFL data. It reuses the v3 charset atlas (the glyph bit layout is identical):
    /// <see cref="ScummV2ExeFontCodec.BuildCharset"/> wraps the decoded glyphs in a CharsetV3 and
    /// <see cref="CharsetV3Control.BuildAtlas"/> renders it. Export/Import go through the codec; on import
    /// the patched executable is written to a user-chosen path (a copy, so the original EXE is preserved),
    /// because the EXE font is not part of the normal "Save changes" LFL pipeline.
    ///
    /// A standalone resource, not a BlockBase, so this is a plain UserControl driven by SetData. Note: an
    /// EXE-font edit is visible only under the original DOS engine (DOSBox); ScummVM hardcodes its own font.
    /// </summary>
    public class V2ExeFontControl : UserControl
    {
        private readonly Label _header;
        private readonly Button _exportButton;
        private readonly Button _importButton;
        private readonly PictureBox _picture;

        private V2ExeFontRef _ref;
        private ScummV2ExeFont _font;

        public V2ExeFontControl()
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

        public void SetData(V2ExeFontRef fontRef)
        {
            _ref = fontRef;
            _font = null;
            if (_picture.Image != null) { _picture.Image.Dispose(); _picture.Image = null; }
            _exportButton.Enabled = false;
            _importButton.Enabled = false;

            if (fontRef == null || string.IsNullOrEmpty(fontRef.ExePath) || !File.Exists(fontRef.ExePath))
            {
                _header.Text = "(no executable)";
                return;
            }

            string error;
            try { _font = ScummV2ExeFont.Read(File.ReadAllBytes(fontRef.ExePath), out error); }
            catch (Exception ex) { _header.Text = "Could not read " + Path.GetFileName(fontRef.ExePath) + ": " + ex.Message; return; }

            if (_font == null)
            {
                _header.Text = "Font not found in " + Path.GetFileName(fontRef.ExePath) + ": " + error;
                return;
            }

            RefreshAtlas();
            _exportButton.Enabled = true;
            _importButton.Enabled = true;
        }

        private void RefreshAtlas()
        {
            if (_picture.Image != null) { _picture.Image.Dispose(); _picture.Image = null; }
            if (_font == null) return;
            _header.Text = string.Format("EXE font ({0})   ·   {1} glyphs   ·   8x8",
                Path.GetFileName(_ref.ExePath), ScummV2ExeFont.GlyphCount);
            _picture.Image = CharsetV3Control.BuildAtlas(ScummV2ExeFontCodec.BuildCharset(_font.GlyphBytes));
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_font == null) return;
            using (var dlg = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = "font.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string guidePath = Path.Combine(Path.GetDirectoryName(dlg.FileName) ?? string.Empty,
                        Path.GetFileNameWithoutExtension(dlg.FileName) + ".guide.png");
                    ScummV2ExeFontCodec.ExportPng(_font, dlg.FileName, guidePath);
                    MessageBox.Show(this,
                        "Font exported to:\n" + dlg.FileName +
                        "\n\nGuide image with the slot ids:\n" + guidePath +
                        "\n\nEdit the punctuation/symbol slots in the main PNG to hold accented letters, then use Import PNG." +
                        "\n\nNote: an EXE-font edit shows only under the original DOS engine (DOSBox), not ScummVM.",
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
            if (_font == null) return;
            using (var open = new OpenFileDialog { Filter = "PNG image (*.png)|*.png" })
            {
                if (open.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string report = ScummV2ExeFontCodec.ImportPng(_font, open.FileName);

                    // The patched executable is the same size, but write it to a user-chosen path (defaulting
                    // to a copy next to the original) so the original EXE need not be overwritten.
                    using (var save = new SaveFileDialog
                    {
                        Filter = "Game executable (*.exe)|*.exe|All files (*.*)|*.*",
                        FileName = Path.GetFileName(_ref.ExePath),
                        InitialDirectory = Path.GetDirectoryName(_ref.ExePath),
                        Title = "Save the patched executable"
                    })
                    {
                        if (save.ShowDialog(this) != DialogResult.OK) return;
                        File.WriteAllBytes(save.FileName, _font.ExeBytes);
                        RefreshAtlas();
                        MessageBox.Show(this,
                            report + "\n\nPatched executable written to:\n" + save.FileName +
                            "\n\nThe edited glyphs render under the original DOS engine (DOSBox), not ScummVM.",
                            "Import PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Import failed: " + ex.Message, "Import PNG", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
