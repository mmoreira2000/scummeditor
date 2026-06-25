using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for a SCUMM v7 AKOS costume (The Dig, Full Throttle): the costume's cels (frames) listed
    /// on the left, the decoded cel on the right, with PNG export. It renders via AkosImageDecoder, which
    /// decodes cel codecs 1 (BYLE RLE), 5 (BOMP) and 16 (MAJMIN). Codec 1/5 cels carry their own colours
    /// (RGBS); codec 16 cels are masks with no colours of their own, so a palette combobox lets the user
    /// render any cel against a chosen room palette (how the costume looks in that room). AKOS interleaves
    /// real frames with tiny 1x1/2x1 placeholder cels for unused animation slots - those show as their
    /// real (near-empty) size and are labelled, so a "blank" preview is understood, not mistaken for a bug.
    /// </summary>
    public class AkosCostumeControl : BlockBaseControl
    {
        private readonly SplitContainer _split;
        private readonly TreeView _tree;
        private readonly Panel _scroll;
        private readonly PictureBox _picture;
        private readonly Label _info;
        private readonly ComboBox _paletteBox;
        private readonly Button _exportButton;

        private BlockBase _akos;
        private int _currentCelIndex = -1;
        private string _codecInfo = string.Empty;
        private bool _splitterApplied;

        /// <summary>A render-palette choice for the combobox; Palette null means the costume's own colours.</summary>
        private class PaletteChoice
        {
            public string Name;
            public Color[] Palette;
            public override string ToString() { return Name; }
        }

        public AkosCostumeControl()
        {
            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };

            _tree = new TreeView { Dock = DockStyle.Fill, HideSelection = false };
            _tree.AfterSelect += TreeAfterSelect;
            _split.Panel1.Controls.Add(_tree);

            _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.LightGray };
            _picture = new PictureBox { SizeMode = PictureBoxSizeMode.AutoSize };
            _scroll.Controls.Add(_picture);

            // Two-row top bar: row 1 = export + palette selector, row 2 = codec / cel info.
            var topBar = new Panel { Dock = DockStyle.Top, Height = 54 };
            // Export only for now: per-cel IMPORT needs the AKOS cel ENCODERS (codec 1 BYLE-RLE, 5 BOMP,
            // 16 MAJMIN) plus re-splicing AKCD + fixing AKOF/AKCI/sizes + the LFLF/LECF offsets - that is
            // the Phase D "encode" step, still to come. Until then this viewer is read-only (no Import
            // button), matching how each phase ships decode first, then the matching encoder.
            _exportButton = new Button { Text = "Export PNG", Width = 90, Left = 3, Top = 3, Enabled = false };
            _exportButton.Click += ExportClick;
            var paletteLabel = new Label { Text = "Palette:", AutoSize = true, Left = 99, Top = 8 };
            _paletteBox = new ComboBox { Left = 150, Top = 4, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
            _paletteBox.SelectedIndexChanged += (s, e) => RenderCurrent();
            _info = new Label { Left = 3, Top = 32, AutoSize = true, Text = string.Empty };
            topBar.Controls.Add(_exportButton);
            topBar.Controls.Add(paletteLabel);
            topBar.Controls.Add(_paletteBox);
            topBar.Controls.Add(_info);

            _split.Panel2.Controls.Add(_scroll);
            _split.Panel2.Controls.Add(topBar);

            Controls.Add(_split);
            _split.BringToFront();
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

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _akos = blockBase;
            _tree.Nodes.Clear();
            ClearImage();
            _currentCelIndex = -1;
            _exportButton.Enabled = false;
            _info.Text = string.Empty;
            if (_akos == null) return;

            int codec = AkosImageDecoder.GetCodec(_akos);
            int celCount = AkosImageDecoder.GetCelCount(_akos);
            _codecInfo = string.Format("codec {0}, {1} cels", codec, celCount);

            for (int i = 0; i < celCount; i++)
            {
                Size sz = AkosImageDecoder.GetCelSize(_akos, i);
                bool placeholder = sz.Width * sz.Height <= 4; // AKOS uses tiny 1x1/2x1 cels for unused slots
                string text = string.Format("Cel {0} ({1}x{2}){3}", i, sz.Width, sz.Height, placeholder ? " - empty" : string.Empty);
                var node = _tree.Nodes.Add(text);
                node.Tag = i;
            }

            BuildPaletteChoices();

            if (_tree.Nodes.Count > 0)
            {
                _tree.SelectedNode = _tree.Nodes[0];
            }
        }

        /// <summary>
        /// Populates the palette combobox with only the palettes that actually apply to this costume: its
        /// OWN colours (RGBS, codec 1/5) - or a grayscale ramp when it has none (codec-16 masks/full-screen
        /// animations carry no RGBS; their true colours come from a runtime palette) - plus the palette(s)
        /// of the costume's OWN room (the ROOM in the same LFLF). Other rooms' palettes are not offered:
        /// a costume is drawn with its own colours or its room's, not an arbitrary room's.
        /// </summary>
        private void BuildPaletteChoices()
        {
            _paletteBox.Items.Clear();

            bool ownPalette = AkosImageDecoder.HasOwnPalette(_akos);
            _paletteBox.Items.Add(new PaletteChoice
            {
                Name = ownPalette ? "Costume (own colours)" : "Grayscale (costume has no palette)",
                Palette = null,
            });

            try
            {
                var lflf = _akos.Parent as DiskBlock;
                RoomBlock room = lflf != null ? lflf.GetROOM() : null;
                PalettesData pals = room != null ? room.GetPALS() : null;
                var wrap = pals != null ? pals.GetWRAP() : null;
                List<PaletteData> apals = wrap != null ? wrap.GetAPALs() : null;
                if (apals != null)
                {
                    for (int p = 0; p < apals.Count; p++)
                    {
                        string name = apals.Count > 1 ? string.Format("Room palette {0}", p) : "Room palette";
                        _paletteBox.Items.Add(new PaletteChoice { Name = name, Palette = apals[p].Colors });
                    }
                }
            }
            catch
            {
                // Palette enumeration is best-effort; the costume's own colours always remain available.
            }

            _paletteBox.SelectedIndex = 0;
        }

        private Color[] SelectedPalette()
        {
            var choice = _paletteBox.SelectedItem as PaletteChoice;
            return choice != null ? choice.Palette : null;
        }

        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            _currentCelIndex = (e.Node != null && e.Node.Tag is int) ? (int)e.Node.Tag : -1;
            RenderCurrent();
        }

        /// <summary>Decodes and shows the selected cel with the selected palette. Tiny placeholder cels and
        /// decode failures are reported in the info label instead of leaving a mysteriously blank panel.</summary>
        private void RenderCurrent()
        {
            ClearImage();
            _exportButton.Enabled = false;
            if (_akos == null || _currentCelIndex < 0)
            {
                _info.Text = _codecInfo;
                return;
            }

            Size sz = AkosImageDecoder.GetCelSize(_akos, _currentCelIndex);
            string celInfo = string.Format("{0}  |  Cel {1}: {2}x{3}", _codecInfo, _currentCelIndex, sz.Width, sz.Height);
            if (sz.Width * sz.Height <= 4)
            {
                _info.Text = celInfo + " - empty placeholder cel (unused animation slot)";
                return;
            }

            try
            {
                Bitmap cel = AkosImageDecoder.DecodeCel(_akos, _currentCelIndex, SelectedPalette());
                if (cel == null)
                {
                    _info.Text = celInfo + " - not decodable (unsupported codec)";
                    return;
                }
                _picture.Image = cel;
                _exportButton.Enabled = true;
                _info.Text = celInfo;
            }
            catch (Exception ex)
            {
                _info.Text = celInfo + " - decode failed: " + ex.Message;
            }
        }

        private void ExportClick(object sender, EventArgs e)
        {
            if (_akos == null || _currentCelIndex < 0) return;

            try
            {
                using (Bitmap cel = AkosImageDecoder.DecodeCel(_akos, _currentCelIndex, SelectedPalette()))
                {
                    if (cel == null) return;
                    using (var dialog = new SaveFileDialog
                    {
                        Filter = "PNG Files|*.png",
                        FileName = string.Format("Costume Cel#{0}.png", _currentCelIndex)
                    })
                    {
                        if (dialog.ShowDialog() != DialogResult.OK) return;
                        cel.Save(dialog.FileName, ImageFormat.Png);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>Disposes the current preview bitmap (a fresh GDI bitmap is decoded per cel/palette).</summary>
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
