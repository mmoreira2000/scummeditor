using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Exceptions;
using ScummEditor.Structures.DataFile;
using ScummEditor.Structures.IndexFile;
using System.Diagnostics;

namespace ScummEditor.Structures
{
    public enum ScummGame
    {
        None = 0,
        SamAndMax = 1,
        DayOfTheTentacle = 2,
        FateOfAtlantis = 3,
        MonkeyIsland1VGA= 4,
        MonkeyIsland2 = 5,
        MonkeyIsland1VGASpeech = 6
    }

    public class GameInfo
    {
        public int ScummVersion { get; set; }
        public ScummGame LoadedGame { get; set; }
        public bool Xored { get; set; }
        public int XorKey { get; set; }

        public string IndexFile { get; set; }
        public string DataFile { get; set; }
    }

    public class ScummV6GameData
    {
        public GameInfo LoadedGameInfo { get; set; }

        public ScummV6IndexFile IndexFile { get; set; }
        public ScummV6DataFile DataFile { get; set; }

        public void LoadDataFromDisc(string filePath)
        {
            LoadedGameInfo = Functions.FindScummGame(filePath);
            if (LoadedGameInfo.LoadedGame == ScummGame.None)
            {
                return;
            }

            //var fileIndex = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".000");
            //var fileData = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".001");

            var fileStream = new XoredFileStream(LoadedGameInfo.XorKey, LoadedGameInfo.IndexFile, FileMode.Open, FileAccess.Read);

            LoadIndexFromBinaryReader(fileStream);
            fileStream.Close();

            fileStream = new XoredFileStream(LoadedGameInfo.XorKey, LoadedGameInfo.DataFile, FileMode.Open, FileAccess.Read);

            LoadDataFromBinaryReader(fileStream);
            fileStream.Close();

            LinkDataAndIndexFile();
        }

        private void LinkDataAndIndexFile()
        {
            var diskBlocks = DataFile.GetLFLFs();
            foreach (DiskBlock diskBlock in diskBlocks)
            {
                List<DirectoryItem> matchRooms = IndexFile.DROO.Rooms.Where(x => x.Offset == diskBlock.BlockOffSet).ToList();
                matchRooms.ForEach(r => r.ItemId = diskBlock.UniqueId);

                long roomOffset = diskBlock.GetROOM().BlockOffSet;

                List<BlockBase> scripts = diskBlock.Childrens.Where(s => s.BlockType == "SCRP").ToList();
                foreach (var script in scripts)
                {
                    List<DirectoryItem> matchScripts = IndexFile.DSCR.Rooms.Where(x => x.Offset == (script.BlockOffSet - roomOffset)).ToList();
                    matchScripts.ForEach(r => r.ItemId = script.UniqueId);
                }

                List<BlockBase> sounds = diskBlock.Childrens.Where(s => s.BlockType == "SOUN").ToList();
                foreach (var sound in sounds)
                {
                    List<DirectoryItem> matchSounds = IndexFile.DSOU.Rooms.Where(x => x.Offset == (sound.BlockOffSet - roomOffset)).ToList();
                    matchSounds.ForEach(r => r.ItemId = sound.UniqueId);
                }

                List<BlockBase> costumes = diskBlock.Childrens.Where(s => s.BlockType == "COST").ToList();
                foreach (var costume in costumes)
                {
                    List<DirectoryItem> matchCostumes = IndexFile.DCOS.Rooms.Where(x => x.Offset == (costume.BlockOffSet - roomOffset)).ToList();
                    matchCostumes.ForEach(r => r.ItemId = costume.UniqueId);
                }

                List<BlockBase> characters = diskBlock.Childrens.Where(s => s.BlockType == "CHAR").ToList();
                foreach (var character in characters)
                {
                    List<DirectoryItem> matchChars = IndexFile.DCHR.Rooms.Where(x => x.Offset == (character.BlockOffSet - roomOffset)).ToList();
                    matchChars.ForEach(r => r.ItemId = character.UniqueId);
                }
            }
        }

        public void SaveDataToDisk()
        {
            PostProcessChanges();

            var fileIndex = Path.Combine(LoadedGameInfo.IndexFile);
            var fileData = Path.Combine(LoadedGameInfo.DataFile);

            var x2 = new XoredFileStream(LoadedGameInfo.XorKey, fileIndex, FileMode.Create, FileAccess.Write);
            SaveIndexToBinaryWriter(x2);
            x2.Flush();
            x2.Close();

            x2 = new XoredFileStream(LoadedGameInfo.XorKey, fileData, FileMode.Create, FileAccess.Write);
            SaveDataToBinaryWriter(x2);
            x2.Flush();
            x2.Close();
        }

