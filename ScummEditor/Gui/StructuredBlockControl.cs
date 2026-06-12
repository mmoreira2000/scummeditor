using System.Windows.Forms;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    /// <summary>
    /// Generic read-only viewer for the structured data blocks (BOXD, BOXM, SCAL, OFFS).
    /// One instance is shared across those block types; it rebuilds the grid for the
    /// selected block on each refresh.
    /// </summary>
    public partial class StructuredBlockControl : BlockBaseControl
    {
        public StructuredBlockControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            grid.Columns.Clear();
            grid.Rows.Clear();

            if (blockBase is BoxData)
            {
                ShowBoxData((BoxData)blockBase);
            }
            else if (blockBase is BoxMatrix)
            {
                ShowBoxMatrix((BoxMatrix)blockBase);
            }
            else if (blockBase is Scale)
            {
                ShowScale((Scale)blockBase);
            }
            else if (blockBase is PaletteOffset)
            {
                ShowPaletteOffset((PaletteOffset)blockBase);
            }
            else if (blockBase is EgaPalette)
            {
                ShowEgaPalette((EgaPalette)blockBase);
            }
            else
            {
                summary.Text = string.Empty;
            }
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

        private void ShowBoxData(BoxData block)
        {
            summary.Text = string.Format("{0} box(es)", block.NumBoxes);
            AddColumns("Box", "ulx", "uly", "urx", "ury", "lrx", "lry", "llx", "lly", "mask", "flags", "scale");

            for (int i = 0; i < block.Boxes.Count; i++)
            {
                Box b = block.Boxes[i];
                grid.Rows.Add(i, b.Ulx, b.Uly, b.Urx, b.Ury, b.Lrx, b.Lry, b.Llx, b.Lly,
                    b.Mask, ToHex(b.Flags), b.Scale);
            }
        }

        private void ShowBoxMatrix(BoxMatrix block)
        {
            summary.Text = string.Format("{0} line(s)", block.Rows.Count);
            AddColumns("Box", "Connections (start-end → box)");

            for (int i = 0; i < block.Rows.Count; i++)
            {
                MatrixRow row = block.Rows[i];
                var parts = new System.Text.StringBuilder();
                for (int j = 0; j < row.Links.Count; j++)
                {
                    BoxLink link = row.Links[j];
                    if (j > 0) parts.Append(", ");
                    parts.AppendFormat("{0}-{1} → {2}", link.Start, link.End, link.Box);
                }
                grid.Rows.Add(i, parts.ToString());
            }
        }

        private void ShowScale(Scale block)
        {
            summary.Text = string.Format("{0} scale slot(s)", block.Slots.Count);
            AddColumns("Slot", "scale1", "y1", "scale2", "y2");

            for (int i = 0; i < block.Slots.Count; i++)
            {
                ScaleSlot s = block.Slots[i];
                grid.Rows.Add(i, s.Scale1, s.Y1, s.Scale2, s.Y2);
            }
        }

        private void ShowPaletteOffset(PaletteOffset block)
        {
            summary.Text = string.Format("{0} palette offset(s), relative to the OFFS block", block.Offsets.Count);
            AddColumns("Palette", "Offset");

            for (int i = 0; i < block.Offsets.Count; i++)
            {
                grid.Rows.Add(i, block.Offsets[i]);
            }
        }

        private void ShowEgaPalette(EgaPalette block)
        {
            summary.Text = string.Format("EGA dithering palette: {0} entries (two EGA colors per VGA index)", block.EntryCount);
            AddColumns("VGA index", "Byte", "EGA color 1", "EGA color 2");

            for (int i = 0; i < block.EntryCount; i++)
            {
                int low, high;
                block.GetEntry(i, out low, out high);
                grid.Rows.Add(i, "0x" + block.RawContent[i].ToString("X2"), low, high);
            }
        }

        private static string ToHex(byte value)
        {
            return "0x" + value.ToString("X2");
        }
    }
}
