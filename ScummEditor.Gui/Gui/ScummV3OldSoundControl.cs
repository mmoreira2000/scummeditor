using System;
using System.IO;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Gui
{
    /// <summary>Tree-node payload for a v3 old-bundle sound: enough context to view, play AND edit it.</summary>
    public class V3OldSoundRef
    {
        public ScummV3OldBundleDataFile DataFile;
        public ScummV3OldBundleIndexFile Index;
        public int RoomNo;
        public int Offset;
    }

    /// <summary>
    /// Viewer/player for a SCUMM v3 "old bundle" sound (Loom EGA, Indy3 EGA). A v3old sound is a tagless
    /// WA+AD resource located by the index SOUND directory; this shows its make-up and, for an AdLib
    /// MUSIC track, plays the embedded melody through the Windows synth (General MIDI, not the original
    /// OPL2 timbres - export for a faithful render) and exports it as a Standard MIDI File. The AdLib
    /// payload (and the whole resource) can also be exported raw for an OPL2 player / ScummVM.
    ///
    /// A standalone resource, not a BlockBase, so it is a plain UserControl driven by SetData.
    /// </summary>
    public class ScummV3OldSoundControl : UserControl
    {
        private readonly Label _status;
        private readonly Button _play;
        private readonly Button _stop;
        private readonly Button _exportMidi;
        private readonly Button _exportRaw;
        private readonly Button _importRaw;
        private readonly MidiMciPlayer _midi = new MidiMciPlayer("v3oldsound");

        private V3OldSoundRef _ref;
        private ScummV3OldSound _sound;

        public ScummV3OldSoundControl()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 58 };
            _play = new Button { Text = "Play", Width = 60, Left = 3, Top = 3 };
            _play.Click += PlayClick;
            _stop = new Button { Text = "Stop", Width = 60, Left = 67, Top = 3 };
            _stop.Click += (s, e) => { _midi.Stop(); _status.Text = "Stopped."; };
            _exportMidi = new Button { Text = "Export MIDI", Width = 90, Left = 131, Top = 3 };
            _exportMidi.Click += ExportMidiClick;
            _exportRaw = new Button { Text = "Export raw", Width = 90, Left = 225, Top = 3 };
            _exportRaw.Click += ExportRawClick;
            _importRaw = new Button { Text = "Import raw", Width = 90, Left = 319, Top = 3 };
            _importRaw.Click += ImportRawClick;
            _status = new Label { AutoSize = false, Left = 3, Top = 34, Width = 600, Height = 18 };

            topPanel.Controls.Add(_play);
            topPanel.Controls.Add(_stop);
            topPanel.Controls.Add(_exportMidi);
            topPanel.Controls.Add(_exportRaw);
            topPanel.Controls.Add(_importRaw);
            topPanel.Controls.Add(_status);
            Controls.Add(topPanel);
        }

        public void SetData(V3OldSoundRef soundRef)
        {
            _midi.Stop();
            _ref = soundRef;
            _sound = soundRef == null ? null : new ScummV3OldSound(soundRef.DataFile.RawContent, soundRef.Offset);
            ScummV3OldSound sound = _sound;
            bool music = sound != null && sound.IsMusic;
            _play.Enabled = music;
            _exportMidi.Enabled = music;
            _exportRaw.Enabled = sound != null && sound.AdLibOffset >= 0;
            _importRaw.Enabled = sound != null && sound.AdLibOffset >= 0 && _ref != null;
            _status.Text = sound == null ? "(no sound)"
                : sound.AdLibOffset < 0 ? "No AdLib data in this sound resource."
                : (music ? "AdLib music track." : "AdLib sound effect (export raw for an OPL2 player).") + "  (" + sound.TotalSize + " bytes)";
        }

        private void PlayClick(object sender, EventArgs e)
        {
            if (_sound == null || !_sound.IsMusic) return;
            byte[] midi = ScummV4AdLibMidi.ToStandardMidi(_sound.GetAdLibPayload());
            string error;
            _status.Text = midi != null && _midi.Play(midi, out error)
                ? "Playing AdLib music as a melody preview (General MIDI; export for the real OPL2 sound)."
                : "Playback failed.";
        }

        private void ExportMidiClick(object sender, EventArgs e)
        {
            if (_sound == null || !_sound.IsMusic) return;
            byte[] midi = ScummV4AdLibMidi.ToStandardMidi(_sound.GetAdLibPayload());
            if (midi == null) { _status.Text = "Could not convert this track."; return; }
            using (var dlg = new SaveFileDialog { Filter = "MIDI file|*.mid", FileName = "sound.mid" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllBytes(dlg.FileName, midi);
            }
            _status.Text = "Exported MIDI.";
        }

        private void ExportRawClick(object sender, EventArgs e)
        {
            if (_sound == null || _sound.AdLibOffset < 0) return;
            byte[] payload = _sound.GetAdLibPayload();
            if (payload == null) return;
            using (var dlg = new SaveFileDialog { Filter = "AdLib resource|*.ad|All files|*.*", FileName = "sound.ad" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllBytes(dlg.FileName, payload);
            }
            _status.Text = "Exported raw AdLib resource.";
        }

        private void ImportRawClick(object sender, EventArgs e)
        {
            if (_ref == null || _sound == null || _sound.AdLibOffset < 0) return;
            using (var dlg = new OpenFileDialog { Filter = "AdLib resource|*.ad|All files|*.*" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                byte[] payload = File.ReadAllBytes(dlg.FileName);
                string error;
                if (!ScummV3OldGraphics.ImportRawAdLib(_ref.DataFile, _ref.Index, _ref.RoomNo, _ref.Offset, payload, out error))
                {
                    MessageBox.Show(this, "Import failed: " + error, "Import raw AdLib", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SetData(_ref); // re-parse the (now edited) resource
                MessageBox.Show(this, "AdLib resource replaced. Use \"Save changes\" to write it back to the game files.",
                    "Import raw AdLib", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
