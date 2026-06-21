using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Read-only viewer for a v2 / v3-old script (an OldBundleBlock of Kind=Script), mirroring the v4
    /// ScriptControl: a monospace disassembly listing with a header and a "stopped before end" warning.
    /// The disassembly comes from the engine (OldBundleNavigator.DisassembleRange).
    /// </summary>
    public class OldBundleScriptControl : UserControl
    {
        private readonly TextBox _code;

        public OldBundleScriptControl()
        {
            _code = OldBundleControlHelpers.CreateCodeBox();
            Controls.Add(_code);
        }

        public void SetData(OldBundleBlock block)
        {
            if (block == null) { _code.Clear(); return; }

            byte[] data = block.DataFile != null ? block.DataFile.RawContent : null;
            ScummV6Disassembler.Result result = OldBundleNavigator.DisassembleRange(data, block.Start, block.End, block.IsV2, block.IsIndy3);

            int length = block.End - block.Start;
            string title = string.IsNullOrEmpty(block.Title) ? block.BlockType : block.Title;
            string header = "// " + title + "   (0x" + block.Start.ToString("X4") + " .. 0x"
                + block.End.ToString("X4") + ", " + length + " bytes)";
            _code.Text = OldBundleControlHelpers.FormatListing(header, result);
            _code.Select(0, 0);
        }
    }
}
