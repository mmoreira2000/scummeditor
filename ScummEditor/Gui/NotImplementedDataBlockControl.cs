using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class NotImplementedDataBlockControl : BlockBaseControl
    {
        private NotImplementedDataBlock _notImplementedDataBlock;

        public NotImplementedDataBlockControl()
        {
            InitializeComponent();
        }


        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);
            _notImplementedDataBlock = (NotImplementedDataBlock)blockBase;

            if (AutomaticLoadData.Checked)
            {
                LoadBinaryData();
            }
            else
            {
                var x = new DynamicByteProvider(new byte[0]);
                hexBox1.ByteProvider = x;
            }
        }

        private void LoadBinaryData()
        {
            var x = new DynamicByteProvider(_notImplementedDataBlock.Contents);
            hexBox1.ByteProvider = x;
        }

        private void LoadData_Click(object sender, EventArgs e)
        {
            LoadBinaryData();
        }

        private void AutomaticLoadData_CheckedChanged(object sender, EventArgs e)
        {
            if (AutomaticLoadData.Checked)
            {
                LoadData.Enabled = false;
                LoadBinaryData();
            }
            else
            {
                LoadData.Enabled = false;
            }
        }
    }
}
