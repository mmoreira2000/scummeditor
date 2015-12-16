using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class ImageBompControl : BlockBaseControl
    {
        private ImageBomp _imageData;

        public ImageBompControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _imageData = (ImageBomp) blockBase;

            Unknown.Text = _imageData.Unknown.ToString();
            ImageWidth.Text = _imageData.Width.ToString();
            ImageHeight.Text = _imageData.Height.ToString();
            Padding1.Text = _imageData.Padding[0].ToString();
            Padding2.Text = _imageData.Padding[1].ToString();
            DataSize.Text = _imageData.Data.Length.ToString();

            var x = new DynamicByteProvider(_imageData.Data);
            ImageData.ByteProvider = x;
        }
    }
}
