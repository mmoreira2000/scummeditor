using System;
using System.IO;
using System.Media;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for a SCUMM v7 external iMUSE sound bundle (The Dig's DIGMUSIC.BUN / DIGVOICE.BUN): lists
    /// every named entry and, on demand, decompresses the selected one (ImuseBundleDecoder -> iMUS ->
    /// ImuseAudioDecoder) to a PCM WAV it can play (System.Media.SoundPlayer) or export (one, or all). The
    /// bundle is parsed lazily and entries are read on demand, so the 130-260 MB file is not loaded whole.
    /// </summary>
    public class ImuseBundleControl : UserControl
    {
        private readonly Label _info;
        private readonly DataGridView _grid;
        private readonly Button _playButton;
        private readonly Button _stopButton;
        private readonly Button _exportButton;
        private readonly Button _exportAllButton;

        private ImuseBundleFile _bundle;
        private SoundPlayer _player;

        public ImuseBundleControl()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 30 };
            _playButton = new Button { Text = "Play", Width = 70, Left = 3, Top = 3 };
            _playButton.Click += (s, e) => PlaySelected();
            _stopButton = new Button { Text = "Stop", Width = 70, Left = 76, Top = 3 };
            _stopButton.Click += (s, e) => StopPlayback();
            _exportButton = new Button { Text = "Export WAV", Width = 90, Left = 149, Top = 3 };
            _exportButton.Click += (s, e) => ExportSelected();
            _exportAllButton = new Button { Text = "Export all", Width = 90, Left = 242, Top = 3 };
            _exportAllButton.Click += (s, e) => ExportAll();
            bar.Controls.Add(_playButton);
            bar.Controls.Add(_stopButton);
            bar.Controls.Add(_exportButton);
            bar.Controls.Add(_exportAllButton);

            _info = new Label { Dock = DockStyle.Top, Height = 22, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells
            };
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) PlaySelected(); };

            Controls.Add(_grid);
            Controls.Add(_info);
            Controls.Add(bar);
        }

        public void SetData(ImuseBundleFile bundle)
        {
            StopPlayback();
            _bundle = bundle;

            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { _bundle.EnsureParsed(); }
            finally { Cursor.Current = previous; }

            _info.Text = string.Format("{0}  -  {1} entries{2}",
                Path.GetFileName(_bundle.FilePath), _bundle.Entries.Count,
                _bundle.IsValid ? string.Empty : "  (could not read the bundle)");

            _grid.Columns.Clear();
            _grid.Rows.Clear();
            AddColumn("#");
            AddColumn("Name");
            AddColumn("Bytes");
            for (int i = 0; i < _bundle.Entries.Count; i++)
            {
                ImuseBundleEntry e = _bundle.Entries[i];
                _grid.Rows.Add(i, e.Name, e.Size);
            }
        }

        private void AddColumn(string header)
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            });
        }

        private int SelectedIndex()
        {
            if (_bundle == null || _grid.CurrentRow == null) return -1;
            int i = _grid.CurrentRow.Index;
            return (i >= 0 && i < _bundle.Entries.Count) ? i : -1;
        }

        private byte[] DecodeToWav(int index)
        {
            byte[] raw = _bundle.ReadEntryRaw(index);
            return raw == null ? null : ImuseBundleDecoder.ToWav(raw);
        }

        private void PlaySelected()
        {
            int i = SelectedIndex();
            if (i < 0) return;
            try
            {
                byte[] wav = DecodeToWav(i);
                if (wav == null)
                {
                    MessageBox.Show(this, "This entry could not be decoded (unsupported codec).",
                        "Play", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                StopPlayback();
                _player = new SoundPlayer(new MemoryStream(wav));
                _player.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not play the entry: " + ex.Message, "Play",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopPlayback()
        {
            if (_player != null)
            {
                try { _player.Stop(); } catch { /* nothing to stop */ }
                _player.Dispose();
                _player = null;
            }
        }

        private void ExportSelected()
        {
            int i = SelectedIndex();
            if (i < 0) return;

            using (var dlg = new SaveFileDialog
            {
                Filter = "WAV audio (*.wav)|*.wav",
                FileName = SafeName(_bundle.Entries[i].Name) + ".wav"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    byte[] wav = DecodeToWav(i);
                    if (wav == null)
                    {
                        MessageBox.Show(this, "This entry could not be decoded.", "Export WAV",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    File.WriteAllBytes(dlg.FileName, wav);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Export failed: " + ex.Message, "Export WAV",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportAll()
        {
            if (_bundle == null || _bundle.Entries.Count == 0) return;
            using (var dlg = new FolderBrowserDialog { Description = "Folder to save one WAV per bundle entry" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                int exported = 0, failed = 0;
                Cursor previous = Cursor.Current;
                Cursor.Current = Cursors.WaitCursor;
                try
                {
                    for (int i = 0; i < _bundle.Entries.Count; i++)
                    {
                        try
                        {
                            byte[] wav = DecodeToWav(i);
                            if (wav == null) { failed++; continue; }
                            string path = Path.Combine(dlg.SelectedPath,
                                string.Format("{0:D4}_{1}.wav", i, SafeName(_bundle.Entries[i].Name)));
                            File.WriteAllBytes(path, wav);
                            exported++;
                        }
                        catch (Exception) { failed++; }
                    }
                }
                finally { Cursor.Current = previous; }

                MessageBox.Show(this,
                    string.Format("{0} entries exported to:\n{1}{2}", exported, dlg.SelectedPath,
                        failed > 0 ? "\n\n" + failed + " entries could not be decoded." : ""),
                    "Export all", MessageBoxButtons.OK,
                    failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
        }

        private static string SafeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
