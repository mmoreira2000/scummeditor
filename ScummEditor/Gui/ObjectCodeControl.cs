using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Read-only viewer for OBCD blocks. The top grid shows the decoded object fields (CDHD
    /// header, OBNA name and the verb offset table); the bottom pane shows the disassembled
    /// verb bytecode, with a label at each verb entry point.
    /// </summary>
    public partial class ObjectCodeControl : BlockBaseControl
    {
        public ObjectCodeControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            var obcd = (ObjectCode)blockBase;

            // --- object fields -------------------------------------------------
            grid.Columns.Clear();
            grid.Rows.Clear();
            AddColumns("Field", "Value");

            if (obcd.HasCodeHeader)
            {
                grid.Rows.Add("Object id", obcd.ObjectId);
                grid.Rows.Add("X", obcd.X);
                grid.Rows.Add("Y", obcd.Y);
                grid.Rows.Add("Width", obcd.Width);
                grid.Rows.Add("Height", obcd.Height);
                grid.Rows.Add("Flags", "0x" + obcd.Flags.ToString("X2"));
                grid.Rows.Add("Parent", obcd.ParentObject);
                grid.Rows.Add("Actor direction", obcd.ActorDirection);
            }
            grid.Rows.Add("Name", obcd.Name);
            grid.Rows.Add("Verbs", obcd.NumVerbs);

            var labels = new Dictionary<int, string>();
            foreach (VerbEntry entry in obcd.VerbEntries)
            {
                int sliceRel = obcd.VerbBlockOffset + entry.Offset - obcd.VerbCodeOffset;
                string name = entry.Id == 0xFF ? "verb_any" : "verb_0x" + entry.Id.ToString("X2");
                bool inRange = sliceRel >= 0 && sliceRel < obcd.VerbCodeLength;

                grid.Rows.Add(name, inRange ? sliceRel.ToString("X4") : "(out of range)");
                if (inRange && !labels.ContainsKey(sliceRel)) labels.Add(sliceRel, name);
            }

            // --- verb bytecode disassembly --------------------------------------
            var sb = new StringBuilder();
            if (obcd.VerbCodeOffset < 0 || obcd.VerbCodeLength <= 0)
            {
                sb.AppendLine("// (no verb scripts)");
            }
            else
            {
                var slice = new byte[obcd.VerbCodeLength];
                Array.Copy(obcd.RawContent, obcd.VerbCodeOffset, slice, 0, obcd.VerbCodeLength);

                Scumm6Disassembler.Result result;
                if (obcd.GameInfo != null && obcd.GameInfo.ScummVersion == 5)
                {
                    result = Scumm5Disassembler.Disassemble(slice, 0, labels);
                }
                else
                {
                    result = Scumm6Disassembler.Disassemble(slice, 0, labels);
                }

                if (!result.DecodedToEnd)
                    sb.AppendLine("// WARNING: disassembly stopped before the end of the script (unknown opcode).");
                sb.Append(result.Listing);
            }

            // TextBox needs CRLF line endings to render line breaks correctly.
            code.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            code.Select(0, 0);
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
    }
}
