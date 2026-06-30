using System.Text;
using ScummEditor.Engine.Structures;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Renders the byte-faithful (Latin-1) localized v7 text with its real accents, by transcoding through
    /// the edition's DOS code page for DISPLAY only. The bytes stored in an entry are the game's own code
    /// page; shown as Latin-1 their high bytes look wrong, so the editor decodes them through the right code
    /// page for the screen and re-encodes an edit back to those bytes.
    ///
    /// SCUMM v7 uses CP850 for every Western edition - including Portuguese (ScummVM renders v7 with
    /// Common::kDos850, string.cpp; verified against the real PT The Dig, whose "ã" only renders correctly
    /// via CP850, NOT CP860). The double-byte CJK editions (and unknown languages) return the text unchanged
    /// (code page 0): a single-byte round-trip is not available for them, and they are not the translation
    /// target. CP850 round-trips every byte 0x00-0xFF exactly, so an unedited string is unchanged.
    /// </summary>
    public static class DosCodePageText
    {
        static DosCodePageText()
        {
            // The DOS code pages are not built into .NET Core; register the provider that supplies them.
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { }
        }

        /// <summary>The DOS code page to display a language's text in, or 0 to leave it byte-faithful (raw).</summary>
        public static int CodePageFor(ScummLanguage language)
        {
            switch (language)
            {
                case ScummLanguage.English:
                case ScummLanguage.French:
                case ScummLanguage.German:
                case ScummLanguage.Italian:
                case ScummLanguage.Spanish:
                case ScummLanguage.Portuguese:
                    return 850; // CP850 - the DOS code page SCUMM v7 uses for every Western edition (incl. PT)
                default:
                    return 0;   // CJK / Hebrew / Russian / unknown: no single-byte round-trip, keep raw
            }
        }

        /// <summary>
        /// The text code page for a localized-text file of the given edition. v1-v7 are DOS-era and use CP850
        /// for every Western language; v8 (The Curse of Monkey Island) is a Windows-95 title whose LANGUAGE.TAB
        /// is Windows-1252 (ANSI), not CP850 - decoding it as CP850 garbles the accents (e.g. the ANSI byte
        /// 0xF3 'o-acute' shows as the CP850 glyph '¾'). CJK / Hebrew / Russian / unknown stay 0 (raw).
        /// </summary>
        public static int CodePageFor(ScummLanguage language, int scummVersion)
        {
            int cp = CodePageFor(language);
            if (cp == 850 && scummVersion >= 8) return 1252; // COMI is a Windows (ANSI) game, not DOS
            return cp;
        }

        /// <summary>Byte-faithful (Latin-1) text -&gt; Unicode for display via the code page; cp 0 = unchanged.</summary>
        public static string ToDisplay(string latin1, int codePage)
        {
            if (codePage == 0 || string.IsNullOrEmpty(latin1)) return latin1;
            Encoding enc = TryGet(codePage);
            return enc == null ? latin1 : enc.GetString(Encoding.Latin1.GetBytes(latin1));
        }

        /// <summary>Edited Unicode text -&gt; byte-faithful (Latin-1) via the code page; cp 0 = unchanged.</summary>
        public static string FromDisplay(string display, int codePage)
        {
            if (codePage == 0 || string.IsNullOrEmpty(display)) return display;
            Encoding enc = TryGet(codePage);
            return enc == null ? display : Encoding.Latin1.GetString(enc.GetBytes(display));
        }

        private static Encoding TryGet(int codePage)
        {
            try { return Encoding.GetEncoding(codePage); }
            catch { return null; }
        }
    }
}
