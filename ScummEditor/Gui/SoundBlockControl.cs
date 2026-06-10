using System;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer/player for SOUN blocks. Lists the embedded sound resources and can export
    /// them and play the ones a modern machine can render directly: Standard MIDI (through
    /// the Windows MIDI synth) and digital VOC sound effects (decoded to PCM). AdLib/Roland
    /// FM data can only be exported; "Play" on those falls back to the General MIDI version
    /// of the same track when the block contains one.
    /// </summary>
    public partial class SoundBlockControl : BlockBaseControl
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr callback);

        private const string MciAlias = "scummEditorMidi";

        private SoundBlock _block;
        private SoundPlayer _soundPlayer;
        private bool _midiOpen;
        private string _tempMidiPath;

        public SoundBlockControl()
        {
            InitializeComponent();

            btnPlay.Click += (s, e) => PlaySelected();
            btnStop.Click += (s, e) => StopPlayback();
            btnExport.Click += (s, e) => ExportSelected();
            btnExportAll.Click += (s, e) => ExportAll();
            grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) PlaySelected(); };
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);
            StopPlayback();

            _block = (SoundBlock)blockBase;

            grid.Columns.Clear();
            grid.Rows.Clear();
            AddColumn("Path");
            AddColumn("Type");
            AddColumn("Size");
            AddColumn("Kind");

            foreach (SoundResource resource in _block.Resources)
            {
                grid.Rows.Add(resource.Path, resource.Type, resource.Data.Length, KindLabel(resource.Data));
            }

            lblStatus.Text = string.Format("{0} resource(s)", _block.Resources.Count);
        }

        private void AddColumn(string header)
        {
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            });
        }

        private static string KindLabel(byte[] data)
        {
            switch (SoundConverter.Classify(data))
            {
                case SoundConverter.SoundKind.StandardMidi: return "MIDI (playable)";
                case SoundConverter.SoundKind.Voc: return "Digital VOC (playable)";
                default: return "raw (export only)";
            }
        }

        private SoundResource SelectedResource()
        {
            if (_block == null || _block.Resources.Count == 0) return null;
            if (grid.CurrentRow == null) return null;
            int index = grid.CurrentRow.Index;
            if (index < 0 || index >= _block.Resources.Count) return null;
            return _block.Resources[index];
        }

        private void PlaySelected()
        {
            SoundResource resource = SelectedResource();
            if (resource == null) return;

            StopPlayback();

            try
            {
                switch (SoundConverter.Classify(resource.Data))
                {
                    case SoundConverter.SoundKind.StandardMidi:
                        PlayMidi(SoundConverter.ExtractMidi(resource.Data));
                        lblStatus.Text = "Playing MIDI: " + resource.Path;
                        break;

                    case SoundConverter.SoundKind.Voc:
                        PlayVoc(resource.Data, resource.Path);
                        break;

                    default:
                        PlayFallbackMidi(resource);
                        break;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Playback failed: " + ex.Message;
            }
        }

        // For AdLib/Roland (or anything we can't synthesise), play the General MIDI version
        // of the same track if the block has one; otherwise say so.
        private void PlayFallbackMidi(SoundResource selected)
        {
            foreach (SoundResource other in _block.Resources)
            {
                if (other == selected) continue;
                if (SoundConverter.Classify(other.Data) == SoundConverter.SoundKind.StandardMidi)
                {
                    PlayMidi(SoundConverter.ExtractMidi(other.Data));
                    lblStatus.Text = string.Format("{0} can't be synthesised here; playing the MIDI version ({1}).",
                        selected.Type, other.Path);
                    return;
                }
            }
            lblStatus.Text = string.Format("{0} is AdLib/Roland FM data - export it and play in an OPL2/MT-32 emulator.",
                selected.Type);
        }

        private void PlayVoc(byte[] data, string path)
        {
            byte[] wav = SoundConverter.VocToWav(data);
            if (wav == null)
            {
                lblStatus.Text = "VOC uses an unsupported codec (e.g. ADPCM); export the raw .voc instead.";
                return;
            }

            _soundPlayer = new SoundPlayer(new MemoryStream(wav));
            _soundPlayer.Play();
            lblStatus.Text = "Playing VOC: " + path;
        }

        private void PlayMidi(byte[] midi)
        {
            if (midi == null || midi.Length == 0)
            {
                lblStatus.Text = "No MIDI data found.";
                return;
            }

            _tempMidiPath = Path.Combine(Path.GetTempPath(), "scummeditor_play.mid");
            File.WriteAllBytes(_tempMidiPath, midi);

            mciSendString("open \"" + _tempMidiPath + "\" type sequencer alias " + MciAlias, null, 0, IntPtr.Zero);
            mciSendString("play " + MciAlias, null, 0, IntPtr.Zero);
            _midiOpen = true;
        }

        private void StopPlayback()
        {
            if (_soundPlayer != null)
            {
                try { _soundPlayer.Stop(); } catch { }
                _soundPlayer.Dispose();
                _soundPlayer = null;
            }

            if (_midiOpen)
            {
                mciSendString("stop " + MciAlias, null, 0, IntPtr.Zero);
                mciSendString("close " + MciAlias, null, 0, IntPtr.Zero);
                _midiOpen = false;
            }
        }

        private void ExportSelected()
        {
            SoundResource resource = SelectedResource();
            if (resource == null) return;

            byte[] outBytes;
            string ext;
            GetExportBytes(resource.Data, out outBytes, out ext);

            using (var dlg = new SaveFileDialog())
            {
                dlg.FileName = SanitizeFileName(resource.Path) + ext;
                dlg.Filter = "Sound file|*" + ext + "|All files|*.*";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                File.WriteAllBytes(dlg.FileName, outBytes);
            }
            lblStatus.Text = "Exported " + resource.Path;
        }

        private void ExportAll()
        {
            if (_block == null || _block.Resources.Count == 0) return;

            using (var dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                int count = 0;
                for (int i = 0; i < _block.Resources.Count; i++)
                {
                    SoundResource resource = _block.Resources[i];
                    byte[] outBytes;
                    string ext;
                    GetExportBytes(resource.Data, out outBytes, out ext);

                    string name = string.Format("{0}_{1}{2}", i, SanitizeFileName(resource.Path), ext);
                    File.WriteAllBytes(Path.Combine(dlg.SelectedPath, name), outBytes);
                    count++;
                }
                lblStatus.Text = string.Format("Exported {0} file(s).", count);
            }
        }

        // Picks the most useful representation: extracted .mid for MIDI, decoded .wav for VOC
        // (raw .voc if the codec is unsupported), raw .bin otherwise.
        private void GetExportBytes(byte[] data, out byte[] outBytes, out string ext)
        {
            switch (SoundConverter.Classify(data))
            {
                case SoundConverter.SoundKind.StandardMidi:
                    outBytes = SoundConverter.ExtractMidi(data) ?? data;
                    ext = ".mid";
                    return;

                case SoundConverter.SoundKind.Voc:
                    byte[] wav = SoundConverter.VocToWav(data);
                    if (wav != null) { outBytes = wav; ext = ".wav"; }
                    else { outBytes = data; ext = ".voc"; }
                    return;

                default:
                    outBytes = data;
                    ext = ".bin";
                    return;
            }
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
