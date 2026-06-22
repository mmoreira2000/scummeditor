using System;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Read-only viewer for a v2 / v3-old object (an OldBundleBlock of Kind=Object), mirroring the v4
    /// ObjectCodeControl: a field/value grid (index, id, name, declared size, verb table) on top and the
    /// disassembled verb bytecode below. Data + disassembly come from the engine; this only renders them.
    /// </summary>
    public class OldBundleObjectControl : UserControl
    {
        private readonly DataGridView _grid;
        private readonly TextBox _code;
        private readonly SplitContainer _split;
        private bool _splitterApplied;

        public OldBundleObjectControl()
        {
            // SplitterDistance is applied later in OnSizeChanged, not here: setting it at construction (when
            // the control still has its tiny default size) throws InvalidOperationException.
            _split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
            _grid = OldBundleControlHelpers.CreateFieldValueGrid();
            _code = OldBundleControlHelpers.CreateCodeBox();
            _split.Panel1.Controls.Add(_grid);
            _split.Panel2.Controls.Add(_code);
            Controls.Add(_split);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (!_splitterApplied && _split != null && _split.Height > 220)
            {
                _split.SplitterDistance = 150; // field grid on top, disassembly below
                _splitterApplied = true;
            }
        }

        public void SetData(OldBundleBlock block)
        {
            _grid.Rows.Clear();
            _code.Clear();
            if (block == null || block.ObjectInfo == null) return;

            OldBundleObjectInfo info = block.ObjectInfo;
            _grid.Rows.Add("Object index", info.Index);
            _grid.Rows.Add("Object id", info.Id);
            _grid.Rows.Add("Name", string.IsNullOrEmpty(info.Name) ? "(none)" : info.Name);
            _grid.Rows.Add("Declared size", info.Width + " x " + info.Height);
            _grid.Rows.Add("Verb segments", info.VerbCode.Count);
            foreach (OldBundleCodeRange v in info.VerbCode)
                _grid.Rows.Add(v.Label, v.End > v.Start ? "0x" + v.Start.ToString("X4") : "(no body)");

            byte[] data = block.DataFile != null ? block.DataFile.RawContent : null;
            var sb = new StringBuilder();
            if (info.VerbCode.Count == 0)
            {
                sb.Append("// (this object has no verb code)");
            }
            else
            {
                foreach (OldBundleCodeRange v in info.VerbCode)
                {
                    sb.AppendLine("===== " + v.Label + " =====");
                    if (v.End > v.Start)
                    {
                        ScummV6Disassembler.Result r = OldBundleNavigator.DisassembleRange(data, v.Start, v.End, block.IsV2, block.IsIndy3, block.GameInfo != null && block.GameInfo.ScummVersion == 1);
                        sb.AppendLine(OldBundleControlHelpers.FormatListing(null, r));
                    }
                    else
                    {
                        sb.AppendLine("(no separate code body)");
                    }
                    sb.AppendLine();
                }
            }
            _code.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            _code.Select(0, 0);
        }
    }
}
