using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer/editor for a SCUMM v7 external localized-text file - The Dig's LANGUAGE.BND (translated
    /// in-game dialogue) or a .TRS subtitle/UI file. Lists every string (Key + a one-line preview) in a
    /// grid; the selected string's full text is editable in the box below. Edits update the in-memory
    /// entries, which the global "Save changes" writes back (byte-identical when nothing changed). Export
    /// dumps the strings as KEY&lt;TAB&gt;TEXT for editing in an external (code-page-aware) editor; Import
    /// applies an edited dump.
    ///
    /// Text is stored byte-faithfully (Latin-1), but the grid and editor DISPLAY it through the edition's
    /// code page (set by SetData - CP850 for the DOS-era v1-v7 Western editions, Windows-1252 for v8/COMI
    /// which is a Windows-95 game, both including Portuguese), so its accents render correctly and an edit is
    /// re-encoded back to those exact bytes. The double-byte CJK editions pass code page 0, stay raw, and the
    /// box is view-only. The Export dump is the raw code-page bytes, for editing in an external code-page-aware
    /// editor.
    /// </summary>
    public class LocalizedTextControl : UserControl
    {
        private readonly Label _info;
        private readonly DataGridView _grid;
        private readonly TextBox _detail;
        private readonly Button _exportButton;
        private readonly Button _importButton;

        private ILocalizedTextFile _file;
        private LocalizedTextEntry _current;
        private bool _loadingDetail;
        private int _codePage; // DOS code page for display (0 = show the raw bytes, e.g. CJK)

        public LocalizedTextControl()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 30 };
            _exportButton = new Button { Text = "Export text", Width = 100, Left = 3, Top = 3 };
            _exportButton.Click += (s, e) => ExportClick();
            _importButton = new Button { Text = "Import text", Width = 100, Left = 106, Top = 3 };
            _importButton.Click += (s, e) => ImportClick();
            bar.Controls.Add(_exportButton);
            bar.Controls.Add(_importButton);

            _info = new Label { Dock = DockStyle.Top, Height = 22, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            _grid.SelectionChanged += (s, e) => LoadDetail();
            split.Panel1.Controls.Add(_grid);

            _detail = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                AcceptsReturn = true,
                AcceptsTab = false,
                Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, 9f)
            };
            _detail.TextChanged += (s, e) => CommitDetail();
            split.Panel2.Controls.Add(_detail);

            Controls.Add(split);
            Controls.Add(_info);
            Controls.Add(bar);

            HandleCreated += (s, e) => { try { split.SplitterDistance = (int)(split.Height * 0.6); } catch { } };
        }

        public void SetData(ILocalizedTextFile file, int codePage)
        {
            _file = file;
            _codePage = codePage;
            _current = null;
            _detail.Clear();

            _grid.Columns.Clear();
            _grid.Rows.Clear();
            AddColumn("Key", 220);
            AddColumn("Text", 600);

            if (_file == null)
            {
                _info.Text = "(no file)";
                _exportButton.Enabled = _importButton.Enabled = false;
                return;
            }

            // Without a single-byte code page (CJK / unknown) the text is shown as raw bytes that cannot be
            // edited in place without corrupting the double-byte data, so the box is view-only there - the
            // Export/Import buttons (raw bytes) remain the way to translate those editions.
            _detail.ReadOnly = _codePage == 0;
            string viewOnly = _detail.ReadOnly && _file.IsValid ? "  (view only - use Export/Import to edit)" : string.Empty;
            _info.Text = string.Format("{0}  -  {1} strings{2}{3}", _file.FileName, _file.Entries.Count,
                _file.IsValid ? string.Empty : "  (no editable strings found)", viewOnly);
            _exportButton.Enabled = _importButton.Enabled = _file.IsValid;

            foreach (LocalizedTextEntry e in _file.Entries)
            {
                _grid.Rows.Add(e.Key, Preview(Display(e.Text)));
            }
            if (_grid.Rows.Count > 0) _grid.Rows[0].Selected = true;
            LoadDetail();
        }

        private void AddColumn(string header, int width)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            });
        }

        private LocalizedTextEntry SelectedEntry()
        {
            if (_file == null || _grid.CurrentRow == null) return null;
            int i = _grid.CurrentRow.Index;
            return (i >= 0 && i < _file.Entries.Count) ? _file.Entries[i] : null;
        }

        private void LoadDetail()
        {
            LocalizedTextEntry entry = SelectedEntry();
            if (entry == _current) return;
            _current = entry;
            _loadingDetail = true;
            _detail.Text = entry != null ? Display(entry.Text) : string.Empty;
            _loadingDetail = false;
        }

        private void CommitDetail()
        {
            if (_loadingDetail || _current == null) return;
            // Re-encode the edited display text to the game's code page bytes, then let the file normalise
            // line endings to its own structure (a .TRS keeps its native ending - LF for the Mac edition; a
            // LANGUAGE.BND collapses it to a space). Windows Forms forces CRLF in the TextBox.
            _current.Text = _file.NormalizeEditedText(DosCodePageText.FromDisplay(_detail.Text, _codePage));
            if (_grid.CurrentRow != null && _grid.CurrentRow.Index >= 0)
            {
                _grid.CurrentRow.Cells[1].Value = Preview(_detail.Text);
            }
        }

        private void ExportClick()
        {
            if (_file == null) return;
            using (var dlg = new SaveFileDialog
            {
                Filter = "Text (*.txt)|*.txt|All files|*.*",
                FileName = Path.GetFileNameWithoutExtension(_file.FileName) + ".txt"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dlg.FileName, _file.ExportToText(), Encoding.Latin1);
                    string cpHint = _codePage == 0
                        ? "The file is in the game's native (multi-byte) encoding - open it with that encoding."
                        : "The file is in code page CP" + _codePage + " (Western European) - open it as CP" + _codePage + ".";
                    MessageBox.Show(this, "Strings exported to:\n" + dlg.FileName +
                        "\n\nEdit the text after each tab. " + cpHint + " Then Import text.",
                        "Export text", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Export failed: " + ex.Message, "Export text", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ImportClick()
        {
            if (_file == null) return;
            using (var dlg = new OpenFileDialog { Filter = "Text (*.txt)|*.txt|All files|*.*" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string report = _file.ImportFromText(File.ReadAllText(dlg.FileName, Encoding.Latin1));
                    SetData(_file, _codePage); // refresh the grid from the updated entries
                    MessageBox.Show(this, report + "\n\nUse \"Save changes\" to write it back to the game file.",
                        "Import text", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Import failed: " + ex.Message, "Import text", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>The byte-faithful entry text shown through the edition's DOS code page (raw when code page 0).</summary>
        private string Display(string text)
        {
            return DosCodePageText.ToDisplay(text, _codePage);
        }

        private static string Preview(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            int nl = text.IndexOfAny(new[] { '\r', '\n' });
            string line = nl >= 0 ? text.Substring(0, nl) : text;
            return line.Length > 120 ? line.Substring(0, 120) + " ..." : line + (nl >= 0 ? " ..." : string.Empty);
        }
    }
}
