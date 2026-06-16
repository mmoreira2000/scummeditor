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
    public partial class ObjectImageHeaderControl : BlockBaseControl
    {
        private ObjectImageHeader _objectImageHeader;

        public ObjectImageHeaderControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _objectImageHeader = (ObjectImageHeader) blockBase;

            Id.Text = _objectImageHeader.Id.ToString();
            LocationHeigth.Text = _objectImageHeader.Height.ToString();
            LocationWidth.Text = _objectImageHeader.Width.ToString();
            NumHotspots.Text = _objectImageHeader.NumHotspots.ToString();
            NumImages.Text = _objectImageHeader.NumImages.ToString();
            NumZPlanes.Text = _objectImageHeader.NumZPlanes.ToString();
            Unknown.Text = _objectImageHeader.Unknown.ToString();
            LocationX.Text = _objectImageHeader.X.ToString();
            LocationY.Text = _objectImageHeader.Y.ToString();

            Hotspots.Items.Clear();
            foreach (Hotspot hotspot in _objectImageHeader.Hotspots)
            {
                ListViewItem item = Hotspots.Items.Add(hotspot.X.ToString());
                item.SubItems.Add(hotspot.Y.ToString());
            }
        }
    }
}
