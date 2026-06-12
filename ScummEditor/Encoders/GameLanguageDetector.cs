using System;
using System.Collections.Generic;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
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

        /// <summary>Returns the detected language name, or null when it cannot be decided.</summary>
        public static string Detect(ScummV6DataFile dataFile)
        {
            List<GameTextEntry> entries;
            try
            {
                entries = GameTextManager.Extract(dataFile, GameTextCodec.Default());
            }
            catch (Exception)
            {
                return null; // optional feature: never break loading because of it
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