        public void LoadDataFromBinaryReader(Stream binaryReader)
        {
            DataFile = new ScummV6DataFile(null, LoadedGameInfo);
            DataFile.LoadFromBinaryReader(binaryReader);
        }

        public void SaveDataToBinaryWriter(Stream binaryWriter)
        {
            DataFile.SaveToBinaryWriter(binaryWriter);
        }

        public void LoadIndexFromBinaryReader(Stream binaryReader)
        {
            IndexFile = new ScummV6IndexFile(LoadedGameInfo);
            IndexFile.LoadFromBinaryReader(binaryReader);
        }

        public void SaveIndexToBinaryWriter(Stream binaryWriter)
        {
            IndexFile.SaveToBinaryWriter(binaryWriter);
        }

        public void PostProcessChanges()
        {
            DataFile.CalculateBlockSize();
            DataFile.CalculateOffsets();

            RoomOffsetTable LOFF = DataFile.GetLOFF();
            List<DiskBlock> diskBlocks = DataFile.GetLFLFs();

            if (diskBlocks.Count != LOFF.Rooms.Count) throw new InvalidFileFormatException("Número de rooms não bate com o de LFLFs");

            //Update ROOM positions offset.
            var orderedDisks = OrderDiskBlocks();
            foreach (RoomOffsetTableItem offsetTableItem in LOFF.Rooms)
            {
                offsetTableItem.OffSet = (uint)orderedDisks[offsetTableItem.Id].Childrens.Single(b => b.GetType() == typeof(RoomBlock)).BlockOffSet;
            }

            foreach (DiskBlock diskBlock in diskBlocks)
            {
                long roomOffset = diskBlock.GetROOM().BlockOffSet;

                List<BlockBase> scripts = diskBlock.Childrens.Where(s => s.BlockType == "SCRP").ToList();
                foreach (var script in scripts)
                {
                    List<DirectoryItem> matchScripts = IndexFile.DSCR.Rooms.Where(x => x.ItemId == script.UniqueId).ToList();
                    matchScripts.ForEach(r => r.Offset = (uint)(script.BlockOffSet - roomOffset));
                }

                List<BlockBase> sounds = diskBlock.Childrens.Where(s => s.BlockType == "SOUN").ToList();
                foreach (var sound in sounds)
                {
                    List<DirectoryItem> matchSounds = IndexFile.DSOU.Rooms.Where(x => x.ItemId == sound.UniqueId).ToList();
                    matchSounds.ForEach(r => r.Offset = (uint)(sound.BlockOffSet - roomOffset));
                }

                List<BlockBase> costumes = diskBlock.Childrens.Where(s => s.BlockType == "COST").ToList();
                foreach (var costume in costumes)
                {
                    List<DirectoryItem> matchCostumes = IndexFile.DCOS.Rooms.Where(x => x.ItemId == costume.UniqueId).ToList();
                    matchCostumes.ForEach(r => r.Offset = (uint)(costume.BlockOffSet - roomOffset));
                }

                List<BlockBase> characters = diskBlock.Childrens.Where(s => s.BlockType == "CHAR").ToList();
                foreach (var character in characters)
                {
                    List<DirectoryItem> matchChars = IndexFile.DCHR.Rooms.Where(x => x.ItemId == character.UniqueId).ToList();
                    matchChars.ForEach(r => r.Offset = (uint)(character.BlockOffSet - roomOffset));
                }
            }

        }

        private DiskBlock[] OrderDiskBlocks()
        {
            var result = new DiskBlock[IndexFile.DROO.Rooms.Count];

            //a númeração de discos é chata, e pode pular alguns rooms.
            //o que essa rotina faz é percorrer todos os rooms encontrados
            //e adiciona-los em um array com o mesmo tamanho do Indice de rooms,
            //deixando nulo os indices dos rooms que não estão em uso (number 0 no arquivo de indice).
            //Dessa forma fica muito mais facil atualizar a tabela de offsets de indices.
            List<DiskBlock> diskBlocks = DataFile.GetLFLFs();

            int nextRoomIndex = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (IndexFile.DROO.Rooms[i].Number == 1)
                {
                    result[i] = diskBlocks[nextRoomIndex];
                    nextRoomIndex++;
                }
            }
            return result;
        }
    }
}