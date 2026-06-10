using System.Text;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Read-only viewer that disassembles a SCUMM v6 script block (SCRP/LSCR/EXCD/ENCD) into a
    /// C#-like listing.
    /// </summary>
    public partial class ScriptControl : BlockBaseControl
    {
        public ScriptControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            var script = (ScriptBlock)blockBase;
            Scumm6Disassembler.Result result = script.Disassemble();

            var sb = new StringBuilder();
            sb.Append("// ").Append(script.BlockType);
            if (script.ScriptId >= 0) sb.Append(" #").Append(script.ScriptId);
            sb.Append("  (").Append(script.RawContent.Length).Append(" bytes)");
            sb.AppendLine();
            if (!result.DecodedToEnd)
            {
                sb.AppendLine("// WARNING: disassembly stopped before the end of the script (unknown opcode).");
            }
            sb.AppendLine();
            sb.Append(result.Listing);

            // TextBox needs CRLF line endings to render line breaks correctly.
            code.Text = sb.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
            code.Select(0, 0);
        }
    }
}
