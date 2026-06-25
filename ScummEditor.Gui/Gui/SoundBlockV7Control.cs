using System;
using System.IO;
using System.Media;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer/player for a SCUMM v7 SOUN block (The Dig, Full Throttle). The Dig stores an iMUS digital
    /// resource (decoded by ImuseAudioDecoder: MAP/FRMT/DATA, 8/12-bit PCM -> WAV); Full Throttle stores a
    /// Creative Voice File (decoded by the existing SoundConverter.VocToWav). The decoded PCM is played via
    /// System.Media.SoundPlayer and can be exported as WAV; the raw resource can also be exported verbatim.
    /// Sound import stays deferred, matching v4-v6.
    /// </summary>
    public class SoundBlockV7Control : BlockBaseControl
    {
        private readonly Label _info;
        private readonly Button _playButton;
        private readonly Button _stopButton;
        private readonly Button _exportWavButton;
        private readonly Button _exportRawButton;

        private byte[] _wav;          // decoded PCM WAV, or null when the codec is not decodable
        private byte[] _raw;          // the raw resource bytes (iMUS / VOC), for verbatim export
        private string _kind = string.Empty;
        private string _rawExtension = ".bin";
        private SoundPlayer _player;

        public SoundBlockV7Control()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 64 };
            _playButton = new Button { Text = "Play", Width = 80, Left = 3, Top = 3, Enabled = false };
            _playButton.Click += (s, e) => PlayClick();
            _stopButton = new Button { Text = "Stop", Width = 80, Left = 87, Top = 3, Enabled = false };
            _stopButton.Click += (s, e) => StopPlayback();
            _exportWavButton = new Button { Text = "Export WAV", Width = 100, Left = 171, Top = 3, Enabled = false };
            _exportWavButton.Click += (s, e) => ExportWavClick();
            _exportRawButton = new Button { Text = "Export raw", Width = 100, Left = 275, Top = 3, Enabled = false };
            _exportRawButton.Click += (s, e) => ExportRawClick();
            _info = new Label { Left = 3, Top = 36, AutoSize = true, Text = string.Empty };

            bar.Controls.Add(_playButton);
            bar.Controls.Add(_stopButton);
            bar.Controls.Add(_exportWavButton);
            bar.Controls.Add(_exportRawButton);
            bar.Controls.Add(_info);
            Controls.Add(bar);
            bar.BringToFront();
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);
            StopPlayback();

            _wav = null;
            _raw = null;
            _kind = string.Empty;
            _rawExtension = ".bin";
            _playButton.Enabled = false;
            _stopButton.Enabled = false;
            _exportWavButton.Enabled = false;
            _exportRawButton.Enabled = false;
            _info.Text = string.Empty;
            if (blockBase == null) return;

            byte[] body = Serialize(blockBase);

            int voc = IndexOf(body, "Creative Voice File");
            if (voc >= 0)
            {
                _kind = "Creative Voice File (VOC)";
                _rawExtension = ".voc";
                _raw = Slice(body, voc);
                _wav = SafeVocToWav(_raw);
            }
            else if (ImuseAudioDecoder.IsImus(body))
            {
                int imus = IndexOf(body, "iMUS");
                _raw = imus >= 0 ? Slice(body, imus) : body;
                _rawExtension = ".imus";
                ImuseAudioDecoder.ImuseInfo info = ImuseAudioDecoder.GetInfo(body);
                _kind = info != null
                    ? string.Format("iMUSE PCM ({0}-bit, {1} Hz, {2} ch)", info.WordSize, info.SampleRate, info.Channels)
                    : "iMUSE";
                _wav = ImuseAudioDecoder.ToWav(body);
            }
            else
            {
                _kind = "unrecognised sound";
                _raw = body;
            }

            _exportRawButton.Enabled = _raw != null && _raw.Length > 0;
            if (_wav != null)
            {
                _playButton.Enabled = true;
                _exportWavButton.Enabled = true;
                _info.Text = _kind + "  -  " + _raw.Length + " bytes";
            }
            else
            {
                _info.Text = _kind + "  -  " + (_raw != null ? _raw.Length : 0) + " bytes (not decodable to WAV; export raw)";
            }
        }

        private void PlayClick()
        {
            if (_wav == null) return;
            try
            {
                StopPlayback();
                _player = new SoundPlayer(new MemoryStream(_wav));
                _player.Play();
                _stopButton.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Play failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _stopButton.Enabled = false;
        }

        private void ExportWavClick()
        {
            if (_wav == null) return;
            using (var dialog = new SaveFileDialog { Filter = "WAV audio|*.wav", FileName = "sound.wav" })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try { File.WriteAllBytes(dialog.FileName, _wav); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private void ExportRawClick()
        {
            if (_raw == null) return;
            using (var dialog = new SaveFileDialog
            {
                Filter = "Sound resource|*" + _rawExtension + "|All files|*.*",
                FileName = "sound" + _rawExtension
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;
                try { File.WriteAllBytes(dialog.FileName, _raw); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        private static byte[] SafeVocToWav(byte[] voc)
        {
            try { return SoundConverter.VocToWav(voc); }
            catch { return null; }
        }

        private static byte[] Serialize(BlockBase block)
        {
            using (var ms = new MemoryStream())
            {
                block.SaveToBinaryWriter(ms);
                return ms.ToArray();
            }
        }

        private static byte[] Slice(byte[] data, int start)
        {
            var result = new byte[data.Length - start];
            Array.Copy(data, start, result, 0, result.Length);
            return result;
        }

        private static int IndexOf(byte[] data, string text)
        {
            for (int i = 0; i + text.Length <= data.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < text.Length; j++)
                {
                    if (data[i + j] != text[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
