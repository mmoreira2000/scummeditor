using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class CostumePaletteSelectionWindow : Form
    {
        public CostumePaletteSelectionWindow()
        {
            InitializeComponent();


            for (int i = 0; i < 255; i++)
            {
                PaletteTable.Controls.Add(new PaletteColor(i)
                {
                    Width = 24,
                    Height = 24,
                    BorderStyle = BorderStyle.FixedSingle,
                    CanEdit = false
                });
            }
        }

        public void SetAndRefreshData(PaletteData paletteData, int paletteIndex)
        {
            for (int i = 0; i < 255; i++)
            {
                Color backColor = paletteData.Colors[i];

                PaletteTable.Controls[i].BackColor = backColor;
                ((PaletteColor) PaletteTable.Controls[i]).Selected = i == paletteIndex;
                PaletteTable.Controls[i].Click += (sender, args) =>
                {
                    foreach (var item in PaletteTable.Controls.OfType<PaletteColor>())
                    {
                        item.Selected = false;
                    }
                    ((PaletteColor)sender).Selected = true;
                };

                toolTip1.SetToolTip(PaletteTable.Controls[i], string.Format("R: {0}, G:{1}, B:{2}", backColor.R, backColor.G, backColor.B));
            }
        }

        public int GetSelectedPaletteIndex()
        {
            return PaletteTable.Controls.OfType<PaletteColor>().First(x => x.Selected).Index;
        }

        private void OK_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
        }
    }
}
