using System;
using System.IO;
using System.Media;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Structures;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for the speech/effects container (MONSTER.SOU / "game".SOU): lists every
    /// entry with its offset, size, sample rate and duration, and decodes the Creative VOC
    /// audio to WAV (play, export one, export all).
    /// </summary>
    public partial class SpeechSouControl : UserControl
    {
        private SpeechSouFile _file;
        private SoundPlayer _player;

        public SpeechSouControl()
        {
            InitializeComponent();
        }

        public void SetData(SpeechSouFile file)
        {
            _file = file;

            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                _file.EnsureParsed();
            }
            finally
            {
                Cursor.Current = previousCursor;
            }

            string name = Path.GetFileName(_file.FilePath);
            info.Text = string.Format("{0}  -  {1} entries, {2:0.0} MB",
                name, _file.Entries.Count, _file.FileLength / 1048576.0);
            if (_file.ParseError != null)
            {
                info.Text += "   (parse stopped: " + _file.ParseError + ")";
            }

            grid.Columns.Clear();
            grid.Rows.Clear();
            AddColumns("#", "Offset", "VOC bytes", "Sample rate", "Duration", "Lip-sync points");

            foreach (SpeechSouEntry entry in _file.Entries)
            {
                grid.Rows.Add(entry.Index,
                    "0x" + entry.Offset.ToString("X8"),
                    entry.VocLength,
                    entry.SampleRate > 0 ? entry.SampleRate + " Hz" : "?",
                    entry.DurationSeconds.ToString("0.00") + " s",
                    entry.LipSyncCount);
            }
        }

        private void AddColumns(params string[] headers)
        {
            foreach (string header in headers)
            {
                var column = new DataGridViewTextBoxColumn
                {
                    HeaderText = header,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                    SortMode = DataGridViewColumnSortMode.NotSortable,
                    ReadOnly = true
                };
                grid.Columns.Add(column);
            }
        }

        private SpeechSouEntry SelectedEntry()
        {
            if (_file == null || grid.CurrentRow == null) return null;
            int index = grid.CurrentRow.Index;
            if (index < 0 || index >= _file.Entries.Count) return null;
            return _file.Entries[index];
        }

        private byte[] DecodeSelectedToWav(SpeechSouEntry entry)
        {
            byte[] voc = _file.ReadVocBytes(entry);
            return SoundConverter.VocToWav(voc);
        }

        private void playButton_Click(object sender, EventArgs e)
        {
            SpeechSouEntry entry = SelectedEntry();
            if (entry == null) return;

            try
            {
                byte[] wav = DecodeSelectedToWav(entry);
                if (wav == null)
                {
                    MessageBox.Show(this, "This entry uses an unsupported VOC codec and cannot be decoded.",
                        "Play", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (_player != null) _player.Stop();
                _player = new SoundPlayer(new MemoryStream(wav));
                _player.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not play the entry: " + ex.Message, "Play",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            SpeechSouEntry entry = SelectedEntry();
            if (entry == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "WAV audio (*.wav)|*.wav",
                FileName = string.Format("speech_{0:D5}.wav", entry.Index)
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                byte[] wav = DecodeSelectedToWav(entry);
                if (wav == null)
                {
                    MessageBox.Show(this, "This entry uses an unsupported VOC codec and cannot be decoded.",
                        "Export WAV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                File.WriteAllBytes(dlg.FileName, wav);
                MessageBox.Show(this, "Entry exported to:\n" + dlg.FileName, "Export WAV",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Export WAV",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void exportAllButton_Click(object sender, EventArgs e)
        {
            if (_file == null || _file.Entries.Count == 0) return;

            var dlg = new FolderBrowserDialog
            {
                Description = "Folder to save one WAV per entry (speech_NNNNN.wav)"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            int exported = 0, failed = 0;
            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                foreach (SpeechSouEntry entry in _file.Entries)
                {
                    try
                    {
                        byte[] wav = DecodeSelectedToWav(entry);
                        if (wav == null) { failed++; continue; }

                        string path = Path.Combine(dlg.SelectedPath, string.Format("speech_{0:D5}.wav", entry.Index));
                        File.WriteAllBytes(path, wav);
                        exported++;
                    }
                    catch (Exception)
                    {
                        failed++;
                    }
                }
            }
            finally
            {
                Cursor.Current = previousCursor;
            }

            MessageBox.Show(this,
                string.Format("{0} entries exported to:\n{1}{2}", exported, dlg.SelectedPath,
                    failed > 0 ? "\n\n" + failed + " entries could not be decoded." : ""),
                "Export all WAVs", MessageBoxButtons.OK,
                failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}
