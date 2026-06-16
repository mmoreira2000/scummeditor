using System;
using System.Windows.Forms;
using ScummEditor.Gui;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class CostumeControl : BlockBaseControl
    {
        private Costume _costumeBlock;

        public CostumeControl()
        {
            InitializeComponent();
        }

        public override void SetAndRefreshData(BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            _costumeBlock = (Costume)blockBase;


            //General
            Header.Text = _costumeBlock.Header;
            Size.Text = _costumeBlock.Size.ToString();
            Format.Text = _costumeBlock.Format.ToString();
            if (_costumeBlock.HasCloseByte)
            {
                CloseByte.Text = _costumeBlock.CloseByte.ToString();
            }
            else
            {
                CloseByte.Text = string.Empty;
            }

            //Animations
            NumAnimations.Text = _costumeBlock.NumAnim.ToString();
            AnimOffsets.Items.Clear();
            foreach (ushort animationOffset in _costumeBlock.AnimOffsets)
            {
                var item = AnimOffsets.Items.Add(animationOffset.ToString());
            }

            Animations.Nodes.Clear();
            foreach (Animation animation in _costumeBlock.Animations)
            {
                TreeNode nodeAnim = Animations.Nodes.Add(animation.Offset.ToString(), "Offset: " + animation.Offset.ToString());
                nodeAnim.Nodes.Add(string.Format("LimbMask: {0}", animation.LimbMask));
                nodeAnim.Nodes.Add(string.Format("Number of Limbs: {0}", animation.NumLimbs));

                int animationCount = 0;
                foreach (AnimationDefinition animationDefinition in animation.AnimDefinitions)
                {
                    animationCount++;
                    TreeNode definition = nodeAnim.Nodes.Add(string.Format("Animation {0}", animationCount));

                    definition.Nodes.Add(string.Format("Disabled: {0}", animationDefinition.Disabled));
                    definition.Nodes.Add(string.Format("Length: {0}", animationDefinition.Length));
                    definition.Nodes.Add(string.Format("NoLoop: {0}", animationDefinition.NoLoop));
                    definition.Nodes.Add(string.Format("Start: {0}", animationDefinition.Start));
                    definition.Nodes.Add(string.Format("NoLoopAndEndOffset Byte: {0}", animationDefinition.NoLoopAndEndOffset));
                }
            }
            Animations.ExpandAll();

            //Animation commands
            AnimationCommandsOffset.Text = _costumeBlock.AnimCommandsOffset.ToString();
            Commands.Items.Clear();
            foreach (byte command in _costumeBlock.Commands)
            {
                ListViewItem item = Commands.Items.Add(string.Format("{0} - (0x{1})", command, command.ToString("X")));
                if (command >= 0x71 && command <= 0x78) item.SubItems.Add("Add Sound");
                if (command == 0x79) item.SubItems.Add("Stop");
                if (command == 0x7A) item.SubItems.Add("Start");
                if (command == 0x7B) item.SubItems.Add("Hide");
                if (command == 0x7C) item.SubItems.Add("SkipFrame");
            }

            //Limbs
            LimbsOffsets.Items.Clear();
            foreach (ushort limbsOffset in _costumeBlock.LimbsOffsets)
            {
                LimbsOffsets.Items.Add(limbsOffset.ToString());
            }

            int limbNumber = 0;
            Limbs.Nodes.Clear();
            foreach (Limb limb in _costumeBlock.Limbs)
            {
                limbNumber++;
                TreeNode nodeLimb = Limbs.Nodes.Add(string.Format("Limb {0}", limbNumber));
                nodeLimb.Nodes.Add(string.Format("Offset: {0}", limb.OffSet));
                nodeLimb.Nodes.Add(string.Format("Size: {0}", limb.Size));

                TreeNode imageOffsets = nodeLimb.Nodes.Add("Image Offsets:");
                foreach (ushort imageOffset in limb.ImageOffsets)
                {
                    imageOffsets.Nodes.Add(string.Format("Offset: {0}", imageOffset));
                }
            }
            Limbs.ExpandAll();

            //Pictures
            int pictureCount = 0;
            Pictures.Items.Clear();
            foreach (CostumeImageData picture in _costumeBlock.Pictures)
            {
                ListViewItem item = Pictures.Items.Add(pictureCount.ToString());
                item.SubItems.Add(picture.Width.ToString());
                item.SubItems.Add(picture.Height.ToString());
                item.SubItems.Add(picture.RelX.ToString());
                item.SubItems.Add(picture.RelY.ToString());
                item.SubItems.Add(picture.MoveX.ToString());
                item.SubItems.Add(picture.MoveY.ToString());
                item.SubItems.Add(picture.RedirLimb.ToString());
                item.SubItems.Add(picture.RedirPict.ToString());
                item.SubItems.Add(picture.ImageDataSize.ToString());

                pictureCount++;
            }
            Pictures.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);

            //Palette Entrys
            PaletteTable.Controls.Clear();
            PaletteData defaultPallete = ((DiskBlock) _costumeBlock.Parent).GetROOM().GetDefaultPalette();

            for (int i = 0; i < _costumeBlock.Palette.Count; i++)
            {
                byte realPaletteIndex = _costumeBlock.Palette[i];


                PaletteColor paletteColor = new PaletteColor(i)
                                                {
                                                    BackColor = defaultPallete.Colors[realPaletteIndex],
                                                    Width = 24,
                                                    Height = 24,
                                                    BorderStyle = BorderStyle.FixedSingle,
                                                    FullPalette = defaultPallete,
                                                    CanEdit = true
                                                };

                paletteColor.PaletteIndex = realPaletteIndex;
                paletteColor.PaletteChanged += (sender, index, paletteIndex) =>
                {
                    _costumeBlock.Palette[index] = Convert.ToByte(paletteIndex);
                    PaletteTable.Controls[index].BackColor = defaultPallete.Colors[paletteIndex];
                    ((PaletteColor) PaletteTable.Controls[index]).PaletteIndex = paletteIndex;
                };

                toolTip1.SetToolTip(paletteColor, string.Format("R: {0}, G:{1}, B:{2}", paletteColor.BackColor.R, paletteColor.BackColor.G, paletteColor.BackColor.B));

              PaletteTable.Controls.Add(paletteColor);
            }
        }
    }
}
