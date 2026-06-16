using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class BlockBaseControl : UserControl
    {
        private BlockBase _blockBase;

        public BlockBaseControl()
        {
            InitializeComponent();
        }

        public virtual void SetAndRefreshData(BlockBase blockBase)
        {
            _blockBase = blockBase;

            blockType.Text = _blockBase.BlockType;
            blockSize.Text = _blockBase.BlockSize.ToString();
            blockOffset.Text = _blockBase.BlockOffSet.ToString();
        }
    }

}
