using System;
using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Locates the (git-ignored) GameData test library and loads games from it for the real-data
    /// tests. The library lives at the repo root; tests walk up from the test assembly to find it.
    /// When it is absent (a clean checkout / CI without the games) the real-data tests skip rather
    /// than fail, so the synthetic unit tests still run everywhere.
    /// </summary>
    public static class GameLibrary
    {
        // SCUMM v3 - "GF_OLD256" small-header games (Indy3 VGA/FM-Towns, Zak FM-Towns, Loom FM-Towns)
        public const string Indy3Vga = "ScummV3/Indiana Jones and the Last Crusade - The Graphic Adventure (1989)/Floppy VGA v2.0 3.0.23";
        public const string Indy3FmTowns = "ScummV3/Indiana Jones and the Last Crusade - The Graphic Adventure (1989)/FM Towns";
        public const string ZakFmTowns = "ScummV3/Zak McKracken and the Alien Mindbenders (1988)/FM Towns v1.0";
        public const string LoomFmTowns = "ScummV3/Loom (1990)/FM Towns";
        // SCUMM v3 - "GF_OLD_BUNDLE" XOR-0xFF EGA games (Indy3 EGA, Loom EGA)
        public const string Indy3Ega = "ScummV3/Indiana Jones and the Last Crusade - The Graphic Adventure (1989)/Floppy EGA v1.4";
        public const string LoomEga = "ScummV3/Loom (1990)/Floppy EGA v1.1";

        // SCUMM v4 (multi-disk floppy / Loom CD)
        public const string MonkeyIsland1FloppyVga = "ScummV4/Secret of Monkey Island, The (1990)/Floppy VGA";
        public const string MonkeyIsland1FloppyEga = "ScummV4/Secret of Monkey Island, The (1990)/Floppy EGA";
        public const string Loom = "ScummV4/Loom (1990)/DOS CD VGA v42";

        // SCUMM v5 (single LFLF data file)
        public const string MonkeyIsland2Floppy = "ScummV5/Monkey Island 2 - LeChucks Revenge (1991)/Floppy";
        public const string MonkeyIsland1CdVga = "ScummV5/Secret of Monkey Island, The (1990)/CD VGA";
        public const string FateOfAtlantisFloppy = "ScummV5/Indiana Jones and the Fate of Atlantis (1992)/Floppy v1.0";
        public const string FateOfAtlantisCd = "ScummV5/Indiana Jones and the Fate of Atlantis (1992)/CD Talkie";

        // SCUMM v6
        public const string DayOfTheTentacleFloppy = "ScummV6/Day of the Tentacle (1993)/Floppy v1.6";
        public const string DayOfTheTentacleCd = "ScummV6/Day of the Tentacle (1993)/CD Talkie";
        public const string SamAndMaxFloppy = "ScummV6/Sam and Max Hit the Road (1993)/Floppy v1.0";
        public const string SamAndMaxCd = "ScummV6/Sam and Max Hit the Road (1993)/DOS CD Talkie";

        private static readonly string _root = FindRoot();

        /// <summary>True when the GameData library was found next to the repo.</summary>
        public static bool Available { get { return _root != null; } }

        /// <summary>Absolute path of a game's data folder under GameData, or null if the folder is missing.</summary>
        public static string Folder(string relativePath)
        {
            if (_root == null) return null;
            string full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return Directory.Exists(full) ? full : null;
        }

        /// <summary>Detects the game in a library folder; returns null if the folder or game is missing.</summary>
        public static GameInfo Detect(string relativePath)
        {
            string folder = Folder(relativePath);
            if (folder == null) return null;
            GameInfo info = Functions.FindScummGameInFolder(folder);
            return (info == null || info.LoadedGame == ScummGame.None) ? null : info;
        }

        /// <summary>Loads the game in a library folder; returns null if the folder or game is missing.</summary>
        public static ScummGameData Load(string relativePath)
        {
            GameInfo info = Detect(relativePath);
            return info == null ? null : ScummGameData.LoadFromGameInfo(info);
        }

        /// <summary>Every parsed block of a loaded game (all disks for v4, the single data file for v5/v6).</summary>
        public static List<BlockBase> AllBlocks(ScummGameData game)
        {
            var blocks = new List<BlockBase>();
            if (game.DataDisks != null && game.DataDisks.Count > 0)
            {
                foreach (DataDisk disk in game.DataDisks) Collect(disk.Tree, blocks);
            }
            else if (game.DataFile != null)
            {
                Collect(game.DataFile, blocks);
            }
            return blocks;
        }

        private static void Collect(BlockBase node, List<BlockBase> acc)
        {
            if (node == null) return;
            acc.Add(node);
            if (node.Childrens != null)
            {
                foreach (BlockBase child in node.Childrens) Collect(child, acc);
            }
        }

        private static string FindRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "GameData")))
                {
                    return Path.Combine(dir.FullName, "GameData");
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
