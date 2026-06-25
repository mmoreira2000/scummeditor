using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
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
        private readonly Button _importButton;

        private BlockBase _akos;
        private int _currentCelIndex = -1;
        private string _codecInfo = string.Empty;
        private bool _splitterApplied;

        // Cache of "palettes referenced by a literal setCurrentPalette(roomN) in a script", computed once
        // per loaded game (keyed by the LECF root) - a view-only candidate source for codec-16 cels.
        private BlockBase _scriptPaletteRoot;
        private List<KeyValuePair<int, Color[]>> _scriptPalettes;

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

            // Two-row top bar: row 1 = export/import + palette selector, row 2 = codec / cel info.
            var topBar = new Panel { Dock = DockStyle.Top, Height = 54 };
            _exportButton = new Button { Text = "Export PNG", Width = 90, Left = 3, Top = 3, Enabled = false };
            _exportButton.Click += ExportClick;
            // Import re-encodes the cel back into the costume (codec 1 BYLE-RLE / codec 5 BOMP). It requires
            // an INDEXED PNG so the costume-colour indices are preserved exactly, independent of the display
            // palette (re-export from this viewer, edit without converting to RGB). Codec 16 (MAJMIN) is not
            // encodable yet, so Import is disabled for those costumes.
            _importButton = new Button { Text = "Import PNG", Width = 90, Left = 99, Top = 3, Enabled = false };
            _importButton.Click += ImportClick;
            var paletteLabel = new Label { Text = "Palette:", AutoSize = true, Left = 198, Top = 8 };
            _paletteBox = new ComboBox { Left = 249, Top = 4, Width = 240, DropDownStyle = ComboBoxStyle.DropDownList };
            _paletteBox.SelectedIndexChanged += (s, e) => RenderCurrent();
            _info = new Label { Left = 3, Top = 32, AutoSize = true, Text = string.Empty };
            topBar.Controls.Add(_exportButton);
            topBar.Controls.Add(_importButton);
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
            _importButton.Enabled = false;
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

            // Candidate palettes that scripts load via a literal setCurrentPalette(roomN): a codec-16
            // cel (no palette of its own) is often shown under the palette a cutscene loads, so offer those.
            try
            {
                var lecf = (_akos.Parent as DiskBlock)?.Parent;
                EnsureScriptPalettes(lecf);
                if (_scriptPalettes != null)
                {
                    foreach (var sp in _scriptPalettes)
                    {
                        _paletteBox.Items.Add(new PaletteChoice { Name = "Script palette: room " + sp.Key, Palette = sp.Value });
                    }
                }
            }
            catch
            {
                // best-effort; never block the viewer on the script scan.
            }

            _paletteBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Builds (once per loaded game) the list of palettes that scripts load via a literal
        /// setCurrentPalette(roomN): scan every script for the reference, then map room N to its palette
        /// through the LOFF room-offset table (room id -> ROOM offset -> that room's APAL). View-only.
        /// </summary>
        private void EnsureScriptPalettes(BlockBase lecf)
        {
            if (ReferenceEquals(lecf, _scriptPaletteRoot)) return;
            _scriptPaletteRoot = lecf;
            _scriptPalettes = new List<KeyValuePair<int, Color[]>>();
            if (lecf == null) return;

            var roomRefs = new HashSet<int>();
            CollectScriptPaletteRooms(lecf, roomRefs);
            if (roomRefs.Count == 0) return;

            var loff = lecf.Childrens.OfType<RoomOffsetTable>().FirstOrDefault();
            if (loff == null) return;

            // ROOM block offset -> that room's first APAL palette.
            var offsetToPalette = new Dictionary<long, Color[]>();
            foreach (DiskBlock disk in lecf.Childrens.OfType<DiskBlock>())
            {
                RoomBlock room = disk.GetROOM();
                if (room == null) continue;
                Color[] pal = TryGetRoomPalette(room);
                if (pal != null) offsetToPalette[room.BlockOffSet] = pal;
            }

            var added = new HashSet<int>();
            foreach (RoomOffsetTableItem item in loff.Rooms)
            {
                if (!roomRefs.Contains(item.Id) || added.Contains(item.Id)) continue;
                Color[] pal;
                if (offsetToPalette.TryGetValue(item.OffSet, out pal))
                {
                    _scriptPalettes.Add(new KeyValuePair<int, Color[]>(item.Id, pal));
                    added.Add(item.Id);
                }
            }
        }

        private static void CollectScriptPaletteRooms(BlockBase node, HashSet<int> rooms)
        {
            var script = node as ScriptBlock;
            if (script != null && script.RawContent != null)
            {
                foreach (int r in ScriptPaletteScanner.FindCurrentPaletteRooms(script.RawContent, script.CodeOffset))
                {
                    rooms.Add(r);
                }
            }
            foreach (BlockBase child in node.Childrens)
            {
                CollectScriptPaletteRooms(child, rooms);
            }
        }

        private static Color[] TryGetRoomPalette(RoomBlock room)
        {
            try { return room.GetPALS().GetWRAP().GetAPALs()[0].Colors; }
            catch { return null; }
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
            _importButton.Enabled = false;
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
                _importButton.Enabled = AkosImageEncoder.CanEncode(_akos); // codec 1/5 only
                _info.Text = celInfo + (_importButton.Enabled ? string.Empty : "  (import N/A: codec not encodable yet)");
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

        /// <summary>
        /// Re-encodes an INDEXED PNG back into the selected cel (codec 1/5) via AkosImageEncoder. An indexed
        /// PNG is required so the costume-colour indexes are preserved exactly, independent of the display
        /// palette - so the round-trip is correct even for costumes whose colours are set at runtime.
        /// </summary>
        private void ImportClick(object sender, EventArgs e)
        {
            if (_akos == null || _currentCelIndex < 0) return;
            if (!AkosImageEncoder.CanEncode(_akos))
            {
                MessageBox.Show("This costume's cel codec cannot be re-encoded yet (import supports codec 1 / 5).",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new OpenFileDialog { Filter = "PNG Files|*.png" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                int celToReselect = _currentCelIndex;
                try
                {
                    using (var bitmap = (Bitmap)Image.FromFile(dialog.FileName))
                    {
                        if (!IndexedImageHelper.IsIndexed(bitmap))
                        {
                            MessageBox.Show(
                                "The image must be an INDEXED (palette-based) PNG so the costume's colour indexes are preserved. " +
                                "Re-export this cel from the editor and edit it without converting it to RGB/truecolor.",
                                "Import", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        byte[,] indices = IndexedImageHelper.GetIndexMatrix(bitmap);
                        AkosImageEncoder.ReplaceCel(_akos, _currentCelIndex, indices);
                    }
                }
                catch (ImageEncodeException ex)
                {
                    MessageBox.Show(ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Re-parse the costume (the AKCD/AKOF changed) and keep the user on the edited cel.
                SetAndRefreshData(_akos);
                if (celToReselect < _tree.Nodes.Count) _tree.SelectedNode = _tree.Nodes[celToReselect];

                MessageBox.Show("Cel imported. Use \"Save changes\" to write it back to the game files.",
                    "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
