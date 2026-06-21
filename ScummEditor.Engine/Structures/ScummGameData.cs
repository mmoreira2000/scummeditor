using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Structures
{
    /// <summary>
    /// Loads, holds and saves a SCUMM game's data and index files. This is the engine-agnostic
    /// base: it owns the shared load/save flow and defers the version-specific steps (which tree
    /// and index classes to build, how to link the index to the data, how to recompute offsets on
    /// save) to the per-engine subclasses <see cref="ScummGameDataV4"/> and
    /// <see cref="ScummGameDataV5V6"/>. Create instances through the static factory methods, which
    /// pick the right subclass from the detected SCUMM version.
    /// </summary>
    public abstract class ScummGameData
    {
        public GameInfo LoadedGameInfo { get; set; }

        public ScummV5V6IndexFile IndexFile { get; set; }

        /// <summary>The first (or only) data container. For multi-disk v4 games see <see cref="DataDisks"/>.</summary>
        public ScummV5V6DataFile DataFile { get; set; }

        /// <summary>Every loaded data container (one per file). v5/v6 has a single entry.</summary>
        public List<DataDisk> DataDisks { get; private set; } = new List<DataDisk>();

        /// <summary>Standalone font files loaded for the game (v4 90x.LFL); empty for v5/v6.</summary>
        public List<FontResource> Fonts { get; private set; } = new List<FontResource>();

        /// <summary>Standalone v3 charset files (9N.LFL); empty for v4/v5/v6 (which use Fonts/Charset).</summary>
        public List<CharsetV3> V3Charsets { get; private set; } = new List<CharsetV3>();

        /// <summary>Loads the v3 9N.LFL charset files (always plaintext) into V3Charsets.</summary>
        protected void LoadV3Charsets()
        {
            V3Charsets.Clear();
            if (LoadedGameInfo.FontFiles == null)
            {
                return;
            }
            foreach (string path in LoadedGameInfo.FontFiles)
            {
                var charset = new CharsetV3 { FilePath = path };
                charset.LoadFromFileBytes(File.ReadAllBytes(path));
                V3Charsets.Add(charset);
            }
        }

        /// <summary>
        /// Every editable charset of the game, in a stable order: the ones embedded in the data
        /// file (v5/v6) followed by the standalone font files (v4 90x.LFL). Batch font
        /// export/import name files charset_N.png by this order.
        /// </summary>
        public List<Charset> GetAllEditableCharsets()
        {
            List<Charset> charsets = CharsetPngCodec.CollectCharsets(DataFile);
            foreach (FontResource font in Fonts)
            {
                charsets.Add(font.Charset);
            }
            return charsets;
        }

        /// <summary>Creates the right per-engine instance for the detected game (not yet loaded).</summary>
        public static ScummGameData Create(GameInfo gameInfo)
        {
            if (gameInfo != null && gameInfo.ScummVersion == 4)
            {
                return new ScummGameDataV4();
            }
            if (gameInfo != null && gameInfo.ScummVersion == 3)
            {
                return gameInfo.UsesOldBundle
                    ? (ScummGameData)new ScummGameDataV3OldBundle() // Loom EGA, Indy3 EGA, Zak DOS
                    : new ScummGameDataV3Small256();                // Indy3 VGA, Zak FM-Towns
            }
            if (gameInfo != null && gameInfo.ScummVersion == 2)
            {
                // v2 (Maniac/Zak "Enhanced") is the same GF_OLD_BUNDLE container as v3old (XOR 0xFF,
                // [size:u16] room chunks, magic-0x0100 index); only the index object-table stride differs
                // (carried on GameInfo), so the verbatim-round-trip v3old game data serves it directly.
                return new ScummGameDataV3OldBundle();
            }
            if (gameInfo != null && gameInfo.ScummVersion == 1)
            {
                // v1 (Maniac/Zak DOS "classic") is the same GF_OLD_BUNDLE container as v2/v3old (XOR 0xFF,
                // [size:u16] room chunks, verbatim round-trip); only the 00.LFL index layout differs
                // (count-less, hardcoded counts), handled by ScummV3OldBundleIndexFile via UsesClassicIndex.
                return new ScummGameDataV3OldBundle();
            }
            return new ScummGameDataV5V6();
        }

        /// <summary>Detects the game at the given path, then loads it.</summary>
        public static ScummGameData LoadFromDisc(string filePath)
        {
            return LoadFromGameInfo(Functions.FindScummGame(filePath));
        }

        /// <summary>Loads a game already detected by Functions.FindScummGamesInFolder.</summary>
        public static ScummGameData LoadFromGameInfo(GameInfo gameInfo)
        {
            ScummGameData gameData = Create(gameInfo);
            gameData.LoadedGameInfo = gameInfo;
            gameData.LoadDetectedGame();
            return gameData;
        }

        private void LoadDetectedGame()
        {
            if (LoadedGameInfo == null || LoadedGameInfo.LoadedGame == ScummGame.None)
            {
                return;
            }

            var fileStream = new XoredFileStream(LoadedGameInfo.IndexXorKey, LoadedGameInfo.IndexFile, FileMode.Open, FileAccess.Read);
            LoadIndexFromBinaryReader(fileStream);
            fileStream.Close();

            LoadAllDataFiles();

            AfterLoad();
            LinkDataAndIndexFile();
        }

        /// <summary>Loads every data container listed in the game info (one for v5/v6, several for v4).</summary>
        private void LoadAllDataFiles()
        {
            DataDisks.Clear();

            List<string> paths = LoadedGameInfo.DataFiles;
            if (paths == null || paths.Count == 0)
            {
                paths = new List<string> { LoadedGameInfo.DataFile };
            }

            foreach (string path in paths)
            {
                var stream = new XoredFileStream(LoadedGameInfo.XorKey, path, FileMode.Open, FileAccess.Read);
                ScummV5V6DataFile tree = CreateDataFile();
                tree.LoadFromBinaryReader(stream);
                stream.Close();

                DataDisks.Add(new DataDisk { FilePath = path, Tree = tree });
            }

            DataFile = DataDisks[0].Tree;
        }

        public void SaveDataToDisk()
        {
            PostProcessChanges();

            var fileIndex = Path.Combine(LoadedGameInfo.IndexFile);

            var x2 = new XoredFileStream(LoadedGameInfo.IndexXorKey, fileIndex, FileMode.Create, FileAccess.Write);
            SaveIndexToBinaryWriter(x2);
            x2.Flush();
            x2.Close();

            // Write back every data container to its own file (v4 has several disks).
            foreach (DataDisk disk in DataDisks)
            {
                var dataStream = new XoredFileStream(LoadedGameInfo.XorKey, disk.FilePath, FileMode.Create, FileAccess.Write);
                disk.Tree.SaveToBinaryWriter(dataStream);
                dataStream.Flush();
                dataStream.Close();
            }

            // Write back the standalone font files (v4 90x.LFL, plaintext = the charset bytes).
            foreach (FontResource font in Fonts)
            {
                File.WriteAllBytes(font.FilePath, font.Charset.RawContent);
            }

            // Write back the standalone v3 charset files (9N.LFL, always plaintext = the charset bytes).
            foreach (CharsetV3 charset in V3Charsets)
            {
                if (!string.IsNullOrEmpty(charset.FilePath))
                {
                    File.WriteAllBytes(charset.FilePath, charset.RawContent);
                }
            }
        }

        public void PostProcessChanges()
        {
            foreach (DataDisk disk in DataDisks)
            {
                disk.Tree.CalculateBlockSize();
                disk.Tree.CalculateOffsets();
            }

            FixUpIndexOffsets();
        }

        public void LoadDataFromBinaryReader(Stream binaryReader)
        {
            DataFile = CreateDataFile();
            DataFile.LoadFromBinaryReader(binaryReader);
        }

        public void SaveDataToBinaryWriter(Stream binaryWriter)
        {
            DataFile.SaveToBinaryWriter(binaryWriter);
        }

        public void LoadIndexFromBinaryReader(Stream binaryReader)
        {
            IndexFile = CreateIndexFile();
            IndexFile.LoadFromBinaryReader(binaryReader);
        }

        public void SaveIndexToBinaryWriter(Stream binaryWriter)
        {
            IndexFile.SaveToBinaryWriter(binaryWriter);
        }

        /// <summary>Builds the data-container tree for this engine (v4 small-header vs v5/v6 IFF).</summary>
        protected abstract ScummV5V6DataFile CreateDataFile();

        /// <summary>Builds the index-file reader for this engine.</summary>
        protected abstract ScummV5V6IndexFile CreateIndexFile();

        /// <summary>Hook run after the data files are loaded (e.g. v4 loads its standalone fonts).</summary>
        protected virtual void AfterLoad() { }

        /// <summary>Links each index directory entry to its data block (done once at load).</summary>
        protected abstract void LinkDataAndIndexFile();

        /// <summary>Recomputes the index offsets from the (recalculated) block positions on save.</summary>
        protected abstract void FixUpIndexOffsets();
    }
}
