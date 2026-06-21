using System.Drawing;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Shared widget/formatting helpers for the v2 / v3-old read-only viewers (room, object, script), so
    /// they look and behave like the v4 counterparts (field/value grid + monospace disassembly box). This
    /// is GUI presentation only - the engine (OldBundleNavigator) supplies data and the raw disassembler
    /// Result; the formatting into display text lives here, on the GUI side.
    /// </summary>
    internal static class OldBundleControlHelpers
    {
        /// <summary>A read-only two-column (Field / Value) grid, matching ObjectCodeControl's layout.</summary>
        public static DataGridView CreateFieldValueGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = SystemColors.Window
            };
            grid.Columns.Add(NonSortableColumn("Field"));
            grid.Columns.Add(NonSortableColumn("Value"));
            return grid;
        }

        private static DataGridViewTextBoxColumn NonSortableColumn(string header)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                ReadOnly = true
            };
        }

        /// <summary>A read-only monospace multiline text box for a disassembly listing, matching ScriptControl.</summary>
        public static TextBox CreateCodeBox()
        {
            return new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = ScrollBars.Both,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                BackColor = Color.White
            };
        }

        /// <summary>
        /// Formats a disassembler Result for a TextBox: an optional header, a warning when the code did not
        /// decode to the end (matching ScriptControl/ObjectCodeControl), then the listing. CRLF-normalised.
        /// </summary>
        public static string FormatListing(string header, ScummV6Disassembler.Result result)
        {
            string body;
            if (result == null)
            {
                body = (header != null ? header + "\n\n" : string.Empty) + "(no code)";
            }
            else
            {
                string warning = result.DecodedToEnd
                    ? string.Empty
                    : "// WARNING: disassembly stopped before the end (unknown opcode).\n";
                body = (header != null ? header + "\n\n" : string.Empty) + warning + (result.Listing ?? string.Empty);
            }
            return body.Replace("\r\n", "\n").Replace("\n", "\r\n");
        }
    }
}
