using System;
using System.IO;
using ScummEditor.Structures;

namespace ScummEditor
{
    public static class Functions
    {
        public static GameInfo FindScummGame(string path)
        {
            GameInfo result;

            path = Path.GetDirectoryName(path);

            string dataPath;
            string indexPath;

            indexPath = Path.Combine(path, "TENTACLE.000");
            dataPath = Path.Combine(path, "TENTACLE.001");
            if (File.Exists(indexPath) && File.Exists(dataPath))
            {
                result = new GameInfo
                {
                    LoadedGame = ScummGame.DayOfTheTentacle,
                    IndexFile = indexPath,
                    DataFile = dataPath,
                    Xored = true,
                    XorKey = 0x69,
                    ScummVersion = 6
                };

                return result;
            }

            indexPath = Path.Combine(path, "SAMNMAX.000");
            dataPath = Path.Combine(path, "SAMNMAX.001");
            if (File.Exists(indexPath) && File.Exists(dataPath))
            {
                result = new GameInfo
                {
                    LoadedGame = ScummGame.SamAndMax,
                    IndexFile = indexPath,
                    DataFile = dataPath,
                    Xored = true,
                    XorKey = 0x69,
                    ScummVersion = 6
                };

                return result;
            }


            indexPath = Path.Combine(path, "ATLANTIS.000");
            dataPath = Path.Combine(path, "ATLANTIS.001");
            if (File.Exists(indexPath) && File.Exists(dataPath))
            {
                result = new GameInfo
                {
                    LoadedGame = ScummGame.FateOfAtlantis,
                    IndexFile = indexPath,
                    DataFile = dataPath,
                    Xored = true,
                    XorKey = 0x69,
                    ScummVersion = 5
                };

                return result;
            }

            indexPath = Path.Combine(path, "MONKEY2.000");
            dataPath = Path.Combine(path, "MONKEY2.001");
            if (File.Exists(indexPath) && File.Exists(dataPath))
            {
                result = new GameInfo
                {
                    LoadedGame = ScummGame.MonkeyIsland2,
                    IndexFile = indexPath,
                    DataFile = dataPath,
                    Xored = true,
                    XorKey = 0x69,
                    ScummVersion = 5
                };

                return result;
            }

            indexPath = Path.Combine(path, "MONKEY.000");
            dataPath = Path.Combine(path, "MONKEY.001");
            if (File.Exists(indexPath) && File.Exists(dataPath))
            {
                result = new GameInfo
                {
                    LoadedGame = ScummGame.MonkeyIsland1VGA,
                    IndexFile = indexPath,
                    DataFile = dataPath,
                    Xored = true,
                    XorKey = 0x69,
                    ScummVersion = 5
                };

                var monstersouInfo = new FileInfo(Path.Combine(path, "MONSTER.SOU"));
                if (monstersouInfo.Exists && monstersouInfo.Length >= 190000000)
                {
                    result.LoadedGame = ScummGame.MonkeyIsland1VGASpeech;
                }
                return result;
            }

            result = new GameInfo();
            result.LoadedGame = ScummGame.None;
            return result;
        }

    }
}