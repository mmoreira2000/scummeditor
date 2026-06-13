using System;
using System.IO;
using System.Media;
using System.Windows.Forms;
using ScummEditor.Structures;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer for the ripped CD audio container (CDDA.SOU): lists the tracks from the
    /// header table and decodes them to PCM WAV (play, export one, export all).
    /// </summary>
    public partial class CdAudioSouControl : UserControl
    {
        private CdAudioSouFile _file;
        private SoundPlayer _player;

        public CdAudioSouControl()
        {
            InitializeComponent();
        }

        public void SetData(CdAudioSouFile file)
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

            if (_file.ParseError != null)
            {
                info.Text = Path.GetFileName(_file.FilePath) + "  -  " + _file.ParseError;
            }
            else
            {
                info.Text = string.Format("{0}  -  {1} CD audio tracks, 44100 Hz 16-bit stereo, total {2}",
                    Path.GetFileName(_file.FilePath), _file.Tracks.Count, FormatDuration(_file.TotalDurationSeconds));
            }

            grid.Columns.Clear();
            grid.Rows.Clear();
            AddColumns("Track", "Offset", "Size", "Duration", "CD start frame");

            foreach (CdAudioTrack track in _file.Tracks)
            {
                grid.Rows.Add(track.Number,
                    "0x" + track.Offset.ToString("X8"),
                    (track.ByteLength / 1048576.0).ToString("0.0") + " MB",
                    FormatDuration(track.DurationSeconds),
                    track.StartFrame);
            }
        }

        private static string FormatDuration(double seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            return string.Format("{0}:{1:D2}", (int)time.TotalMinutes, time.Seconds);
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

        private CdAudioTrack SelectedTrack()
        {
            if (_file == null || grid.CurrentRow == null) return null;
            int index = grid.CurrentRow.Index;
            if (index < 0 || index >= _file.Tracks.Count) return null;
            return _file.Tracks[index];
        }

        private void playButton_Click(object sender, EventArgs e)
        {
            CdAudioTrack track = SelectedTrack();
            if (track == null) return;

            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                var wav = new MemoryStream();
                _file.WriteTrackWav(track, wav);
                wav.Position = 0;

                if (_player != null) _player.Stop();
                _player = new SoundPlayer(wav);
                _player.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not play the track: " + ex.Message, "Play",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = previousCursor;
            }
        }

        private void exportButton_Click(object sender, EventArgs e)
        {
            CdAudioTrack track = SelectedTrack();
            if (track == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "WAV audio (*.wav)|*.wav",
                FileName = string.Format("track_{0:D2}.wav", track.Number)
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                _file.ExportTrackToWav(track, dlg.FileName);
                MessageBox.Show(this, "Track exported to:\n" + dlg.FileName, "Export WAV",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Export failed: " + ex.Message, "Export WAV",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor.Current = previousCursor;
            }
        }

        private void exportAllButton_Click(object sender, EventArgs e)
        {
            if (_file == null || _file.Tracks.Count == 0) return;

            var dlg = new FolderBrowserDialog
            {
                Description = "Folder to save one WAV per track (track_NN.wav)"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            int exported = 0, failed = 0;
            Cursor previousCursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                foreach (CdAudioTrack track in _file.Tracks)
                {
                    try
                    {
                        string path = Path.Combine(dlg.SelectedPath, string.Format("track_{0:D2}.wav", track.Number));
                        _file.ExportTrackToWav(track, path);
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
                string.Format("{0} tracks exported to:\n{1}{2}", exported, dlg.SelectedPath,
                    failed > 0 ? "\n\n" + failed + " tracks failed." : ""),
                "Export all tracks", MessageBoxButtons.OK,
                failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
    }
}
