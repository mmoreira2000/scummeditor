using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;

namespace ScummEditor.Engine
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
            new KnownGame { IndexFileName = "MONKEYK.000",  DataFileName = "MONKEYK.001",  Game = ScummGame.MonkeyIsland1VGA, ScummVersion = 5 }, // Japanese FM Towns release
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

            GameInfo info = DetectGameInFolder(folderPath);
            if (info != null && info.LoadedGame != ScummGame.None)
            {
                // Language is detected from content (index-file MD5 / heuristic), independent of the game/version.
                info.Language = ScummLanguageDetector.Detect(info);
            }
            return info;
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

            // LucasArts SCUMM v7 (The Dig, Full Throttle): GAME.LA0 index + GAME.LA1 data.
            GameInfo v7 = DetectScummV7(folder);
            if (v7 != null)
            {
                return v7;
            }

            // No v5/v6/v7 game matched - try the SCUMM v4 layout (000.LFL + DISKnn.LEC).
            GameInfo v4 = DetectScummV4(folder);
            if (v4 != null)
            {
                return v4;
            }

            // Then the SCUMM v3 layout (00.LFL index + NN.LFL room files; two-digit names).
            GameInfo v3 = DetectScummV3(folder);
            if (v3 != null)
            {
                return v3;
            }

            var none = new GameInfo();
            none.LoadedGame = ScummGame.None;
            return none;
        }

        /// <summary>
        /// Detects a SCUMM v7 game (The Dig, Full Throttle). Both use a GAME.LA0 index file and a
        /// GAME.LA1 data file, neither XOR-encrypted, so the layout is confirmed by content: the index
        /// begins with a plain "RNAM" tag and the data with the "LECF" container tag. The specific game
        /// is read from the data file's base name (DIG / FT), the only two SCUMM v7 titles.
        /// </summary>
        private static GameInfo DetectScummV7(string folder)
        {
            foreach (string indexPath in Directory.GetFiles(folder, "*.LA0"))
            {
                string baseName = Path.GetFileNameWithoutExtension(indexPath);
                string dataPath = Path.Combine(folder, baseName + ".LA1");

                if (!File.Exists(dataPath))
                {
                    continue;
                }
                if (!StartsWithBigHeaderTag(indexPath, "RNAM") || !StartsWithBigHeaderTag(dataPath, "LECF"))
                {
                    continue;
                }

                // The only two SCUMM v7 games are The Dig (DIG.LA0) and Full Throttle (FT.LA0). The
                // Curse of Monkey Island (SCUMM v8) ships the SAME COMI.LA0/COMI.LA1 naming and the
                // same plain RNAM/LECF magic, so it passes the content checks above. v8 is NOT
                // supported, so reject any other base name here rather than mislabel it as The Dig and
                // crash on load (the v8 index has a DRSC block and a larger MAXS the v7 reader chokes on).
                bool isFullThrottle = string.Equals(baseName, "FT", StringComparison.OrdinalIgnoreCase);
                bool isTheDig = string.Equals(baseName, "DIG", StringComparison.OrdinalIgnoreCase);
                if (!isFullThrottle && !isTheDig)
                {
                    continue;
                }

                ScummGame game = isFullThrottle ? ScummGame.FullThrottle : ScummGame.TheDig;

                var info = new GameInfo
                {
                    LoadedGame = game,
                    IndexFile = indexPath,
                    DataFile = dataPath,
                    DataFiles = new List<string> { dataPath }, // v7 keeps all room data in one file
                    NutFontFiles = EnumerateNutFonts(folder), // external SMUSH fonts (FONT0.NUT, ...)
                    BundleFiles = EnumerateBundles(folder),   // external iMUSE bundles (DIGMUSIC/DIGVOICE.BUN)
                    TrsFiles = EnumerateTrs(folder),          // external .TRS subtitle/UI text
                    Xored = false,
                    XorKey = 0x00,      // v7 data is not XOR-encrypted
                    IndexXorKey = 0x00, // v7 index is not XOR-encrypted
                    ScummVersion = 7,
                    UsesSmallHeader = false, // big-header [tag:4][size:4 BE] IFF blocks, like v5/v6
                    IsTalkie = true          // The Dig and Full Throttle are CD/talkie releases
                };

                // Full Throttle ships its recorded speech as an external MONSTER.SOU (Creative VOC), exactly
                // like the v5/v6 talkies, so the existing speech viewer handles it. The Dig has no MONSTER.SOU
                // (its voice lives in DIGVOICE.BUN), so SpeechFilePath stays null there.
                string speechPath = Path.Combine(folder, "MONSTER.SOU");
                if (File.Exists(speechPath))
                {
                    info.SpeechFilePath = speechPath;
                }

                // The Dig's localized editions ship the translated in-game text in an external LANGUAGE.BND.
                // The European editions keep it at the folder root; the CJK editions (Chinese/Japanese/
                // Korean) keep it under a VIDEO/ subfolder, so we search recursively like EnumerateNutFonts
                // and EnumerateTrs already do (ScummVM finds it in either place via its recursive search path).
                string languageBundle = Directory
                    .GetFiles(folder, "LANGUAGE.BND", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(languageBundle))
                {
                    info.LanguageBundlePath = languageBundle;
                }

                return info;
            }

            return null;
        }

        /// <summary>
        /// Every .NUT SMUSH font in a v7 game folder, ordered by name. The game keeps some fonts at the
        /// folder root (BIGFONT.NUT, SCUMMFNT.NUT, ...) and the video-subtitle fonts in a VIDEO subfolder
        /// (FONT0.NUT...), so the search recurses; both belong to this edition.
        /// </summary>
        private static List<string> EnumerateNutFonts(string folder)
        {
            var fonts = new List<string>();
            foreach (string path in Directory.GetFiles(folder, "*.NUT", SearchOption.AllDirectories))
            {
                fonts.Add(path);
            }
            fonts.Sort(StringComparer.OrdinalIgnoreCase);
            return fonts;
        }

        /// <summary>Every .BUN iMUSE sound bundle in a v7 game folder (The Dig's DIGMUSIC.BUN /
        /// DIGVOICE.BUN), ordered by name. Full Throttle ships none (its speech is MONSTER.SOU).</summary>
        private static List<string> EnumerateBundles(string folder)
        {
            var bundles = new List<string>();
            foreach (string path in Directory.GetFiles(folder, "*.BUN"))
            {
                bundles.Add(path);
            }
            bundles.Sort(StringComparer.OrdinalIgnoreCase);
            return bundles;
        }

        /// <summary>Every .TRS text resource in a v7 game folder (root + VIDEO subfolder), ordered by name:
        /// the cutscene-subtitle / UI strings (The Dig DIGTXT.TRS/DIG.TRS, Full Throttle's per-scene .TRS).</summary>
        private static List<string> EnumerateTrs(string folder)
        {
            var trs = new List<string>();
            foreach (string path in Directory.GetFiles(folder, "*.TRS", SearchOption.AllDirectories))
            {
                trs.Add(path);
            }
            trs.Sort(StringComparer.OrdinalIgnoreCase);
            return trs;
        }

        /// <summary>True when the file begins with a v5/v6/v7 big-header 4-char block tag.</summary>
        private static bool StartsWithBigHeaderTag(string path, string tag)
        {
            byte[] head = ReadFileHead(path, 4);
            if (head == null || head.Length < 4)
            {
                return false;
            }
            for (int i = 0; i < 4; i++)
            {
                if ((char)head[i] != tag[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Detects a SCUMM v4 game (Monkey Island 1 floppy, Loom CD). Both share the file names
        /// 000.LFL + DISK01.LEC, and Loom also exists as a v3 release, so the v4 layout is confirmed
        /// by content (the index starts with a small-header "RN" block). The specific game is chosen
        /// from the DATA alone - never the game EXE, which may be missing from the folder: Loom CD
        /// ships its music as ripped CD audio (CDDA.SOU) and fits on a single data disk, while Monkey
        /// Island 1 floppy spans several DISKnn.LEC and has no CD audio.
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

            List<string> dataDisks = EnumerateV4DataDisks(folder);
            bool hasCdAudioFile = File.Exists(Path.Combine(folder, "CDDA.SOU"));

            // Loom CD has ripped CD audio and a single data disk; MI1 floppy has several disks and no
            // CD audio. Either signal alone identifies Loom, so the EXE is not needed.
            ScummGame game = (hasCdAudioFile || dataDisks.Count == 1)
                ? ScummGame.Loom
                : ScummGame.MonkeyIsland1Floppy;

            var result = new GameInfo
            {
                LoadedGame = game,
                IndexFile = indexPath,
                DataFile = dataPath,
                DataFiles = dataDisks,                // a v4 game is spread over all DISKnn.LEC
                FontFiles = EnumerateV4Fonts(folder), // 90x.LFL charset files
                Xored = true,
                XorKey = 0x69,      // DISKnn.LEC data
                IndexXorKey = 0x00, // 000.LFL is plaintext
                ScummVersion = 4,
                UsesSmallHeader = true // [size:4 LE][tag:2] blocks
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

        /// <summary>
        /// Detects a SCUMM v3 game (Indiana Jones 3, Loom EGA, Zak). v3 stores one room per file
        /// (00.LFL index + NN.LFL rooms + 9x.LFL charsets), in two sub-families decided by the index:
        ///   - GF_OLD256 (Indy3 VGA, Zak FM-Towns): plaintext, v4-style small-header blocks; 00.LFL
        ///     begins with a small-header "0R" block.
        ///   - old-bundle (Loom EGA, Indy3 EGA): whole files XOR 0xFF; 00.LFL decrypts to magic 0x0100.
        /// The specific game is told apart from the index resource counts alone (never the EXE).
        /// </summary>
        private static GameInfo DetectScummV3(string folder)
        {
            string indexPath = Path.Combine(folder, "00.LFL");
            if (!File.Exists(indexPath))
            {
                return null;
            }

            byte[] head = ReadFileHead(indexPath, 6);
            if (head == null || head.Length < 6)
            {
                return null;
            }

            bool oldBundle;
            int xorKey;
            bool classicV1 = false;
            if (head[4] == (byte)'0' && head[5] == (byte)'R')
            {
                // GF_OLD256: plaintext small-header index starting with the room directory "0R".
                oldBundle = false;
                xorKey = 0x00;
            }
            else if ((head[0] ^ 0xFF) == 0x00 && (head[1] ^ 0xFF) == 0x01)
            {
                // old-bundle v2/v3 (enhanced): XOR 0xFF over the whole file; decrypts to magic 0x0100.
                oldBundle = true;
                xorKey = 0xFF;
            }
            else if ((head[0] ^ 0xFF) == 0x31 && (head[1] ^ 0xFF) == 0x0A)
            {
                // SCUMM v1 "classic" (Maniac/Zak DOS floppy): XOR 0xFF, count-less index magic 0x0A31.
                oldBundle = true;
                xorKey = 0xFF;
                classicV1 = true;
            }
            else
            {
                return null;
            }

            List<string> rooms = EnumerateV3Rooms(folder);
            if (rooms.Count == 0)
            {
                return null;
            }

            List<string> fonts = EnumerateV3Fonts(folder); // 9x.LFL charsets

            // SCUMM v1 (classic, magic 0x0A31) and v2 (Maniac/Zak "Enhanced", magic 0x0100) both share the
            // old-bundle XOR-0xFF container with the v3old EGA games (Loom, Indy3) but ship NO 9x.LFL charset
            // files (their fonts are baked into the game EXE) and use a 1-byte global-object table instead of
            // v3old's 4-byte one. v1 is told from v2 by its index magic; v2 from v3old by the zero-charset
            // signal (without it the v3old index reader walks off the end of a v1/v2 index).
            bool isV1 = classicV1;
            bool isV2 = oldBundle && !classicV1 && fonts.Count == 0;
            bool isV1OrV2 = isV1 || isV2;

            var result = new GameInfo
            {
                LoadedGame = isV1OrV2
                    ? IdentifyV1V2Game(folder)
                    : IdentifyV3Game(indexPath, oldBundle, xorKey, folder, rooms.Count, fonts.Count),
                IndexFile = indexPath,
                DataFile = rooms[0],
                DataFiles = rooms,                 // one NN.LFL per room
                FontFiles = fonts,
                Xored = xorKey != 0,
                XorKey = xorKey,
                IndexXorKey = xorKey,
                ScummVersion = isV1 ? 1 : (isV2 ? 2 : 3),
                UsesSmallHeader = !oldBundle, // GF_OLD256 uses the v4 [size:4 LE][tag:2] header
                UsesOldBundle = oldBundle,    // EGA + v1/v2 games use untagged [size:uint16] chunks
                GlobalObjectEntrySize = isV1OrV2 ? 1 : 4, // v1/v2 object table is 1 byte/object; v3old is 4
                UsesClassicIndex = isV1       // v1 00.LFL is count-less with hardcoded per-game counts
            };

            // FM-Towns releases ship ripped CD audio (CDDA.SOU); mark the CD edition.
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

        /// <summary>The NN.LFL room files (01-89), ordered by room number; excludes 00 and the 9x charsets.</summary>
        private static List<string> EnumerateV3Rooms(string folder)
        {
            var rooms = new List<KeyValuePair<int, string>>();
            foreach (string path in Directory.GetFiles(folder, "*.LFL"))
            {
                int number = ParseLflNumber(Path.GetFileNameWithoutExtension(path));
                if (number >= 1 && number <= 89)
                {
                    rooms.Add(new KeyValuePair<int, string>(number, path));
                }
            }
            return rooms.OrderBy(r => r.Key).Select(r => r.Value).ToList();
        }

        /// <summary>The 9x.LFL charset files (90-99), ordered by number.</summary>
        private static List<string> EnumerateV3Fonts(string folder)
        {
            var fonts = new List<KeyValuePair<int, string>>();
            foreach (string path in Directory.GetFiles(folder, "*.LFL"))
            {
                int number = ParseLflNumber(Path.GetFileNameWithoutExtension(path));
                if (number >= 90 && number <= 99)
                {
                    fonts.Add(new KeyValuePair<int, string>(number, path));
                }
            }
            return fonts.OrderBy(f => f.Key).Select(f => f.Value).ToList();
        }

        /// <summary>Parses an "NN" LFL base name to its number, or -1 when it is not all digits.</summary>
        private static int ParseLflNumber(string nameWithoutExtension)
        {
            int number;
            return int.TryParse(nameWithoutExtension, out number) ? number : -1;
        }

        /// <summary>
        /// Identifies the v3 game from data-only signals (never the EXE). The charset (9x.LFL) count is
        /// the most reliable one and is stable across languages, graphic editions (EGA/VGA/FM-Towns)
        /// and versions: Indiana Jones 3 ships 6-7 charsets, Zak McKracken 2, and Loom exactly 1. The
        /// index resource counts cannot tell Indy3 from Zak on FM-Towns (which pads every directory to
        /// 99 rooms / 199 scripts / 199 sounds), so the charset count is checked first, with the index
        /// counts and room-file count as fallbacks for unusual layouts.
        /// </summary>
        private static ScummGame IdentifyV3Game(string indexPath, bool oldBundle, int xorKey, string folder, int roomFileCount, int fontFileCount)
        {
            if (fontFileCount >= 4) return ScummGame.IndianaJones3; // Indy3 ships 6-7 charsets
            if (fontFileCount == 2) return ScummGame.ZakMcKracken;  // Zak ships 2 charsets
            if (fontFileCount == 1) return ScummGame.Loom;          // Loom ships exactly 1 charset

            // Unusual charset count: fall back to the index resource counts.
            int[] counts = ReadV3DirectoryCounts(indexPath, oldBundle, xorKey);
            if (counts != null)
            {
                int rooms = counts[0];
                int scripts = counts[1];
                int sounds = counts[2];

                if (rooms >= 100 || scripts >= 200) return ScummGame.Loom;       // Loom: 100 rooms / 200 scripts
                if (scripts >= 180 && sounds >= 180) return ScummGame.ZakMcKracken; // Zak: ~199 scripts and sounds
                if (scripts >= 110 && scripts < 170 && sounds < 130) return ScummGame.IndianaJones3; // Indy3: ~139/~84
            }

            // Last resort: Indy3 ships far more room files (~83) than Zak (~59).
            return roomFileCount >= 70 ? ScummGame.IndianaJones3 : ScummGame.ZakMcKracken;
        }

        /// <summary>
        /// Tells Maniac Mansion from Zak McKracken in a v1/v2 game from data alone: Zak ships room 58
        /// (58.LFL), Maniac does not (its rooms stop around 52-53). This is the same rule scummvm uses
        /// (detection_internal.h: zak has 58.LFL, maniac does not).
        /// </summary>
        private static ScummGame IdentifyV1V2Game(string folder)
        {
            return File.Exists(Path.Combine(folder, "58.LFL"))
                ? ScummGame.ZakMcKracken
                : ScummGame.ManiacMansion;
        }

        /// <summary>
        /// Reads {rooms, scripts, sounds, costumes} from the v3 index without fully parsing it.
        /// old-bundle: magic(2)+numObj(2)+objTable(numObj*4) then four [count:1][count bytes][count*2 LE]
        /// directories in order ROOM, COSTUME, SCRIPT, SOUND. small-header: the count is the uint16 at
        /// each 0R/0S/0N/0C block's body. Returns null on any malformed read.
        /// </summary>
        private static int[] ReadV3DirectoryCounts(string indexPath, bool oldBundle, int xorKey)
        {
            try
            {
                byte[] data = File.ReadAllBytes(indexPath);
                if (xorKey != 0)
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] ^= (byte)xorKey;
                    }
                }

                if (oldBundle)
                {
                    int p = 2; // skip magic
                    int numObjects = data[p] | (data[p + 1] << 8);
                    p += 2 + numObjects * 4;

                    int rooms = ReadOldBundleDirCount(data, ref p);
                    int costumes = ReadOldBundleDirCount(data, ref p);
                    int scripts = ReadOldBundleDirCount(data, ref p);
                    int sounds = ReadOldBundleDirCount(data, ref p);
                    return new[] { rooms, scripts, sounds, costumes };
                }

                int rRooms = 0, rScripts = 0, rSounds = 0, rCostumes = 0;
                int q = 0;
                while (q + 8 <= data.Length)
                {
                    uint size = (uint)(data[q] | (data[q + 1] << 8) | (data[q + 2] << 16) | (data[q + 3] << 24));
                    string tag = string.Empty + (char)data[q + 4] + (char)data[q + 5];
                    if (size < 6 || q + size > data.Length)
                    {
                        break;
                    }

                    int count = data[q + 6] | (data[q + 7] << 8);
                    if (tag == "0R") rRooms = count;
                    else if (tag == "0S") rScripts = count;
                    else if (tag == "0N") rSounds = count;
                    else if (tag == "0C") rCostumes = count;

                    q += (int)size;
                }
                return new[] { rRooms, rScripts, rSounds, rCostumes };
            }
            catch (IOException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                // A v2 index (1-byte object table) walked with the v3old 4-byte stride runs off the end;
                // callers treat null as "counts unknown" and fall back to other signals.
                return null;
            }
        }

        /// <summary>Reads one old-bundle directory ([count:1][count roomno bytes][count uint16 offsets]) and returns its count, advancing p.</summary>
        private static int ReadOldBundleDirCount(byte[] data, ref int p)
        {
            int count = data[p];
            p += 1 + count + count * 2;
            return count;
        }

        /// <summary>Reads up to <paramref name="length"/> bytes from the start of a file; null on error.</summary>
        private static byte[] ReadFileHead(string path, int length)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    var head = new byte[length];
                    int read = stream.Read(head, 0, length);
                    if (read < length)
                    {
                        return null;
                    }
                    return head;
                }
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}
