using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Structures.DataFile;
using ScummEditor.Gui;

namespace ScummEditor.Gui
{
    public partial class PaletteDataControl : BlockBaseControl
    {
        private PaletteData _paletteData;

        public PaletteDataControl()
        {
            InitializeComponent();

            for (int i = 0; i < 255; i++)
            {
                PaletteTable.Controls.Add(new PaletteColor(i)
                                              {
                                                  Width = 24,
                                                  Height = 24,
                                                  BorderStyle = BorderStyle.FixedSingle,
                                                  CanEdit = true
                                              });
            }
        }

        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);
            _paletteData = (PaletteData)blockBase;

            for (int i = 0; i < 255; i++)
            {
                Color backColor = _paletteData.Colors[i];
                PaletteTable.Controls[i].BackColor = backColor;
                ((PaletteColor)PaletteTable.Controls[i]).ColorChanged += (sender, index, color) => _paletteData.Colors[index] = color;

                toolTip1.SetToolTip(PaletteTable.Controls[i], string.Format("R: {0}, G:{1}, B:{2}", backColor.R, backColor.G, backColor.B));
            }
        }
    }
}
