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
    public partial class RoomHeaderControl : BlockBaseControl
    {
        private RoomHeader _roomHeader;
        public RoomHeaderControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _roomHeader = (RoomHeader) blockBase;

            Width.Text = _roomHeader.Width.ToString();
            Height.Text = _roomHeader.Height.ToString();
            NumberObjects.Text = _roomHeader.NumObjects.ToString();
        }
    }
}
