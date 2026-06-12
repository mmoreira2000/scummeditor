using System;
using System.IO;
using ScummEditor.Structures;

namespace ScummEditor
{
    public static class Functions
    {
        /// <summary>One supported game, identified by its index/data file pair.</summary>
        private class KnownGame
        {
            public string IndexFileName;
            public string DataFileName;
            public ScummGame Game;
            public int ScummVersion;
        }

        // Effects-only MONSTER.SOU files have a few MB; the talkie ones (recorded speech)
        // have 150 MB or more. 50 MB sits safely between the two groups.
        private const long TalkieMinimumSpeechBytes = 50000000;

        private static readonly KnownGame[] KnownGames = new KnownGame[]
        {
            new KnownGame { IndexFileName = "TENTACLE.000", DataFileName = "TENTACLE.001", Game = ScummGame.DayOfTheTentacle, ScummVersion = 6 },
            new KnownGame { IndexFileName = "SAMNMAX.000",  DataFileName = "SAMNMAX.001",  Game = ScummGame.SamAndMax,        ScummVersion = 6 },
            new KnownGame { IndexFileName = "SAMNMAX.SM0",  DataFileName = "SAMNMAX.SM1",  Game = ScummGame.SamAndMax,        ScummVersion = 6 },
            new KnownGame { IndexFileName = "ATLANTIS.000", DataFileName = "ATLANTIS.001", Game = ScummGame.FateOfAtlantis,   ScummVersion = 5 },
            new KnownGame { IndexFileName = "INDY4.000",    DataFileName = "INDY4.001",    Game = ScummGame.FateOfAtlantis,   ScummVersion = 5 }, // FM Towns release
            new KnownGame { IndexFileName = "MONKEY2.000",  DataFileName = "MONKEY2.001",  Game = ScummGame.MonkeyIsland2,    ScummVersion = 5 },
            new KnownGame { IndexFileName = "MONKEY.000",   DataFileName = "MONKEY.001",   Game = ScummGame.MonkeyIsland1VGA, ScummVersion = 5 },
        };

        /// <summary>Detects the game from the path of one of its files (legacy entry point).</summary>
        public static GameInfo FindScummGame(string path)
        {
            string folder = Path.GetDirectoryName(path);
            return DetectGameInFolder(folder);
        }

        /// <summary>
        /// Detects the game looking ONLY at the content of the selected folder: the user always
        /// selects the exact game folder, and the files inside it tell which game and SCUMM
        /// version it is. Returns LoadedGame = ScummGame.None when nothing is recognized.
        /// </summary>
        public static GameInfo FindScummGameInFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                var none = new GameInfo();
                none.LoadedGame = ScummGame.None;
                return none;
            }

            return DetectGameInFolder(folderPath);
        }

        private static GameInfo DetectGameInFolder(string folder)
        {
            foreach (KnownGame candidate in KnownGames)
            {
                string indexPath = Path.Combine(folder, candidate.IndexFileName);
                string dataPath = Path.Combine(folder, candidate.DataFileName);

                if (!File.Exists(indexPath))
                {
                    continue;
                }
                if (!File.Exists(dataPath))
                {
                    continue;
                }

                var result = new GameInfo
                {
                    LoadedGame = candidate.Game,
                    IndexFile = indexPath,
                    DataFile = dataPath,
                    Xored = true,
                    XorKey = 0x69,
                    ScummVersion = candidate.ScummVersion
                };

                // A speech file next to the data files marks the talkie (CD) release. Most
                // releases call it MONSTER.SOU; FM Towns uses the game's own base name
                // (e.g. INDY4.SOU). The floppy editions also ship a MONSTER.SOU, but it only
                // holds sound effects (a few MB) - real recorded speech takes 150 MB or more,
                // so only a big file means the talkie edition.
                string speechPath = Path.Combine(folder, "MONSTER.SOU");
                if (!File.Exists(speechPath))
                {
                    string baseName = Path.GetFileNameWithoutExtension(candidate.IndexFileName);
                    speechPath = Path.Combine(folder, baseName + ".SOU");
                }

                result.IsTalkie = false;
                var speechInfo = new FileInfo(speechPath);
                if (speechInfo.Exists && speechInfo.Length >= TalkieMinimumSpeechBytes)
                {
                    result.IsTalkie = true;
                }

                // Monkey Island 1 CD: the speech edition has its own entry in the game list.
                if (candidate.Game == ScummGame.MonkeyIsland1VGA && result.IsTalkie)
                {
                    result.LoadedGame = ScummGame.MonkeyIsland1VGASpeech;
                }

                return result;
            }

            var none = new GameInfo();
            none.LoadedGame = ScummGame.None;
            return none;
        }
    }
}
