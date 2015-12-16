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
    public partial class ColorCycleControl : BlockBaseControl
    {
        private ColorCycles _colorCycles;

        public ColorCycleControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _colorCycles = (ColorCycles)blockBase;

            CloseBytesCount.Text = string.Format("Number of Close Bytes: {0}", _colorCycles.Close.Count);

            ColorCycles.Items.Clear();
            foreach (ColorCycle colorCycle in _colorCycles.Cycles)
            {
                var item = ColorCycles.Items.Add(colorCycle.Unkown.ToString());
                item.SubItems.Add(colorCycle.Freq.ToString());
                item.SubItems.Add(colorCycle.Flags.ToString());
                item.SubItems.Add(colorCycle.Start.ToString());
                item.SubItems.Add(colorCycle.End.ToString());
            }
        }
    }
}
