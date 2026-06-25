using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScummEditor.Gui;
using ScummEditor.Gui.IndexFile;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Gui
{
    public class TreeNavigatorManager
    {
        private BlockBaseControl _blockBaseControl { get; set; }
        private Dictionary<string, BlockBaseControl> _controlViewers;
        private readonly SpeechSouControl _speechSouControl = new SpeechSouControl();
        private readonly CdAudioSouControl _cdAudioSouControl = new CdAudioSouControl();
        private readonly CharsetV3Control _charsetV3Control = new CharsetV3Control();
        private readonly ScummV3OldSoundControl _v3oldSoundControl = new ScummV3OldSoundControl();
        private readonly OldBundleImageControl _oldBundleImageControl = new OldBundleImageControl();
        private readonly OldBundleCostumeControl _oldBundleCostumeControl = new OldBundleCostumeControl();
        private readonly OldBundleDirectoryControl _oldBundleDirectoryControl = new OldBundleDirectoryControl();
        private readonly OldBundleRoomControl _oldBundleRoomControl = new OldBundleRoomControl();
        private readonly OldBundleObjectControl _oldBundleObjectControl = new OldBundleObjectControl();
        private readonly OldBundleScriptControl _oldBundleScriptControl = new OldBundleScriptControl();
        private readonly V2ExeFontControl _v2ExeFontControl = new V2ExeFontControl();
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
            _controlViewers.Add(typeof(ScummV4RoomBlock).Name, new ScummV4RoomImageControl());
            // The byte-preserved blocks (v4-v6 NotImplementedDataBlock and the v7 generic blocks) all
            // share the hex viewer so their raw content is shown instead of just the generic header.
            var rawBlockControl = new NotImplementedDataBlockControl();
            _controlViewers.Add(typeof(NotImplementedDataBlock).Name, rawBlockControl);
            _controlViewers.Add(typeof(RawContainerBlock).Name, rawBlockControl);
            _controlViewers.Add(typeof(RawDataBlock).Name, rawBlockControl);
            // The v7 index meta blocks (RNAM/MAXS/DOBJ/AARY/ANAM) are RawIndexBlock; decode them for a
            // friendly view instead of the raw hex dump.
            _controlViewers.Add(typeof(RawIndexBlock).Name, new V7IndexBlockControl());
            _controlViewers.Add(typeof(RoomOffsetTable).Name, new RoomOffsetTableControl());
            _controlViewers.Add(typeof(ZPlane).Name, new ZPlaneControl());
            _controlViewers.Add(typeof(ObjectImageHeader).Name, new ObjectImageHeaderControl());
            _controlViewers.Add(typeof(Costume).Name, new CostumeControl());
            _controlViewers.Add(typeof(CostumeAkos).Name, new AkosCostumeControl()); // v7 AKOS costumes
            _controlViewers.Add(typeof(ImageBomp).Name, new ImageBompControl());

            var structuredBlockControl = new StructuredBlockControl();
            _controlViewers.Add(typeof(BoxData).Name, structuredBlockControl);
            _controlViewers.Add(typeof(BoxMatrix).Name, structuredBlockControl);
            _controlViewers.Add(typeof(Scale).Name, structuredBlockControl);
            _controlViewers.Add(typeof(PaletteOffset).Name, structuredBlockControl);
            _controlViewers.Add(typeof(EgaPalette).Name, structuredBlockControl);
            // v4 room sub-blocks (M2)
            _controlViewers.Add(typeof(BoxDataV4).Name, structuredBlockControl);
            _controlViewers.Add(typeof(ScaleV4).Name, structuredBlockControl);
            _controlViewers.Add(typeof(EgaShadowPaletteV4).Name, structuredBlockControl);
            _controlViewers.Add(typeof(ColorCyclesV4).Name, structuredBlockControl);
            _controlViewers.Add(typeof(LocalScriptCountV4).Name, structuredBlockControl);
            _controlViewers.Add(typeof(LocalObjectListV4).Name, structuredBlockControl);
            _controlViewers.Add(typeof(SoundListV4).Name, structuredBlockControl);

            _controlViewers.Add(typeof(ObjectCode).Name, new ObjectCodeControl());

            _controlViewers.Add(typeof(SoundBlock).Name, new SoundBlockControl());

            var scriptControl = new ScriptControl();
            _controlViewers.Add(typeof(ScriptBlock).Name, scriptControl);
            _controlViewers.Add(typeof(ScriptBlockV4).Name, scriptControl); // v4 SC/LS/EX/EN scripts
            _controlViewers.Add(typeof(CostumeV4).Name, new CostumeV4Control()); // v4 CO costumes
            _controlViewers.Add(typeof(SoundBlockV4).Name, new SoundBlockV4Control()); // v4 SO sound

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
            _controlViewers.Add(typeof(RoomNamesV5V6).Name, indexDetailsControl);

            _treeView = treeView;
            _displayPanel = displayPanel;
            _treeView.AfterSelect += AfterNodeSelectedEvent;
        }

        public ScummGameData GameData { get; set; }

        public void LoadTree()
        {
            // BeginUpdate/EndUpdate suppress per-node repaints while the tree is populated. This is
            // essential for v7 (The Dig, Full Throttle): their data files are tens of megabytes and
            // produce many thousands of block nodes.
            _treeView.BeginUpdate();
            try
            {
                BuildTree();
            }
            finally
            {
                _treeView.EndUpdate();
            }
        }

        private void BuildTree()
        {
            _treeView.Nodes.Clear();

            var v4Index = GameData.IndexFile as ScummV4IndexFile;
            if (v4Index != null)
            {
                CreateScummV4IndexFileTree(v4Index);
            }
            else if (GameData.IndexFile is ScummV3OldBundleIndexFile)
            {
                CreateOldBundleIndexTree((ScummV3OldBundleIndexFile)GameData.IndexFile);
            }
            else if (GameData.IndexFile is ScummV7IndexFile)
            {
                CreateScummV7IndexFileTree((ScummV7IndexFile)GameData.IndexFile);
            }
            else if (GameData.IndexFile != null)
            {
                CreateScummIndexFileTree(GameData.IndexFile);
            }

            // One data node per container (v4 games are spread over several DISKnn.LEC disks).
            if (GameData.DataDisks != null && GameData.DataDisks.Count > 0)
            {
                // v2 / v3 old-bundle rooms are raw NN.LFL files with no real block tags; they are reified
                // into a synthetic BlockBase tree (OldBundleBlockBuilder) so the SAME block walker the
                // other engines use renders them - the tree looks like v4-v6 (RO / HD / BM / OI / OC / ...).
                var oldBundleIndex = GameData.IndexFile as ScummV3OldBundleIndexFile;
                foreach (DataDisk disk in GameData.DataDisks)
                {
                    var oldBundleRoom = disk.Tree as ScummV3OldBundleDataFile;
                    if (oldBundleRoom != null && oldBundleIndex != null)
                    {
                        CreateOldBundleFileTree(oldBundleRoom, disk.FilePath);
                    }
                    else
                    {
                        CreateScummDataFileTree(disk.Tree, System.IO.Path.GetFileName(disk.FilePath));
                    }
                }
            }
            else if (GameData.DataFile != null)
            {
                CreateScummDataFileTree(GameData.DataFile, "Data File");
            }

            CreateFontFileNodes();
            CreateV3FontNodes();
            CreateV2ExeFontNode();
            CreateSouFileNodes(GameData.LoadedGameInfo);
        }

        /// <summary>
        /// Browsable index tree for a v2 / v3 old-bundle game: the four resource directories as synthetic
        /// 0R / 0S / 0N / 0C blocks, mirroring how the v4 index exposes its directory blocks.
        /// </summary>
        private void CreateOldBundleIndexTree(ScummV3OldBundleIndexFile index)
        {
            TreeNode indexNode = _treeView.Nodes.Add("IndexFile", "Index File (00.LFL)");
            foreach (BlockBase block in OldBundleBlockBuilder.BuildIndexBlocks(index, GameData.LoadedGameInfo))
            {
                CreateNode(block, indexNode);
            }
        }

        /// <summary>
        /// One file node per v2 / v3 old-bundle room (NN.LFL), reified into a synthetic BlockBase tree
        /// (RO / HD / BM / OI / OC / EN / EX / LS, plus file-level SC / CO / SO) so the standard block
        /// walker renders it like v4-v6. A parse fault degrades to a labelled inert child, not a failed load.
        /// </summary>
        private void CreateOldBundleFileTree(ScummV3OldBundleDataFile dataFile, string filePath)
        {
            int roomNo;
            if (!int.TryParse(System.IO.Path.GetFileNameWithoutExtension(filePath), out roomNo)) roomNo = -1;

            string label = System.IO.Path.GetFileName(filePath);
            if (roomNo >= 0) label += " (Room " + roomNo + ")";
            TreeNode fileNode = _treeView.Nodes.Add(label, label);

            try
            {
                OldBundleBlockBuilder.BuildFileBlocks(GameData, dataFile, roomNo);
                WalkChildren(dataFile, fileNode);
            }
            catch (Exception ex)
            {
                fileNode.Nodes.Add(new TreeNode("(could not parse room: " + ex.Message + ")"));
            }
        }

        /// <summary>Root node for the v2 EXE-embedded font (MANIAC.EXE / ZAK.EXE); the V2ExeFont viewer handles it.</summary>
        private void CreateV2ExeFontNode()
        {
            if (GameData.LoadedGameInfo == null || GameData.LoadedGameInfo.ScummVersion > 2) return;
            string dataFile = GameData.LoadedGameInfo.DataFile;
            if (string.IsNullOrEmpty(dataFile)) return;

            string exePath = ScummV2ExeFontCodec.FindGameExe(System.IO.Path.GetDirectoryName(dataFile));
            if (exePath == null) return;

            // FindGameExe falls back to the first .exe in the folder; only show the node when that file
            // really contains the v2 font signature, so an unrelated executable is not mislabelled as a font.
            try { if (ScummV2ExeFont.Locate(System.IO.File.ReadAllBytes(exePath)) < 0) return; }
            catch { return; }

            var node = _treeView.Nodes.Add("FontExe", "Font (" + System.IO.Path.GetFileName(exePath) + ", in EXE)");
            node.Tag = new V2ExeFontRef { ExePath = exePath };
        }

        /// <summary>Root nodes for the standalone v3 charset files (9N.LFL); the CharsetV3 viewer handles them.</summary>
        private void CreateV3FontNodes()
        {
            if (GameData.V3Charsets == null) return;

            foreach (CharsetV3 charset in GameData.V3Charsets)
            {
                string fileName = charset.FilePath != null ? System.IO.Path.GetFileName(charset.FilePath) : "9N.LFL";
                var node = _treeView.Nodes.Add("FontV3", "Font (" + fileName + ")");
                node.Tag = charset;
            }
        }

        /// <summary>Root nodes for the standalone font files (v4 90x.LFL); the Charset viewer handles them.</summary>
        private void CreateFontFileNodes()
        {
            if (GameData.Fonts == null) return;

            foreach (FontResource font in GameData.Fonts)
            {
                var node = _treeView.Nodes.Add("Font",
                    "Font (" + System.IO.Path.GetFileName(font.FilePath) + ")");
                node.Tag = font.Charset;
            }
        }

        /// <summary>Index tree for SCUMM v4: a flat list of the index blocks (RN, 0R, 0S, ...).</summary>
        private void CreateScummV4IndexFileTree(ScummV4IndexFile indexFile)
        {
            var node = _treeView.Nodes.Add("IndexFile", "Index File");
            foreach (BlockBase block in indexFile.Blocks)
            {
                CreateNode(block, node);
            }
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

        private void CreateScummDataFileTree(ScummDataFile dataFile, string label)
        {
            TreeNode dataNode = _treeView.Nodes.Add(label, label);

            LoadNextBlock(dataFile, dataNode);
        }

        private TreeNode LoadNextBlock(BlockBase blockBase, TreeNode parentNode, int nodeIndex = -1)
        {
            TreeNode blockNode = CreateNode(blockBase, parentNode, nodeIndex);
            WalkChildren(blockBase, blockNode);
            return blockNode;
        }

        /// <summary>
        /// Adds a block's children under <paramref name="blockNode"/>, grouping same-tag siblings into an
        /// indexed series (e.g. "OC 000", "OC 001"). Used both by LoadNextBlock and by the old-bundle file
        /// tree (whose file node walks its children directly, with no extra container block).
        /// </summary>
        private void WalkChildren(BlockBase blockBase, TreeNode blockNode)
        {
            // For readability the v4 local scripts (LS) are shown nested under the local-script count
            // block (LC), which declares how many of them the room has (LC precedes the LS blocks in
            // the room). This only nests the tree NODES - LC and LS stay siblings in the block model,
            // so saving is unchanged. v5/v6 use the tags NLSC/LSCR, so this never triggers there.
            TreeNode localScriptCountNode = null;

            IEnumerable<IGrouping<string, BlockBase>> groupedChildrens = blockBase.Childrens.GroupBy(g => g.BlockType);
            foreach (IGrouping<string, BlockBase> groupedChildren in groupedChildrens)
            {
                TreeNode groupParent = (groupedChildren.Key == "LS" && localScriptCountNode != null)
                    ? localScriptCountNode
                    : blockNode;

                if (groupedChildren.Count() > 1)
                {
                    int counter = 0;
                    foreach (BlockBase child in groupedChildren)
                    {
                        LoadNextBlock(child, groupParent, counter);
                        counter++;
                    }
                }
                else
                {
                    foreach (BlockBase child in groupedChildren)
                    {
                        TreeNode childNode = LoadNextBlock(child, groupParent);
                        if (child.BlockType == "LC")
                        {
                            localScriptCountNode = childNode;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Index tree for SCUMM v7 (The Dig, Full Throttle): the raw RNAM/MAXS blocks, the five typed
        /// resource directories, then the raw DOBJ/AARY and the v7-only ANAM (audio names). The raw
        /// blocks are kept verbatim, so they are shown with the generic block viewer.
        /// </summary>
        private void CreateScummV7IndexFileTree(ScummV7IndexFile index)
        {
            var node = _treeView.Nodes.Add("IndexFile", "Index File");

            CreateNode(index.RawRNAM, node);
            CreateNode(index.RawMAXS, node);
            CreateNode(index.DROO, node);
            CreateNode(index.DSCR, node);
            CreateNode(index.DSOU, node);
            CreateNode(index.DCOS, node);
            CreateNode(index.DCHR, node);
            CreateNode(index.RawDOBJ, node);
            CreateNode(index.RawAARY, node);
            CreateNode(index.RawANAM, node);
        }

        private void CreateScummIndexFileTree(ScummIndexFile scummV6IndexFile)
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

            // v3 charsets (9N.LFL) are standalone font files, not BlockBase, so they get their own viewer.
            var charsetV3 = e.Node.Tag as CharsetV3;
            if (charsetV3 != null)
            {
                _charsetV3Control.SetData(charsetV3);
                _displayPanel.Controls.Add(_charsetV3Control);
                _charsetV3Control.Dock = DockStyle.Fill;
                return;
            }

            // v2 EXE-embedded font (MANIAC.EXE / ZAK.EXE), not part of the LFL data.
            var v2ExeFont = e.Node.Tag as V2ExeFontRef;
            if (v2ExeFont != null)
            {
                _v2ExeFontControl.SetData(v2ExeFont);
                _displayPanel.Controls.Add(_v2ExeFontControl);
                _v2ExeFontControl.Dock = DockStyle.Fill;
                return;
            }

            // v2 / v3 old-bundle synthetic blocks: the tree is a real BlockBase tree (walked like v4-v6);
            // route each leaf by its kind to the matching old-bundle viewer.
            var oldBundleBlock = e.Node.Tag as OldBundleBlock;
            if (oldBundleBlock != null)
            {
                ShowOldBundleBlock(oldBundleBlock);
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

        /// <summary>Routes a v2 / v3 old-bundle block to the matching viewer by its kind.</summary>
        private void ShowOldBundleBlock(OldBundleBlock block)
        {
            switch (block.Kind)
            {
                case OldBundleNodeKind.Header:
                    ShowOldBundleControl(_oldBundleRoomControl, c => c.SetData(block));
                    break;
                case OldBundleNodeKind.Image:
                    ShowOldBundleControl(_oldBundleImageControl, c => c.SetData(block));
                    break;
                case OldBundleNodeKind.Object:
                    ShowOldBundleControl(_oldBundleObjectControl, c => c.SetData(block));
                    break;
                case OldBundleNodeKind.Script:
                    ShowOldBundleControl(_oldBundleScriptControl, c => c.SetData(block));
                    break;
                case OldBundleNodeKind.Directory:
                    ShowOldBundleControl(_oldBundleDirectoryControl, c => c.SetData(block));
                    break;
                case OldBundleNodeKind.Costume:
                    ShowOldBundleControl(_oldBundleCostumeControl, c => c.SetData(block));
                    break;
                case OldBundleNodeKind.Sound:
                    var soundRef = new V3OldSoundRef
                    {
                        DataFile = block.DataFile,
                        Index = GameData.IndexFile as ScummV3OldBundleIndexFile,
                        RoomNo = block.RoomNo,
                        Offset = block.Offset
                    };
                    ShowOldBundleControl(_v3oldSoundControl, c => c.SetData(soundRef));
                    break;
                default: // Room container and any other: show the generic block info.
                    ShowOldBundleControl(_blockBaseControl, c => c.SetAndRefreshData(block));
                    break;
            }
        }

        /// <summary>Loads data into one of the shared old-bundle viewers and shows it docked-fill in the panel.</summary>
        private void ShowOldBundleControl<T>(T control, Action<T> setData) where T : Control
        {
            setData(control);
            _displayPanel.Controls.Add(control);
            control.Dock = DockStyle.Fill;
        }

    }
}