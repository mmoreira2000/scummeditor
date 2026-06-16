using System;
using System.Collections.Generic;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /*
    Best-effort detection of the language of the game texts.

    There is no language field in the SCUMM files, so the detector reads a sample of the
    game's own dialogue strings and counts how many words match a small list of very common
    words of each known language. The language only wins when it has enough hits AND a clear
    margin over the runner-up; otherwise the result is null ("unknown") and the caller simply
    omits the language. This works for fan translations too, where checksum-based tables
    (like ScummVM's) would not.
    */
    public static class GameLanguageDetector
    {
        private class LanguageProfile
        {
            public string Name;
            public string[] CommonWords;
        }

        // The word lists only contain words that are frequent in game dialogue and are not
        // shared between the listed languages (shared ones like "para"/"com" were left out).
        private static readonly LanguageProfile[] Profiles = new LanguageProfile[]
        {
            new LanguageProfile
            {
                Name = "English",
                CommonWords = new[] { "the", "you", "what", "this", "that", "have", "with", "your", "don", "can" }
            },
            new LanguageProfile
            {
                Name = "Portuguese",
                CommonWords = new[] { "você", "não", "uma", "isso", "então", "também", "são", "coisa", "aqui", "está" }
            },
            new LanguageProfile
            {
                Name = "Spanish",
                CommonWords = new[] { "usted", "pero", "ahora", "esto", "qué", "muy", "eso", "tienes", "puedo", "hola" }
            },
            new LanguageProfile
            {
                Name = "German",
                CommonWords = new[] { "ich", "nicht", "und", "das", "ist", "ein", "du", "wir", "der", "sie" }
            },
            new LanguageProfile
            {
                Name = "French",
                CommonWords = new[] { "vous", "pas", "les", "avec", "une", "suis", "votre", "moi", "quoi", "bien" }
            },
            new LanguageProfile
            {
                Name = "Italian",
                CommonWords = new[] { "non", "che", "per", "sono", "questo", "cosa", "anche", "ciao", "perché", "della" }
            }
        };

        // The decision needs at least this many hits, and twice the runner-up's hits.
        private const int MinimumHits = 25;

        // A Japanese (SJIS) game is recognized by structure, not words: the editor's Latin-1 text
        // codec renders its double-byte text as a dense run of high (non-ASCII) characters. Measured
        // over the library, every Japanese edition is >= 0.10 and every European one <= 0.02, so 0.06
        // separates them safely (with a minimum sample so tiny/garbage inputs never trigger it).
        private const double JapaneseHighByteRatio = 0.06;
        private const int JapaneseMinimumChars = 2000;

        /// <summary>
        /// Returns the detected language name, or null when it cannot be decided. Works for every
        /// supported SCUMM version: a v4 game's texts are extracted from all its DISKnn.LEC disks via
        /// the v4 pipeline; v5/v6 use the single data file. Reads only the game's own data - no EXE.
        /// </summary>
        public static string Detect(ScummGameData game)
        {
            if (game == null) return null;

            List<GameTextEntry> entries;
            try
            {
                GameTextCodec codec = GameTextCodec.Default();
                bool isV4 = game.LoadedGameInfo != null && game.LoadedGameInfo.ScummVersion == 4;
                entries = isV4
                    ? GameTextManager.ExtractV4(game, codec)
                    : GameTextManager.Extract(game.DataFile, codec);
            }
            catch (Exception)
            {
                return null; // optional feature: never break loading because of it
            }

            return DetectFromEntries(entries);
        }

        /// <summary>Legacy overload: detect from a single v5/v6 data file.</summary>
        public static string Detect(ScummV5V6DataFile dataFile)
        {
            List<GameTextEntry> entries;
            try
            {
                entries = GameTextManager.Extract(dataFile, GameTextCodec.Default());
            }
            catch (Exception)
            {
                return null;
            }

            return DetectFromEntries(entries);
        }

        private static string DetectFromEntries(List<GameTextEntry> entries)
        {
            // Japanese is recognized by its high-byte (SJIS) density before any word matching - the
            // European word lists would never match its codec-rendered text anyway.
            if (IsHighByteDense(entries))
            {
                return "Japanese";
            }

            // Count word hits per language over a sample of the texts.
            var hitsPerProfile = new int[Profiles.Length];
            int entriesExamined = 0;

            foreach (GameTextEntry entry in entries)
            {
                if (entriesExamined >= 800)
                {
                    break;
                }
                entriesExamined++;

                string plainText = RemoveTokens(entry.Text);
                List<string> words = SplitIntoWords(plainText);

                foreach (string word in words)
                {
                    for (int p = 0; p < Profiles.Length; p++)
                    {
                        foreach (string commonWord in Profiles[p].CommonWords)
                        {
                            if (word == commonWord)
                            {
                                hitsPerProfile[p]++;
                                break;
                            }
                        }
                    }
                }
            }

            // Pick the best language and the runner-up.
            int bestIndex = -1;
            int bestHits = 0;
            int secondBestHits = 0;
            for (int p = 0; p < Profiles.Length; p++)
            {
                if (hitsPerProfile[p] > bestHits)
                {
                    secondBestHits = bestHits;
                    bestHits = hitsPerProfile[p];
                    bestIndex = p;
                }
                else if (hitsPerProfile[p] > secondBestHits)
                {
                    secondBestHits = hitsPerProfile[p];
                }
            }

            if (bestIndex < 0)
            {
                return null;
            }
            if (bestHits < MinimumHits)
            {
                return null;
            }
            if (bestHits < secondBestHits * 2)
            {
                return null; // too close to call
            }

            return Profiles[bestIndex].Name;
        }

        /// <summary>
        /// True when the texts are mostly high (non-ASCII) characters - the signature of a Japanese
        /// SJIS game seen through the Latin-1 text codec. European editions stay well below the
        /// threshold even with their accented characters.
        /// </summary>
        private static bool IsHighByteDense(List<GameTextEntry> entries)
        {
            int total = 0;
            int high = 0;
            foreach (GameTextEntry entry in entries)
            {
                foreach (char c in entry.Text)
                {
                    if (char.IsWhiteSpace(c)) continue;
                    total++;
                    if (c > 127) high++;
                }
            }
            return total >= JapaneseMinimumChars && (double)high / total >= JapaneseHighByteRatio;
        }

        /// <summary>Removes {tokens} from a display text, keeping only the spoken words.</summary>
        private static string RemoveTokens(string text)
        {
            var result = new System.Text.StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        break; // malformed token: ignore the rest
                    }
                    i = close + 1;
                    continue;
                }
                result.Append(c);
                i++;
            }
            return result.ToString();
        }

        /// <summary>Splits a text into lowercase words (letters only, accents included).</summary>
        private static List<string> SplitIntoWords(string text)
        {
            var words = new List<string>();
            var currentWord = new System.Text.StringBuilder();

            foreach (char c in text)
            {
                if (char.IsLetter(c))
                {
                    currentWord.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    if (currentWord.Length > 0)
                    {
                        words.Add(currentWord.ToString());
                        currentWord.Length = 0;
                    }
                }
            }
            if (currentWord.Length > 0)
            {
                words.Add(currentWord.ToString());
            }
            return words;
        }
    }
}
