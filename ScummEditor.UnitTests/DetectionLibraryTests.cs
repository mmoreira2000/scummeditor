using System;
using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Library-wide detection regression: every loadable edition under GameData must detect the right
    /// game/version, and its language (MD5 table + content-heuristic refinement) must NEVER be the WRONG
    /// language - it matches the language the folder path implies, or is Unknown. Skips without the library.
    /// </summary>
    public class DetectionLibraryTests
    {
        [SkippableFact]
        public void NoEditionReportsTheWrongLanguage()
        {
            Skip.IfNot(GameLibrary.Available, "GameData library not present");
            string root = GameLibrary.Folder("");
            Skip.If(root == null, "GameData root not found");

            int games = 0, ok = 0, unknown = 0;
            var mismatches = new List<string>();
            Walk(new DirectoryInfo(root), root, ref games, ref ok, ref unknown, mismatches);

            Assert.True(games >= 90, "expected the full library; only " + games + " games detected");
            Assert.True(mismatches.Count == 0,
                "editions detected with the WRONG language:\n" + string.Join("\n", mismatches));
        }

        private static void Walk(DirectoryInfo dir, string root, ref int games, ref int ok, ref int unknown, List<string> mismatches)
        {
            if (dir.GetFiles().Length > 0)
            {
                GameInfo info = Functions.FindScummGameInFolder(dir.FullName);
                if (info != null && info.LoadedGame != ScummGame.None)
                {
                    games++;
                    try
                    {
                        ScummGameData game = ScummGameData.LoadFromGameInfo(info);
                        ScummLanguageDetector.RefineFromContent(game);
                        info = game.LoadedGameInfo;
                    }
                    catch { /* keep the MD5-only language */ }

                    string rel = dir.FullName.Substring(root.Length).TrimStart('\\', '/');
                    ScummLanguage expected = ExpectedLanguageFromPath(rel);
                    if (info.Language == ScummLanguage.Unknown) unknown++;
                    else if (info.Language == expected) ok++;
                    else mismatches.Add(string.Format("{0} -> {1} (path implies {2})", rel, info.Language, expected));
                }
            }
            foreach (DirectoryInfo sub in dir.GetDirectories())
                Walk(sub, root, ref games, ref ok, ref unknown, mismatches);
        }

        private static ScummLanguage ExpectedLanguageFromPath(string rel)
        {
            string[] parts = rel.Split('\\', '/');
            for (int i = 0; i < parts.Length - 1; i++)
                if (string.Equals(parts[i], "Other Languages", StringComparison.OrdinalIgnoreCase))
                    return NameToLanguage(parts[i + 1]);
            return ScummLanguage.English; // a main edition (not under Other Languages) is the English release
        }

        private static ScummLanguage NameToLanguage(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "french": return ScummLanguage.French;
                case "german": return ScummLanguage.German;
                case "italian": return ScummLanguage.Italian;
                case "spanish": return ScummLanguage.Spanish;
                case "portuguese": case "brazilian": return ScummLanguage.Portuguese;
                case "japanese": return ScummLanguage.Japanese;
                case "hebrew": return ScummLanguage.Hebrew;
                case "korean": return ScummLanguage.Korean;
                case "chinese": return ScummLanguage.Chinese;
                case "russian": return ScummLanguage.Russian;
                default: return ScummLanguage.Unknown;
            }
        }
    }
}
