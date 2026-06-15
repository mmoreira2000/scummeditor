using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Viewer/player for a SCUMM v4 "SO" sound block. Lists its WA/AD (and nested SO) sub-blocks with
    /// tag, kind, offset and size, and can:
    ///   - Play an AdLib MUSIC track as a melody PREVIEW: the embedded MIDI track is extracted and
    ///     played through the Windows synth (General MIDI instruments, not the original OPL2 timbres -
    ///     export for a faithful render). SFX and WA/Roland resources are FM/waveform streams the
    ///     editor cannot synthesise, so for those Play just points to export.
    ///   - Export the raw bytes of one sub-block, or all of them at once, for an OPL2/MT-32 player
    ///     (or ScummVM).
    /// The block itself is kept verbatim and round-trips byte-for-byte.
    /// </summary>
    public class SoundBlockV4Control : BlockBaseControl
    {
        private readonly DataGridView _grid;
        private readonly Label _status;
        private readonly Button _play;
        private readonly Button _stop;
        private readonly Button _export;
        private readonly Button _exportAll;
        private readonly MidiMciPlayer _midi = new MidiMciPlayer("v4sound");

        private SoundBlockV4 _sound;
        private readonly List<SoundSubBlockV4> _rowSubs = new List<SoundSubBlockV4>();

        public SoundBlockV4Control()
        {
            var topPanel = new Panel { Dock = DockStyle.Top, Height = 58 };

            _play = new Button { Text = "Play", Width = 60, Left = 3, Top = 3 };
            _play.Click += PlayClick;
            _stop = new Button { Text = "Stop", Width = 60, Left = 67, Top = 3 };
            _stop.Click += StopClick;
            _export = new Button { Text = "Export", Width = 70, Left = 131, Top = 3 };
            _export.Click += ExportClick;
            _exportAll = new Button { Text = "Export All", Width = 80, Left = 205, Top = 3 };
            _exportAll.Click += ExportAllClick;

            _status = new Label { AutoSize = false, Left = 3, Top = 34, Width = 600, Height = 18 };

            topPanel.Controls.Add(_play);
            topPanel.Controls.Add(_stop);
            topPanel.Controls.Add(_export);
            topPanel.Controls.Add(_exportAll);
            topPanel.Controls.Add(_status);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            _grid.Columns.Add("tag", "Sub-block");
            _grid.Columns.Add("kind", "Kind");
            _grid.Columns.Add("offset", "Offset");
            _grid.Columns.Add("size", "Size");
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) PlayClick(s, e); };

            // Add the Fill grid first, then the Top panel, into the base control's Contents panel
            // (below the BlockType/Offset/Size header) so nothing overlaps that header.
            Contents.Controls.Add(_grid);
            Contents.Controls.Add(topPanel);
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);
            _midi.Stop();

            _grid.Rows.Clear();
            _rowSubs.Clear();
            _sound = blockBase as SoundBlockV4;
            if (_sound == null) { _status.Text = string.Empty; return; }

            int wa = 0, ad = 0, so = 0;
            AddRows(_sound.SubBlocks, 0, ref wa, ref ad, ref so);
            _status.Text = string.Format("v4 sound: {0} WA, {1} AD, {2} nested SO   ({3} bytes)", wa, ad, so, _sound.RawContent.Length);
        }

        private void AddRows(List<SoundSubBlockV4> subBlocks, int depth, ref int wa, ref int ad, ref int so)
        {
            if (subBlocks == null) return;
            foreach (SoundSubBlockV4 sub in subBlocks)
            {
                if (sub.Tag == "WA") wa++;
                else if (sub.Tag == "AD") ad++;
                else if (sub.Tag == "SO") so++;

                _grid.Rows.Add(new string(' ', depth * 2) + sub.Tag, sub.Kind, "0x" + sub.Offset.ToString("X4"), sub.Size);
                _rowSubs.Add(sub); // row -> sub-block, kept in sync with the grid
                AddRows(sub.Children, depth + 1, ref wa, ref ad, ref so);
            }
        }

        private SoundSubBlockV4 SelectedSub()
        {
            if (_sound == null || _grid.CurrentRow == null) return null;
            int index = _grid.CurrentRow.Index;
            return (index >= 0 && index < _rowSubs.Count) ? _rowSubs[index] : null;
        }

        /// <summary>The sub-block's payload bytes (everything after its 6-byte small header).</summary>
        private byte[] Payload(SoundSubBlockV4 sub)
        {
            int start = sub.Offset + 6;
            int length = sub.Size - 6;
            if (start < 0 || length <= 0 || start + length > _sound.RawContent.Length) return new byte[0];
            var payload = new byte[length];
            Array.Copy(_sound.RawContent, start, payload, 0, length);
            return payload;
        }

        private void PlayClick(object sender, EventArgs e)
        {
            SoundSubBlockV4 sub = SelectedSub();
            if (sub == null) return;

            if (sub.Tag == "AD")
            {
                byte[] midi = ScummV4AdLibMidi.ToStandardMidi(Payload(sub));
                if (midi != null)
                {
                    string error;
                    if (_midi.Play(midi, out error))
                    {
                        _status.Text = "Playing AdLib music as a melody preview (General MIDI instruments; export for the real OPL2 sound).";
                    }
                    else
                    {
                        _status.Text = "Playback failed: " + error;
                    }
                    return;
                }
            }

            _status.Text = "This is a raw AdLib SFX / Roland / waveform stream - export it and play it in an OPL2/MT-32 player or ScummVM.";
        }

        private void StopClick(object sender, EventArgs e)
        {
            _midi.Stop();
            _status.Text = "Stopped.";
        }

        private void ExportClick(object sender, EventArgs e)
        {
            SoundSubBlockV4 sub = SelectedSub();
            if (sub == null) return;
            if (sub.Tag == "SO") { _status.Text = "Select a WA or AD sub-block to export."; return; }

            using (var dialog = new SaveFileDialog())
            {
                dialog.FileName = sub.Tag + "_0x" + sub.Offset.ToString("X4") + ExtensionFor(sub);
                dialog.Filter = "Sound data|*" + ExtensionFor(sub) + "|All files|*.*";
                if (dialog.ShowDialog() != DialogResult.OK) return;
                File.WriteAllBytes(dialog.FileName, Payload(sub));
            }
            _status.Text = "Exported " + sub.Tag + " at 0x" + sub.Offset.ToString("X4") + ".";
        }

        private void ExportAllClick(object sender, EventArgs e)
        {
            if (_sound == null) return;

            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() != DialogResult.OK) return;

                int count = 0;
                for (int i = 0; i < _rowSubs.Count; i++)
                {
                    SoundSubBlockV4 sub = _rowSubs[i];
                    if (sub.Tag == "SO") continue; // containers have no payload of their own
                    string name = string.Format("{0:D3}_{1}_0x{2:X4}{3}", i, sub.Tag, sub.Offset, ExtensionFor(sub));
                    File.WriteAllBytes(Path.Combine(dialog.SelectedPath, name), Payload(sub));
                    count++;
                }
                _status.Text = string.Format("Exported {0} sound resource(s).", count);
            }
        }

        private static string ExtensionFor(SoundSubBlockV4 sub)
        {
            if (sub.Tag == "AD") return ".ad";
            if (sub.Tag == "WA") return ".wa";
            return ".bin";
        }
    }
}
