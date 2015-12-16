using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Structures.DataFile;
using ScummEditor.Structures.IndexFile;

namespace ScummEditor.Gui
{
    public partial class RoomOffsetTableControl : BlockBaseControl
    {
        private RoomOffsetTable _directoryItem;

        public RoomOffsetTableControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _directoryItem = (RoomOffsetTable)blockBase;

            NumberOfItems.Text = _directoryItem.NumOfRooms.ToString();

            RoomsInfo.Items.Clear();
            foreach (RoomOffsetTableItem colorCycle in _directoryItem.Rooms)
            {
                var item = RoomsInfo.Items.Add(colorCycle.Id.ToString());
                item.SubItems.Add(colorCycle.OffSet.ToString());
            }
            RoomsInfo.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }
    }
}
