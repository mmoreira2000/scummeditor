using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                    DataFiles = new List<string> { dataPath }, // v5/v6 keep all data in one file
                    Xored = true,
                    XorKey = 0x69,
                    IndexXorKey = 0x69, // v5/v6 index is XOR-encrypted like the data
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
                if (speechInfo.Exists)
                {
                    // The file is exposed in the tree even when small (floppy editions ship an
                    // effects-only MONSTER.SOU); only a big one marks the talkie edition.
                    result.SpeechFilePath = speechPath;
                    if (speechInfo.Length >= TalkieMinimumSpeechBytes)
                    {
                        result.IsTalkie = true;
                    }
                }

                // Some releases ship the CD audio tracks ripped as CDDA.SOU instead of a speech
                // file (e.g. the Monkey Island 1 CD edition). That marks the CD edition, but it
                // is NOT speech - the game must not be reported as a talkie because of it.
                result.HasCdAudio = false;
                var cdAudioInfo = new FileInfo(Path.Combine(folder, "CDDA.SOU"));
                if (cdAudioInfo.Exists)
                {
                    result.CdAudioFilePath = cdAudioInfo.FullName;
                    if (cdAudioInfo.Length >= TalkieMinimumSpeechBytes)
                    {
                        result.HasCdAudio = true;
                    }
                }

                // Monkey Island 1 CD: the speech edition has its own entry in the game list.
                if (candidate.Game == ScummGame.MonkeyIsland1VGA && result.IsTalkie)
                {
                    result.LoadedGame = ScummGame.MonkeyIsland1VGASpeech;
                }

                return result;
            }

            // No v5/v6 game matched - try the SCUMM v4 layout (000.LFL + DISKnn.LEC).
            GameInfo v4 = DetectScummV4(folder);
            if (v4 != null)
            {
                return v4;
            }

            var none = new GameInfo();
            none.LoadedGame = ScummGame.None;
            return none;
        }

        /// <summary>
        /// Detects a SCUMM v4 game (Monkey Island 1 floppy, Loom CD). Both share the file names
        /// 000.LFL + DISK01.LEC, and Loom also exists as a v3 release, so the v4 layout is
        /// confirmed by content (the index starts with a small-header "RN" block) and the
        /// specific game is chosen by the EXE next to it.
        /// </summary>
        private static GameInfo DetectScummV4(string folder)
        {
            string indexPath = Path.Combine(folder, "000.LFL");
            string dataPath = Path.Combine(folder, "DISK01.LEC");

            if (!File.Exists(indexPath) || !File.Exists(dataPath))
            {
                return null;
            }
            if (!StartsWithSmallHeaderTag(indexPath, "RN"))
            {
                return null;
            }

            ScummGame game = ScummGame.MonkeyIsland1Floppy;
            if (File.Exists(Path.Combine(folder, "LOOM.EXE")))
            {
                game = ScummGame.Loom;
            }

            var result = new GameInfo
            {
                LoadedGame = game,
                IndexFile = indexPath,
                DataFile = dataPath,
                DataFiles = EnumerateV4DataDisks(folder), // a v4 game is spread over all DISKnn.LEC
                FontFiles = EnumerateV4Fonts(folder),     // 90x.LFL charset files
                Xored = true,
                XorKey = 0x69,      // DISKnn.LEC data
                IndexXorKey = 0x00, // 000.LFL is plaintext
                ScummVersion = 4
            };

            // Loom CD ships ripped CD audio tracks (CDDA.SOU); MI1 floppy has none.
            var cdAudioInfo = new FileInfo(Path.Combine(folder, "CDDA.SOU"));
            if (cdAudioInfo.Exists)
            {
                result.CdAudioFilePath = cdAudioInfo.FullName;
                if (cdAudioInfo.Length >= TalkieMinimumSpeechBytes)
                {
                    result.HasCdAudio = true;
                }
            }

            return result;
        }

        /// <summary>
        /// All DISKnn.LEC data disks in the folder, ordered by disk number. v4 games spread their
        /// rooms across several disks (with possible gaps, e.g. MI1 EGA has 01-04 then 09).
        /// </summary>
        private static List<string> EnumerateV4DataDisks(string folder)
        {
            var disks = new List<KeyValuePair<int, string>>();
            foreach (string path in Directory.GetFiles(folder, "DISK*.LEC"))
            {
                int number = ParseDiskNumber(Path.GetFileNameWithoutExtension(path));
                disks.Add(new KeyValuePair<int, string>(number, path));
            }

            return disks.OrderBy(d => d.Key).Select(d => d.Value).ToList();
        }

        /// <summary>The v4 charset files in the folder: every *.LFL except the 000.LFL index (901-904.LFL).</summary>
        private static List<string> EnumerateV4Fonts(string folder)
        {
            var fonts = new List<string>();
            foreach (string path in Directory.GetFiles(folder, "*.LFL"))
            {
                if (!string.Equals(Path.GetFileName(path), "000.LFL", StringComparison.OrdinalIgnoreCase))
                {
                    fonts.Add(path);
                }
            }
            fonts.Sort(StringComparer.OrdinalIgnoreCase);
            return fonts;
        }

        /// <summary>Parses the trailing number of a "DISKnn" name (returns 0 when there is none).</summary>
        private static int ParseDiskNumber(string nameWithoutExtension)
        {
            string digits = new string(nameWithoutExtension.Where(char.IsDigit).ToArray());
            int number;
            return int.TryParse(digits, out number) ? number : 0;
        }

        /// <summary>True when the file begins with a v4 small-header block ([size:4 LE][tag:2]) of the given tag.</summary>
        private static bool StartsWithSmallHeaderTag(string path, string tag)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    var head = new byte[6];
                    if (stream.Read(head, 0, head.Length) != head.Length)
                    {
                        return false;
                    }
                    return (char)head[4] == tag[0] && (char)head[5] == tag[1];
                }
            }
            catch (IOException)
            {
                return false;
            }
        }
    }
}
