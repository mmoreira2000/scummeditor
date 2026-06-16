using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Structures.IndexFile;

namespace ScummEditor.Gui.IndexFile
{
    public partial class DirectoryOfRoomsControl : BlockBaseControl
    {
        private DirectoryOfRooms _directoryItem;

        public DirectoryOfRoomsControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _directoryItem = (DirectoryOfRooms)blockBase;

            NumberOfItems.Text = _directoryItem.NumOfItems.ToString();

            RoomsInfo.Items.Clear();
            foreach (DirectoryItem colorCycle in _directoryItem.Rooms)
            {
                var item = RoomsInfo.Items.Add(colorCycle.Number.ToString());
                item.SubItems.Add(colorCycle.Offset.ToString());
            }
            RoomsInfo.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }
    }
}
