using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
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
    /// Text is byte-faithful (Latin-1): accented bytes are the game's DOS code page, so high bytes render
    /// raw here - for accent-correct editing use Export/Import with a CP850/CP860-aware editor. (A future
    /// refinement can apply the game's charmap for in-place display.)
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

        public void SetData(ILocalizedTextFile file)
        {
            _file = file;
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

            _info.Text = string.Format("{0}  -  {1} strings{2}", _file.FileName, _file.Entries.Count,
                _file.IsValid ? string.Empty : "  (no editable strings found)");
            _exportButton.Enabled = _importButton.Enabled = _file.IsValid;

            foreach (LocalizedTextEntry e in _file.Entries)
            {
                _grid.Rows.Add(e.Key, Preview(e.Text));
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
            _detail.Text = entry != null ? entry.Text : string.Empty;
            _loadingDetail = false;
        }

        private void CommitDetail()
        {
            if (_loadingDetail || _current == null) return;
            // The TextBox uses \r\n line endings, matching the DOS .TRS / .BND files.
            _current.Text = _detail.Text;
            if (_grid.CurrentRow != null && _grid.CurrentRow.Index >= 0)
            {
                _grid.CurrentRow.Cells[1].Value = Preview(_current.Text);
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
                    MessageBox.Show(this, "Strings exported to:\n" + dlg.FileName +
                        "\n\nEdit the text after each tab. The file is in the game's DOS code page (open it as " +
                        "CP850, or CP860 for Portuguese). Then Import text.",
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
                    SetData(_file); // refresh the grid from the updated entries
                    MessageBox.Show(this, report + "\n\nUse \"Save changes\" to write it back to the game file.",
                        "Import text", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Import failed: " + ex.Message, "Import text", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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
