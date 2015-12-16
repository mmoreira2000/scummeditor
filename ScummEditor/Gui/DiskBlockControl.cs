using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ScummEditor.Encoders;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Gui
{
    public partial class DiskBlockControl : BlockBaseControl
    {
        private Dictionary<string, RoomBlockImageControl> _roomImages;
        private Dictionary<string, ClassCreateInfo> CreateInfos { get; set; }
        private DiskBlock _diskBlock;
        private bool _loading;

        public DiskBlockControl()
        {
            InitializeComponent();
        }

        private void CreateOrShowControl(string id)
        {
            splitContainer1.Panel2.Controls.Clear();

            RoomBlock roomBlock = (RoomBlock)_diskBlock.Childrens.Single(r => r.GetType() == typeof(RoomBlock));

            if (!_roomImages.ContainsKey(id))
            {
                ClassCreateInfo createInfo = CreateInfos[id];
                switch (createInfo.ImageType)
                {
                    case ImageType.Background:
                        var backgroundImageControl = new RoomBlockImageControl(roomBlock, ImageType.Background);

                        backgroundImageControl.Visible = true;
                        backgroundImageControl.Dock = DockStyle.Fill;
                        backgroundImageControl.DecodeTransparent = DecodeTransparent.Checked;
                        backgroundImageControl.PaletteIndex = Palettes.SelectedIndex;
                        backgroundImageControl.Decode();
                        _roomImages.Add(createInfo.ControlId, backgroundImageControl);

                        break;
                    case ImageType.ZPlane:
                        var zplaneImageControl = new RoomBlockImageControl(roomBlock, ImageType.ZPlane, createInfo.ZPlaneIndex);
                        zplaneImageControl.Visible = true;
                        zplaneImageControl.Dock = DockStyle.Fill;
                        zplaneImageControl.DecodeTransparent = DecodeTransparent.Checked;
                        zplaneImageControl.PaletteIndex = Palettes.SelectedIndex;
                        zplaneImageControl.Decode();
                        _roomImages.Add(createInfo.ControlId, zplaneImageControl);

                        break;
                    case ImageType.Object:
                        var objectImageControl = new RoomBlockImageControl(roomBlock, ImageType.Object, createInfo.ObjectIndex, createInfo.ImageIndex);
                        objectImageControl.Visible = true;
                        objectImageControl.Dock = DockStyle.Fill;
                        objectImageControl.DecodeTransparent = DecodeTransparent.Checked;
                        objectImageControl.PaletteIndex = Palettes.SelectedIndex;
                        objectImageControl.Decode();
                        _roomImages.Add(createInfo.ControlId, objectImageControl);

                        break;
                    case ImageType.ObjectsZPlane:
                        var zPlaneImageControl = new RoomBlockImageControl(roomBlock, ImageType.ObjectsZPlane, createInfo.ObjectIndex, createInfo.ImageIndex, createInfo.ZPlaneIndex);
                        zPlaneImageControl.Visible = true;
                        zPlaneImageControl.Dock = DockStyle.Fill;
                        zPlaneImageControl.DecodeTransparent = DecodeTransparent.Checked;
                        zPlaneImageControl.PaletteIndex = Palettes.SelectedIndex;
                        zPlaneImageControl.Decode();
                        _roomImages.Add(createInfo.ControlId, zPlaneImageControl);

                        break;

                    case ImageType.Costume:
                        var costumeFrameControl = new RoomBlockImageControl(roomBlock, createInfo.Costume, ImageType.Costume, createInfo.ImageIndex);
                        costumeFrameControl.Visible = true;
                        costumeFrameControl.Dock = DockStyle.Fill;
                        costumeFrameControl.DecodeTransparent = DecodeTransparent.Checked;
                        costumeFrameControl.PaletteIndex = Palettes.SelectedIndex;
                        costumeFrameControl.Decode();
                        _roomImages.Add(createInfo.ControlId, costumeFrameControl);
                        break;
                }
            }

            splitContainer1.Panel2.Controls.Add(_roomImages[id]);
        }
        public override void SetAndRefreshData(Structures.BlockBase blockBase)
        {
            base.SetAndRefreshData(blockBase);

            if (ReferenceEquals(_diskBlock, blockBase)) return;

            _loading = true;
            
            _diskBlock = (DiskBlock)blockBase;

            var roomBlock = (RoomBlock)_diskBlock.Childrens.Single(r => r.GetType() == typeof(RoomBlock));

            //Configurando as palettas Padrão
            PalettesData pals = roomBlock.GetPALS();
            List<PaletteData> paletteDatas = null;
            if (pals != null)
            {
                paletteDatas = pals.GetWRAP().GetAPALs();
            }
            Palettes.Items.Clear();
            if (paletteDatas != null)
            {
                for (int i = 0; i < paletteDatas.Count; i++)
                {
                    Palettes.Items.Add("Palette #" + i);
                }
            }
            else
            {
                Palettes.Items.Add("Palette #0");
            }
            if (Palettes.SelectedIndex < 0) Palettes.SelectedIndex = 0;
            Palettes.Visible = Palettes.Items.Count != 1;
            //Termino de configuração das palettas padrão

            TreeImages.Nodes.Clear();
            _roomImages = new Dictionary<string, RoomBlockImageControl>();
            CreateInfos = new Dictionary<string, ClassCreateInfo>();

            TreeNode backgroundNode = null;

            RoomImage RMIM = roomBlock.GetRMIM();
            if (RMIM.GetIM00().GetSMAP().Strips.Count > 0)
            {
                var createInfo = new ClassCreateInfo();
                createInfo.ImageType = ImageType.Background;
                createInfo.ControlId = "Background";
                CreateInfos.Add("Background", createInfo);

                backgroundNode = TreeImages.Nodes.Add("Background", "Room Background");
            }

            List<ZPlane> zPlanes = RMIM.GetIM00().GetZPlanes();
            for (int i = 0; i < zPlanes.Count; i++)
            {
                string planeKey = "Background ZPlane " + (i + 1);

                var createInfo = new ClassCreateInfo();
                createInfo.ImageType = ImageType.ZPlane;
                createInfo.ControlId = planeKey;
                createInfo.ZPlaneIndex = i;
                CreateInfos.Add(planeKey, createInfo);

                if (backgroundNode == null)
                {
                    TreeImages.Nodes.Add(planeKey, "Background Z-Plane " + (i + 1));
                }
                else
                {
                    backgroundNode.Nodes.Add(planeKey, "Z-Plane " + (i + 1));
                }
            }

            //Objetos
            List<ObjectImage> OBIMs = roomBlock.GetOBIMs();
            for (int i = 0; i < OBIMs.Count; i++)
            {
                TreeNode nodeObject = TreeImages.Nodes.Add("_object" + i, "Object " + i);

                ObjectImage item = OBIMs[i];
                List<ImageData> IMXX = item.GetIMxx();

                for (int j = 0; j < IMXX.Count; j++)
                {
                    ImageData image = IMXX[j];

                    string objectImageKey = string.Format("Object #{0}-{1}", i, j);
                    TreeNode nodeImage = nodeObject.Nodes.Add(objectImageKey, "Image " + j);

                    var createInfo = new ClassCreateInfo();
                    createInfo.ImageType = ImageType.Object;
                    createInfo.ControlId = objectImageKey;
                    createInfo.ObjectIndex = i;
                    createInfo.ImageIndex = j;
                    CreateInfos.Add(objectImageKey, createInfo);

                    List<ZPlane> objectZPlanes = image.GetZPlanes();
                    for (int k = 0; k < objectZPlanes.Count; k++)
                    {
                        string objectZPlaneKey = string.Format("Object #{0}-{1} ZPlane {2}", i, j, (k + 1));

                        nodeImage.Nodes.Add(objectZPlaneKey, "Z-Plane " + k);

                        createInfo = new ClassCreateInfo();
                        createInfo.ImageType = ImageType.ObjectsZPlane;
                        createInfo.ControlId = objectZPlaneKey;
                        createInfo.ObjectIndex = i;
                        createInfo.ImageIndex = j;
                        createInfo.ZPlaneIndex = k;
                        CreateInfos.Add(objectZPlaneKey, createInfo);
                    }
                }

                //Remove os itens se não tiver nenhuma imagem neles, só serve para poluir a tela.
                if (nodeObject.Nodes.Count == 0)
                {
                    TreeImages.Nodes.Remove(nodeObject);
                }
            }

            //Costumes
            List<Costume> costumesList = _diskBlock.Childrens.OfType<Costume>().ToList();

            for (int i = 0; i < costumesList.Count; i++)
            {
                TreeNode costume = TreeImages.Nodes.Add("_costume" + i, string.Format("Costume {0}", i.ToString().PadLeft(3, '0')));

                Costume currentCostume = costumesList[i];
                for (int j = 0; j < currentCostume.Pictures.Count; j++)
                {
                    //Vamos filtras apenas os frames que tem imagem para decodificar.
                    if (currentCostume.Pictures[j].ImageData.Length == 0
                        || currentCostume.Pictures[j].ImageData.Length == 1 && currentCostume.Pictures[j].ImageData[0] == 0) continue;

                    string costumeKey = string.Format("Costume #{0}-{1}", i, j);

                    var createInfo = new ClassCreateInfo();
                    createInfo.ImageType = ImageType.Costume;
                    createInfo.Costume = currentCostume;
                    createInfo.ControlId = costumeKey;
                    createInfo.ImageIndex = j;
                    CreateInfos.Add(costumeKey, createInfo);

                    costume.Nodes.Add(costumeKey, string.Format("Frame {0}", j.ToString().PadLeft(2, '0')));
                }
            }

            TreeImages.ExpandAll();
            TreeImages.SelectedNode = TreeImages.Nodes[0];

            _loading = false;
        }

        private void TreeImages_AfterSelect(object sender, TreeViewEventArgs e)
        {
            splitContainer1.Panel2.Controls.Clear();

            if (e.Node.Name.StartsWith("_object") || e.Node.Name.StartsWith("_costume"))
            {
                if (e.Node.Nodes.Count > 0) CreateOrShowControl(e.Node.Nodes[0].Name);
            }
            else
            {
                CreateOrShowControl(e.Node.Name);
            }
        }

        private void DecodeTransparent_CheckedChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            foreach (KeyValuePair<string, RoomBlockImageControl> roomBlockImageControl in _roomImages)
            {
                roomBlockImageControl.Value.DecodeTransparent = DecodeTransparent.Checked;
                roomBlockImageControl.Value.Decode();
            }
        }

        private void Palettes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_loading) return;
            foreach (KeyValuePair<string, RoomBlockImageControl> roomBlockImageControl in _roomImages)
            {
                roomBlockImageControl.Value.PaletteIndex = Palettes.SelectedIndex;
                roomBlockImageControl.Value.Decode();
            }
        }
    }

    public struct ClassCreateInfo
    {
        public string ControlId { get; set; }
        public Costume Costume { get; set; }
        public ImageType ImageType { get; set; }
        public int ImageIndex { get; set; }
        public int ObjectIndex { get; set; }
        public int ZPlaneIndex { get; set; }
    }
}
