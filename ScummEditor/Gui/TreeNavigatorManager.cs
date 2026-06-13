using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Gui;
using ScummEditor.Gui.IndexFile;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;
using ScummEditor.Structures.IndexFile;

namespace ScummEditor.Gui
{
    public class TreeNavigatorManager
    {
        private BlockBaseControl _blockBaseControl { get; set; }
        private Dictionary<string, BlockBaseControl> _controlViewers;
        private readonly SpeechSouControl _speechSouControl = new SpeechSouControl();
        private readonly CdAudioSouControl _cdAudioSouControl = new CdAudioSouControl();
        private readonly TreeView _treeView;
        private readonly Panel _displayPanel;

        public TreeNavigatorManager(TreeView treeView, Panel displayPanel)
        {
            _blockBaseControl = new BlockBaseControl();

            _controlViewers = new Dictionary<string, BlockBaseControl>();
            _controlViewers.Add(typeof(BlockBase).Name, _blockBaseControl);
            _controlViewers.Add(typeof(PaletteData).Name, new PaletteDataControl());
            _controlViewers.Add(typeof(ColorCycles).Name, new ColorCycleControl());
            _controlViewers.Add(typeof(ImageStripTable).Name, new ImageStripTableControl());
            _controlViewers.Add(typeof(ValuePaddingBlock).Name, new ValuePaddingBlockControl());
            _controlViewers.Add(typeof(RoomImageHeader).Name, new RoomImageHeaderControl());
            _controlViewers.Add(typeof(RoomHeader).Name, new RoomHeaderControl());
            _controlViewers.Add(typeof(DiskBlock).Name, new DiskBlockControl());
            _controlViewers.Add(typeof(NotImplementedDataBlock).Name, new NotImplementedDataBlockControl());
            _controlViewers.Add(typeof(RoomOffsetTable).Name, new RoomOffsetTableControl());
            _controlViewers.Add(typeof(ZPlane).Name, new ZPlaneControl());
            _controlViewers.Add(typeof(ObjectImageHeader).Name, new ObjectImageHeaderControl());
            _controlViewers.Add(typeof(Costume).Name, new CostumeControl());
            _controlViewers.Add(typeof(ImageBomp).Name, new ImageBompControl());

            var structuredBlockControl = new StructuredBlockControl();
            _controlViewers.Add(typeof(BoxData).Name, structuredBlockControl);
            _controlViewers.Add(typeof(BoxMatrix).Name, structuredBlockControl);
            _controlViewers.Add(typeof(Scale).Name, structuredBlockControl);
            _controlViewers.Add(typeof(PaletteOffset).Name, structuredBlockControl);
            _controlViewers.Add(typeof(EgaPalette).Name, structuredBlockControl);

            _controlViewers.Add(typeof(ObjectCode).Name, new ObjectCodeControl());

            _controlViewers.Add(typeof(SoundBlock).Name, new SoundBlockControl());

            var scriptControl = new ScriptControl();
            _controlViewers.Add(typeof(ScriptBlock).Name, scriptControl);

            _controlViewers.Add(typeof(Charset).Name, new CharsetControl());

            var directoryOfItemsControlGeneric = new DirectoryOfItemsControl();
            _controlViewers.Add(typeof(DirectoryOfItems).Name, directoryOfItemsControlGeneric);
            _controlViewers.Add(typeof(DirectoryOfRooms).Name, new DirectoryOfRoomsControl());
            _controlViewers.Add(typeof(DirectoryOfCharsets).Name, directoryOfItemsControlGeneric);
            _controlViewers.Add(typeof(DirectoryOfCostumes).Name, directoryOfItemsControlGeneric);
            _controlViewers.Add(typeof(DirectoryOfScripts).Name, directoryOfItemsControlGeneric);
            _controlViewers.Add(typeof(DirectoryOfSounds).Name, directoryOfItemsControlGeneric);

            var indexDetailsControl = new IndexDetailsControl();
            _controlViewers.Add(typeof(MaximumValues).Name, indexDetailsControl);
            _controlViewers.Add(typeof(DirectoryOfObjects).Name, indexDetailsControl);
            _controlViewers.Add(typeof(DirectoryOfArrays).Name, indexDetailsControl);
            _controlViewers.Add(typeof(RoomNamesV6).Name, indexDetailsControl);

            _treeView = treeView;
            _displayPanel = displayPanel;
            _treeView.AfterSelect += AfterNodeSelectedEvent;
        }

        public ScummV6GameData ScummV6GameData { get; set; }

        public void LoadTree()
        {
            _treeView.Nodes.Clear();

            if (ScummV6GameData.IndexFile != null) CreateScummIndexFileTree(ScummV6GameData.IndexFile);
            if (ScummV6GameData.DataFile != null) CreateScummDataFileTree(ScummV6GameData.DataFile);
            CreateSouFileNodes(ScummV6GameData.LoadedGameInfo);
        }

