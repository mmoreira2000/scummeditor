using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures.DataFile;
using Encoder = System.Text.Encoder;

namespace ScummEditor.Gui
{
    public partial class RoomBlockImageControl : UserControl
    {
        private readonly RoomBlock _roomBlock;
        private readonly Costume _costume;
        private readonly ImageType _imageType;
        private readonly int _imageIndex;
        private readonly int _zPlaneIndex;
        private readonly int _objectIndex;

        public bool DecodeTransparent { get; set; }

        public int PaletteIndex { get; set; }

        public RoomBlockImageControl()
        {
            InitializeComponent();
        }

        public RoomBlockImageControl(RoomBlock roomBlock, Costume costume, ImageType imageType, int frameIndex)
            : this(roomBlock, costume, imageType, 0, frameIndex, 0) { }

        public RoomBlockImageControl(RoomBlock roomBlock, ImageType imageType)
            : this(roomBlock, null, imageType, 0, 0, 0) { }

        public RoomBlockImageControl(RoomBlock roomBlock, ImageType imageType, int zPlaneIndex)
            : this(roomBlock, null, imageType, 0, 0, zPlaneIndex) { }

        public RoomBlockImageControl(RoomBlock roomBlock, ImageType imageType, int objectIndex, int imageIndex)
            : this(roomBlock, null, imageType, objectIndex, imageIndex, 0) { }

        public RoomBlockImageControl(RoomBlock roomBlock, ImageType imageType, int objectIndex, int imageIndex, int zPlaneIndex)
            : this(roomBlock, null, imageType, objectIndex, imageIndex, zPlaneIndex) { }

        public RoomBlockImageControl(RoomBlock roomBlock, Costume costume, ImageType imageType, int objectIndex, int imageIndex, int zPlaneIndex)
        {
            InitializeComponent();

            _roomBlock = roomBlock;
            _costume = costume;
            _imageType = imageType;
            _imageIndex = imageIndex;
            _zPlaneIndex = zPlaneIndex;
            _objectIndex = objectIndex;

            CompressionMethod.Items.Add("Auto Detect");
            CompressionMethod.Items.Add("Uncompressed");
            CompressionMethod.Items.Add("Method 1 Vertical");
            CompressionMethod.Items.Add("Method 1 Horizontal");
            CompressionMethod.Items.Add("Method 2");
            CompressionMethod.Items.Add("Method 2 Alternate Code");

            CompressionMethod.SelectedIndex = 0;

            if (imageType == ImageType.Background || (imageType == ImageType.Object && _roomBlock.GetOBIMs()[_objectIndex].GetIMxx()[_imageIndex].GetSMAP() != null))
            {
                CompressionMethodLabel.Visible = true;
                CompressionMethod.Visible = true;
            }
            else
            {
                pictureScroll.Top = CompressionMethod.Top;
            }
        }

        public void Decode()
        {
            DecodeImage();
        }

        private void DecodeImage()
        {
            Bitmap background = GenericDecodeImage();
            if (background != null)
            {
                pictureBackground.Width = background.Width;
                pictureBackground.Height = background.Height;
                pictureBackground.Image = background;
            }
        }


        private void SaveToDisk_Click(object sender, EventArgs e)
        {
            var dlg = new SaveFileDialog()
            {
                Filter = "PNG Files|*.png"
            };
            var result = dlg.ShowDialog();
            if (result == DialogResult.Cancel) return;

            // GenericDecodeImage already returns a true indexed (8bpp) bitmap for palette-based
            // images, so the saved PNG preserves the original palette indexes losslessly.
            Bitmap background = GenericDecodeImage();

            background.Save(dlg.FileName, ImageFormat.Png);
        }

        private void TesteReencode_Click(object sender, EventArgs e)
        {
            Bitmap background = GenericDecodeImage();

            GenericEncodeImage(background);

            Bitmap backgroundReencoded = GenericDecodeImage();
            pictureBackground.Width = background.Width;
            pictureBackground.Height = background.Height;
            pictureBackground.Image = backgroundReencoded;
        }

        private void ImportFromDisk_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog()
            {
                Filter = "PNG Files|*.png"
            };
            var result = dlg.ShowDialog();
            if (result == DialogResult.Cancel) return;

            var background = (Bitmap)Bitmap.FromFile(dlg.FileName);
            GenericEncodeImage(background);

            DecodeImage();
        }

        // Decoding/encoding lives in the engine (ImageResourceCodec); this control only chooses the
        // resource + compression and shows/saves the Bitmap.
        private Bitmap GenericDecodeImage()
        {
            return ImageResourceCodec.Decode(_roomBlock, _costume, _imageType,
                _objectIndex, _imageIndex, _zPlaneIndex, PaletteIndex, DecodeTransparent);
        }

        private void GenericEncodeImage(Bitmap bitmapToEncode)
        {
            try
            {
                ImageResourceCodec.Encode(_roomBlock, _costume, _imageType,
                    _objectIndex, _imageIndex, _zPlaneIndex, bitmapToEncode,
                    (ImageEncoder.EncodeTypeSettings)CompressionMethod.SelectedIndex);
            }
            catch (ImageEncodeException ex)
            {
                MessageBox.Show(ex.Message, "Error importing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
