using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ScummEditor.Engine.Structures.DataFile;

using ScummEditor.Engine.Structures;
namespace ScummEditor.Gui
{
    public partial class NotImplementedDataBlockControl : BlockBaseControl
    {
        // Any byte-preserved block: NotImplementedDataBlock (v4-v6) and the v7 RawContainerBlock /
        // RawDataBlock / RawIndexBlock. Contents is null for a RawContainerBlock that parsed children
        // (a container) - it then has no raw bytes of its own, so an empty hex view is shown.
        private IRawContentBlock _rawBlock;

        public NotImplementedDataBlockControl()
        {
            InitializeComponent();
        }


        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);
            _rawBlock = (IRawContentBlock)blockBase;

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
            var x = new DynamicByteProvider(_rawBlock.Contents ?? new byte[0]);
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
