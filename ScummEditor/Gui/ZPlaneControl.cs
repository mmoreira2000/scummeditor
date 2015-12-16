using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class ZPlaneControl : BlockBaseControl
    {
        private ZPlane _zPlane;

        public ZPlaneControl()
        {
            InitializeComponent();
        }


        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _zPlane = (ZPlane)blockBase;

            StripCount.Text = "Number of Strips: " + _zPlane.Strips.Count;

            Strips.Items.Clear();
            foreach (ZPlaneStripData zPlaneStripData in _zPlane.Strips)
            {
                ListViewItem item = Strips.Items.Add(zPlaneStripData.OffSet.ToString());
                item.SubItems.Add(zPlaneStripData.ImageData.Length.ToString());
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
            sb.AppendLine("OffSet\tImageData");
            foreach (ListViewItem item in Strips.Items)
            {
                sb.AppendFormat("{0}\t{1}\n", item.SubItems[0].Text, item.SubItems[1].Text);
            }

            File.WriteAllText(d.FileName, sb.ToString());

        }
    }
}
