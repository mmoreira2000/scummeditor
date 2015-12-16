using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class ImageStripTableControl : BlockBaseControl
    {
        private ImageStripTable _stripTable;

        public ImageStripTableControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _stripTable = (ImageStripTable)blockBase;

            StripCount.Text = "Number of Strips: " + _stripTable.Strips.Count;

            Strips.Items.Clear();
            foreach (StripData colorCycle in _stripTable.Strips)
            {
                ListViewItem item = Strips.Items.Add(colorCycle.OffSet.ToString());
                item.SubItems.Add(string.Format("{0} (0x{1})", colorCycle.CodecId, colorCycle.CodecId.ToString("X").PadLeft(2, '0')));
                item.SubItems.Add(colorCycle.ImageData.Length.ToString());
                item.SubItems.Add(Enum.GetName(typeof(CompressionTypes), colorCycle.CompressionType));
                item.SubItems.Add(Enum.GetName(typeof(RenderingDirections), colorCycle.RenderdingDirection));
                item.SubItems.Add(colorCycle.Transparent.ToString());
                item.SubItems.Add(string.Format("{0} (0x{1})", colorCycle.ParamSubtraction, colorCycle.ParamSubtraction.ToString("X").PadLeft(2, '0')));
            }

            Strips.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void ExportTable_Click(object sender, EventArgs e)
        {
            var d = new SaveFileDialog();
            d.Filter = "Text Files (*.txt)|*.txt";

            DialogResult resp = d.ShowDialog(this);
            if (resp == DialogResult.Cancel) return;

            var sb = new StringBuilder();
            sb.AppendLine("OffSet\tCodecId\tImageData\tMethod\tDirection\tTransparent\tSubtraction");
            foreach (ListViewItem item in Strips.Items)
            {
                sb.AppendFormat("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\n", item.SubItems[0].Text, item.SubItems[1].Text, item.SubItems[2].Text, item.SubItems[3].Text, item.SubItems[4].Text, item.SubItems[5].Text, item.SubItems[6].Text);
            }

            File.WriteAllText(d.FileName, sb.ToString());

        }
    }
}
