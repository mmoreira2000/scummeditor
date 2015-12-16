using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Encoders;
using ScummEditor.Exceptions;
using ScummEditor.Structures.DataFile;
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
            int[] usedIndexes;
            Bitmap background = GenericDecodeImage(out usedIndexes);
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

            int[] usedPalleteIndexes;
            Bitmap background = GenericDecodeImage(out usedPalleteIndexes);

            //EncoderParameters parameters = new EncoderParameters();
            //var x = new System.Drawing.Imaging.Encoder(new Guid("66087055-ad66-4c7c-9a18-38a2310b8337"));
            //parameters.Param[0] = new EncoderParameter(x, 8);
            if (Export8Bits.Checked)
            {
                var convert = new ImageDepthConversor();
                if (_imageType == ImageType.Costume)
                {
                    var c = new List<Color>();
                    for (int i = 0; i < 256; i++)
                    {
                        c.Add(Color.Black);
                    }

                    PaletteData defaultPallete = _roomBlock.GetDefaultPalette();
                    for (int i = 0; i < _costume.Palette.Count; i++)
                    {
                        c[i] = defaultPallete.Colors[_costume.Palette[i]];
                    }
                    background = convert.CopyToBpp(background, 8, c.ToArray());
                }
                else
                {
                    background = convert.CopyToBpp(background, 8, _roomBlock.GetDefaultPalette().Colors);
                }
            }

            background.Save(dlg.FileName, ImageFormat.Png);
            File.WriteAllText(dlg.FileName + ".idx", string.Join(";", usedPalleteIndexes));
        }

        private void TesteReencode_Click(object sender, EventArgs e)
        {
            int[] usedIndexes;
            Bitmap background = GenericDecodeImage(out usedIndexes);

            GenericEncodeImage(background, usedIndexes);

            Bitmap backgroundReencoded = GenericDecodeImage(out usedIndexes);
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
            var preferredIndexes = new int[] { };

            string indexFile = dlg.FileName + ".idx";
            if (File.Exists(indexFile))
            {
                preferredIndexes = File.ReadAllText(indexFile).Split(';').Select(s => Convert.ToInt32(s)).ToArray();
            }
            GenericEncodeImage(background, preferredIndexes);

            DecodeImage();
        }

        private Bitmap GenericDecodeImage(out int[] usedPalleteIndexes)
        {
            usedPalleteIndexes = null;

            if (_imageType == ImageType.Background)
            {
                var decoder = new ImageDecoder();
                decoder.PaletteIndex = PaletteIndex;
                decoder.UseTransparentColor = DecodeTransparent;
                Bitmap decodedImage = decoder.Decode(_roomBlock);
                usedPalleteIndexes = decoder.UsedIndexes.ToArray();
                return decodedImage;
            }
            if (_imageType == ImageType.ZPlane)
            {
                var decoder = new ZPlaneDecoder();
                return decoder.Decode(_roomBlock, _zPlaneIndex);
            }
            if (_imageType == ImageType.Object)
            {
                if (_roomBlock.GetOBIMs()[_objectIndex].GetIMxx()[_imageIndex].GetSMAP() == null)
                {
                    var decoder = new BompImageDecoder();
                    decoder.PaletteIndex = PaletteIndex;
                    decoder.UseTransparentColor = DecodeTransparent;
                    Bitmap decodedImage = decoder.Decode(_roomBlock, _objectIndex, _imageIndex);
                    usedPalleteIndexes = decoder.UsedIndexes.ToArray();
                    return decodedImage;
                }
                else
                {
                    var decoder = new ImageDecoder();
                    decoder.PaletteIndex = PaletteIndex;
                    decoder.UseTransparentColor = DecodeTransparent;
                    Bitmap decodedImage = decoder.Decode(_roomBlock, _objectIndex, _imageIndex);
                    usedPalleteIndexes = decoder.UsedIndexes.ToArray();
                    return decodedImage;
                }
            }
            if (_imageType == ImageType.ObjectsZPlane)
            {
                var decoder = new ZPlaneDecoder();
                return decoder.Decode(_roomBlock, _objectIndex, _imageIndex, _zPlaneIndex);
            }
            if (_imageType == ImageType.Costume)
            {
                var decoder = new CostumeImageDecoder();
                decoder.PaletteIndex = PaletteIndex;
                decoder.UseTransparentColor = DecodeTransparent;
                return decoder.Decode(_roomBlock, _costume, _imageIndex);
            }
            return null;
        }

        private void GenericEncodeImage(Bitmap bitmapToEncode, int[] preferredIndexes)
        {
            try
            {
                switch (_imageType)
                {
                    case ImageType.Background:
                        {
                            var encoder = new ImageEncoder();
                            encoder.PaletteIndex = PaletteIndex;
                            encoder.EncodeSettings = (ImageEncoder.EncodeTypeSettings)CompressionMethod.SelectedIndex;
                            encoder.PreferredIndexes = new List<int>(preferredIndexes);
                            encoder.Encode(_roomBlock, bitmapToEncode);
                        }
                        break;
                    case ImageType.ZPlane:
                        {
                            var encoder = new ZPlaneEncoder();
                            encoder.Encode(_roomBlock, bitmapToEncode, _zPlaneIndex);
                        }
                        break;
                    case ImageType.Object:
                        if (_roomBlock.GetOBIMs()[_objectIndex].GetIMxx()[_imageIndex].GetSMAP() == null)
                        {
                            var encoder = new BompImageEncoder();
                            encoder.PaletteIndex = PaletteIndex;
                            encoder.PreferredIndexes = new List<int>(preferredIndexes);
                            encoder.Encode(_roomBlock, _objectIndex, _imageIndex, bitmapToEncode);
                        }
                        else
                        {
                            var encoder = new ImageEncoder();
                            encoder.PaletteIndex = PaletteIndex;
                            encoder.EncodeSettings = (ImageEncoder.EncodeTypeSettings)CompressionMethod.SelectedIndex;
                            encoder.PreferredIndexes = new List<int>(preferredIndexes);
                            encoder.Encode(_roomBlock, _objectIndex, _imageIndex, bitmapToEncode);
                        }
                        break;
                    case ImageType.ObjectsZPlane:
                        {
                            var encoder = new ZPlaneEncoder();
                            encoder.Encode(_roomBlock, _objectIndex, _imageIndex, bitmapToEncode, _zPlaneIndex);
                        }
                        break;
                    case ImageType.Costume:
                        {
                            var encoder = new CostumeImageEncoder();
                            encoder.PaletteIndex = PaletteIndex;
                            encoder.Encode(_roomBlock, _costume, _imageIndex, bitmapToEncode);
                        }
                        break;
                }

            }
            catch (ImageEncodeException ex)
            {
                MessageBox.Show(ex.Message, "Error importing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
