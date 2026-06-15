using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Read-only viewer for a SCUMM v4 "SO" sound block: lists its WA/AD (and nested SO) sub-blocks
    /// with tag, offset, size and a human-readable kind. There is no Play button - v4 AD/WA payloads
    /// are raw OPL2 / Roland streams, not Standard MIDI or VOC, so the editor's players cannot render
    /// them. The block itself is kept verbatim (round-trips byte-for-byte).
    /// </summary>
    public class SoundBlockV4Control : BlockBaseControl
    {
        private readonly DataGridView _grid;
        private readonly Label _status;

        public SoundBlockV4Control()
        {
            _status = new Label { Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(4, 0, 0, 0) };
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _grid.Columns.Add("tag", "Sub-block");
            _grid.Columns.Add("kind", "Kind");
            _grid.Columns.Add("offset", "Offset");
            _grid.Columns.Add("size", "Size");

            Controls.Add(_grid);
            Controls.Add(_status);
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _grid.Rows.Clear();
            var sound = blockBase as SoundBlockV4;
            if (sound == null) { _status.Text = string.Empty; return; }

            int wa = 0, ad = 0, so = 0;
            AddRows(sound.SubBlocks, 0, ref wa, ref ad, ref so);
            _status.Text = string.Format("v4 sound: {0} WA, {1} AD, {2} nested SO   ({3} bytes)", wa, ad, so, sound.RawContent.Length);
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
                AddRows(sub.Children, depth + 1, ref wa, ref ad, ref so);
            }
        }
    }
}
