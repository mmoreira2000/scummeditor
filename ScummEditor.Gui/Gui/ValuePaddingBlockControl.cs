using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class ValuePaddingBlockControl : BlockBaseControl
    {
        private ValuePaddingBlock _valuePaddingBlock;

        public ValuePaddingBlockControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _valuePaddingBlock = (ValuePaddingBlock)blockBase;

            Value.Text = string.Format("{0} (0x{1})", _valuePaddingBlock.Value, _valuePaddingBlock.Value.ToString("X").PadLeft(2, '0'));
            Padding.Text = string.Format("{0} (0x{1})", _valuePaddingBlock.Padding, _valuePaddingBlock.Padding.ToString("X").PadLeft(2, '0'));
        }
    }
}
