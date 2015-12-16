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
    public delegate void ColorChangedEventHandler(object sender, int paletteIndex, Color newColor);
    public delegate void PaletteChangedEventHandler(object sender, int paletteIndex, int newRelativePaletteIndex);

    public partial class PaletteColor : UserControl
    {
        private bool _canEdit;
        private int _paletteIndex;
        private bool _selected;

        public event ColorChangedEventHandler ColorChanged;
        public event PaletteChangedEventHandler PaletteChanged;

        public PaletteColor(int index)
        {
            InitializeComponent();
            Index = index;
            PaletteIndex = index;
        }

        public PaletteData FullPalette { get; set; }

        public bool CanEdit
        {
            get { return _canEdit; }
            set
            {
                _canEdit = value;
                Cursor = _canEdit ? Cursors.Hand : Cursors.Default;
            }
        }

        public int Index { get; private set; }

        public int PaletteIndex
        {
            get { return _paletteIndex; }
            set
            {
                _paletteIndex = value;
                indexText.Text = _paletteIndex.ToString();
            }
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                if (_selected)
                {
                    indexText.Location = new Point(2, 2);
                    var bmp = new Bitmap(Width, Height);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.FillRectangle(new SolidBrush(BackColor), new Rectangle(0, 0, Width, Height));
                        g.DrawRectangle(new Pen(Brushes.Red, 2), new Rectangle(1, 1, Width - 4, Height - 4));
                    }
                    BackgroundImage = bmp;
                }
                else
                {
                    indexText.Location = new Point(0, 0);
                    BackgroundImage = null;
                }
                //Invalidate();
            }
        }

        private void ColorClick(object sender, EventArgs e)
        {
            if (!CanEdit) return;

            if (FullPalette == null)
            {
                var cDlg = new ColorDialog();
                cDlg.Color = BackColor;
                if (cDlg.ShowDialog(this) == DialogResult.Cancel) return;

                BackColor = cDlg.Color;
                OnColorChanged(Index, cDlg.Color);
            }
            else
            {
                var window = new CostumePaletteSelectionWindow();
                window.SetAndRefreshData(FullPalette, _paletteIndex);
                if (window.ShowDialog(this) == DialogResult.Cancel) return;
                var result = window.GetSelectedPaletteIndex();
                OnPaletteChanged(Index, result);
            }


        }

        private void OnColorChanged(int paletteindex, Color newcolor)
        {
            ColorChangedEventHandler handler = ColorChanged;
            if (handler != null) handler(this, paletteindex, newcolor);
        }

        private void OnPaletteChanged(int paletteindex, int newrelativepaletteindex)
        {
            PaletteChangedEventHandler handler = PaletteChanged;
            if (handler != null) handler(this, paletteindex, newrelativepaletteindex);
        }

        private void TextClick(object sender, EventArgs e)
        {
            OnClick(EventArgs.Empty);
        }
    }
}
