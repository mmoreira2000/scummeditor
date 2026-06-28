using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ScummEditor.Engine.Structures;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Detects a game's release language from its CONTENT (never the folder name). Primary signal: the MD5
    /// of the first 1 MB of the RAW (on-disk) index file - exactly ScummVM's kMD5FileSizeLimit method -
    /// matched against ScummLanguageMd5Table (derived from ScummVM's scumm-md5.h). When the MD5 is not in
    /// the table, a content heuristic gives a best-effort guess; failing that, Unknown.
    /// </summary>
    public static class ScummLanguageDetector
    {
        private const int Md5PrefixBytes = 1024 * 1024; // == ScummVM kMD5FileSizeLimit

        public static ScummLanguage Detect(GameInfo info)
        {
            if (info == null || info.LoadedGame == ScummGame.None) return ScummLanguage.Unknown;

            string md5 = ComputeFilePrefixMd5(info.IndexFile, Md5PrefixBytes);
            ScummLanguage tabled;
            if (md5 != null && ScummLanguageMd5Table.Md5ToLanguage.TryGetValue(md5, out tabled))
            {
                return tabled;
            }

            return DetectByContentHeuristic(info);
        }

        /// <summary>Lowercase-hex MD5 of the first <paramref name="maxBytes"/> bytes of the file; null on error.</summary>
        public static string ComputeFilePrefixMd5(string path, int maxBytes)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                using (FileStream stream = File.OpenRead(path))
                using (MD5 md5 = MD5.Create())
                {
                    int toRead = (int)Math.Min(maxBytes, stream.Length);
                    var buffer = new byte[toRead];
                    int read = 0;
                    while (read < toRead)
                    {
                        int n = stream.Read(buffer, read, toRead - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    byte[] hash = md5.ComputeHash(buffer, 0, read);
                    var sb = new StringBuilder(hash.Length * 2);
                    foreach (byte b in hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        /// <summary>
        /// MD5 alone cannot use the loaded game text, so the content heuristic runs as a separate
        /// post-load refinement (see RefineFromContent). At detection time an untabled release is Unknown.
        /// </summary>
        private static ScummLanguage DetectByContentHeuristic(GameInfo info)
        {
            return ScummLanguage.Unknown;
        }

        /// <summary>
        /// Post-load refinement of GameInfo.Language using the content word-heuristic (which needs the
        /// loaded data). Rules:
        ///  - a confident NON-English MD5 result is authoritative and kept;
        ///  - a confident content language FILLS an Unknown MD5 result;
        ///  - it OVERRIDES an English MD5 result, which is the fan-translation case: the localized copy
        ///    kept the original English index, so the index MD5 wrongly reports English.
        /// Never throws (language is an optional, non-blocking feature).
        /// </summary>
        public static void RefineFromContent(ScummGameData game)
        {
            if (game == null || game.LoadedGameInfo == null) return;

            // SCUMM v8 (The Curse of Monkey Island) keeps its translated dialogue in the external
            // LANGUAGE.TAB; its in-container scripts are /TAG/ references (engine text), so the v2-v7
            // content heuristic would misread them and report a wrong language. Real v8 language detection
            // (from LANGUAGE.TAB) lands in a later milestone; until then leave the MD5 result as-is
            // (Unknown for the untabled v8 releases) rather than guess wrong.
            if (game.LoadedGameInfo.ScummVersion == 8)
            {
                return;
            }

            // SCUMM v7 (The Dig, Full Throttle) needs its own detector: The Dig keeps its dialogue in the
            // external LANGUAGE.BND (its in-container scripts are English engine text), while Full Throttle
            // keeps the translated dialogue in the container scripts. DetectV7Language routes each correctly.
            if (game.LoadedGameInfo.ScummVersion == 7)
            {
                ScummLanguage v7 = DetectV7Language(game);
                if (v7 != ScummLanguage.Unknown) game.LoadedGameInfo.Language = v7;
                return;
            }

            ScummLanguage md5 = game.LoadedGameInfo.Language;
            if (md5 != ScummLanguage.Unknown && md5 != ScummLanguage.English) return; // trust a confident non-English MD5

            ScummLanguage content;
            try { content = FromName(GameLanguageDetector.Detect(game)); }
            catch { return; }

            if (content == ScummLanguage.Unknown) return;           // nothing better to offer
            if (md5 == content) return;                             // already agree
            game.LoadedGameInfo.Language = content;                 // fill Unknown, or override a dubious English
        }

        /// <summary>
        /// Detects a SCUMM v7 edition's language from its real translated text. The Dig's dialogue lives in
        /// the external LANGUAGE.BND - its double-byte editions declare a CJK marker (j/h/c), and the Western
        /// ones are recognised by the word heuristic over the bundle entries. Full Throttle (and the English
        /// Dig) have no bundle, so their translated dialogue is the in-container script text. Returns Unknown
        /// when nothing decides (e.g. a language with no word profile), so the caller leaves it unset.
        /// </summary>
        private static ScummLanguage DetectV7Language(ScummGameData game)
        {
            LanguageBundleFile bundle = null;
            if (game.LocalizedTextFiles != null)
            {
                foreach (ILocalizedTextFile f in game.LocalizedTextFiles)
                {
                    if (f is LanguageBundleFile b && b.IsValid) { bundle = b; break; }
                }
            }

            if (bundle != null)
            {
                switch (bundle.CjkMarker)
                {
                    case 'j': return ScummLanguage.Japanese;
                    case 'h': return ScummLanguage.Korean;
                    case 'c': return ScummLanguage.Chinese;
                }
                var texts = new System.Collections.Generic.List<string>(bundle.Entries.Count);
                foreach (LocalizedTextEntry e in bundle.Entries) texts.Add(e.Text);
                try { return FromName(GameLanguageDetector.DetectFromStrings(texts)); }
                catch { return ScummLanguage.Unknown; }
            }

            // No LANGUAGE.BND: Full Throttle (dialogue in the scripts) or the English Dig - detect from the
            // in-container text, which for these is the actual (possibly translated) dialogue.
            try { return FromName(GameLanguageDetector.Detect(game)); }
            catch { return ScummLanguage.Unknown; }
        }

        private static ScummLanguage FromName(string name)
        {
            switch (name)
            {
                case "English": return ScummLanguage.English;
                case "French": return ScummLanguage.French;
                case "German": return ScummLanguage.German;
                case "Italian": return ScummLanguage.Italian;
                case "Spanish": return ScummLanguage.Spanish;
                case "Portuguese": return ScummLanguage.Portuguese;
                case "Japanese": return ScummLanguage.Japanese;
                default: return ScummLanguage.Unknown;
            }
        }
    }
}