        /// <summary>
        /// Root nodes for the standalone audio containers next to the game files: the speech
        /// file (MONSTER.SOU / "game".SOU) and the ripped CD audio (CDDA.SOU). The files are
        /// parsed lazily, when their node is first selected.
        /// </summary>
        private void CreateSouFileNodes(GameInfo gameInfo)
        {
            if (gameInfo == null) return;

            if (gameInfo.SpeechFilePath != null)
            {
                var node = _treeView.Nodes.Add("SpeechFile",
                    "Speech File (" + System.IO.Path.GetFileName(gameInfo.SpeechFilePath) + ")");
                node.Tag = new SpeechSouFile(gameInfo.SpeechFilePath);
            }

            if (gameInfo.CdAudioFilePath != null)
            {
                var node = _treeView.Nodes.Add("CdAudioFile",
                    "CD Audio (" + System.IO.Path.GetFileName(gameInfo.CdAudioFilePath) + ")");
                node.Tag = new CdAudioSouFile(gameInfo.CdAudioFilePath);
            }
        }

        private void CreateScummDataFileTree(ScummV6DataFile dataFile)
        {
            TreeNode dataNode = _treeView.Nodes.Add("DataFile", "Data File");

            LoadNextBlock(dataFile, dataNode);
        }

        private void LoadNextBlock(BlockBase blockBase, TreeNode parentNode, int nodeIndex = -1)
        {
            TreeNode blockNode = CreateNode(blockBase, parentNode, nodeIndex);

            IEnumerable<IGrouping<string, BlockBase>> groupedChildrens = blockBase.Childrens.GroupBy(g => g.BlockType);
            foreach (IGrouping<string, BlockBase> groupedChildren in groupedChildrens)
            {
                if (groupedChildren.Count() > 1)
                {
                    int counter = 0;
                    foreach (BlockBase child in groupedChildren)
                    {
                        LoadNextBlock(child, blockNode, counter);
                        counter++;
                    }
                }
                else
                {
                    foreach (BlockBase child in groupedChildren)
                    {
                        LoadNextBlock(child, blockNode);
                    }
                }
            }
        }

        private void CreateScummIndexFileTree(ScummV6IndexFile scummV6IndexFile)
        {
            var node = _treeView.Nodes.Add("IndexFile", "Index File");

            CreateNode(scummV6IndexFile.RNAM, node);
            CreateNode(scummV6IndexFile.MAXS, node);
            CreateNode(scummV6IndexFile.DROO, node);
            CreateNode(scummV6IndexFile.DSCR, node);
            CreateNode(scummV6IndexFile.DSOU, node);
            CreateNode(scummV6IndexFile.DCOS, node);
            CreateNode(scummV6IndexFile.DCHR, node);
            CreateNode(scummV6IndexFile.DOBJ, node);
            if (scummV6IndexFile.AARY != null) CreateNode(scummV6IndexFile.AARY, node);
        }

        private static TreeNode CreateNode(BlockBase blockBase, TreeNode parentNode, int index = -1)
        {
            string nodeText = blockBase.BlockType;
            if (index >= 0)
            {
                nodeText += " " + index.ToString().PadLeft(3, '0');
            }

            var node = new TreeNode(nodeText)
                           {
                               Tag = blockBase
                           };
            parentNode.Nodes.Add(node);
            return node;
        }


        private void AfterNodeSelectedEvent(object sender, TreeViewEventArgs e)
        {
            _displayPanel.Controls.Clear();
            if (e.Node.Tag == null) return;

            // The audio container nodes carry their own (non-block) objects and viewers.
            var speechFile = e.Node.Tag as SpeechSouFile;
            if (speechFile != null)
            {
                _speechSouControl.SetData(speechFile);
                _displayPanel.Controls.Add(_speechSouControl);
                _speechSouControl.Dock = DockStyle.Fill;
                return;
            }

            var cdAudioFile = e.Node.Tag as CdAudioSouFile;
            if (cdAudioFile != null)
            {
                _cdAudioSouControl.SetData(cdAudioFile);
                _displayPanel.Controls.Add(_cdAudioSouControl);
                _cdAudioSouControl.Dock = DockStyle.Fill;
                return;
            }

            var item = (BlockBase)e.Node.Tag;

            string name = item.GetType().Name;



            if (_controlViewers.ContainsKey(name))
            {
                _controlViewers[name].SetAndRefreshData(item);
                _displayPanel.Controls.Add(_controlViewers[name]);
                _controlViewers[name].Dock = DockStyle.Fill;
            }
            else
            {
                _blockBaseControl.SetAndRefreshData(item);
                _displayPanel.Controls.Add(_blockBaseControl);
                _blockBaseControl.Dock = DockStyle.Fill;
            }
        }

    }
}