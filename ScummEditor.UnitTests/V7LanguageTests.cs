using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 language detection. v7 has no language field and its MD5 is not in the v2-v6 table, so the
    /// detector reads the real translated text: The Dig's dialogue is in the external LANGUAGE.BND (whose
    /// double-byte editions carry a CJK marker j/h/c), while Full Throttle keeps it in the container scripts.
    /// These run on the real editions and assert the detected GameInfo.Language (set by RefineFromContent,
    /// the same call the GUI makes after load).
    /// </summary>
    public class V7LanguageTests
    {
        [SkippableTheory]
        // The Dig - dialogue in LANGUAGE.BND (Western word-heuristic) or its CJK marker; English ships no bundle.
        [InlineData("ScummV7/Dig, The (1995)/DOS v1.0", ScummLanguage.English)]
        [InlineData("ScummV7/Dig, The (1995)/Other Languages/Portuguese/CD", ScummLanguage.Portuguese)]
        [InlineData("ScummV7/Dig, The (1995)/Other Languages/French/DOS CD", ScummLanguage.French)]
        [InlineData("ScummV7/Dig, The (1995)/Other Languages/German/CD", ScummLanguage.German)]
        [InlineData("ScummV7/Dig, The (1995)/Other Languages/Italian/DOS CD", ScummLanguage.Italian)]
        [InlineData("ScummV7/Dig, The (1995)/Other Languages/Spanish/CD", ScummLanguage.Spanish)]
        [InlineData("ScummV7/Dig, The (1995)/Other Languages/Chinese/CD", ScummLanguage.Chinese)]
        [InlineData("ScummV7/Dig, The (1995)/Other Languages/Japanese/Windows", ScummLanguage.Japanese)]
        [InlineData("ScummV7/Dig, The (1995)/Other Languages/Korean/Windows", ScummLanguage.Korean)]
        // Full Throttle - dialogue in the container scripts (no bundle), incl. Spanish the .TRS could not call.
        [InlineData("ScummV7/Full Throttle (1995)/DOS CD", ScummLanguage.English)]
        [InlineData("ScummV7/Full Throttle (1995)/Other Languages/Portuguese/DOS CD", ScummLanguage.Portuguese)]
        [InlineData("ScummV7/Full Throttle (1995)/Other Languages/French/DOS CD", ScummLanguage.French)]
        [InlineData("ScummV7/Full Throttle (1995)/Other Languages/German/DOS CD", ScummLanguage.German)]
        [InlineData("ScummV7/Full Throttle (1995)/Other Languages/Italian/DOS CD", ScummLanguage.Italian)]
        [InlineData("ScummV7/Full Throttle (1995)/Other Languages/Spanish/DOS CD", ScummLanguage.Spanish)]
        public void V7EditionLanguageIsDetected(string relativePath, ScummLanguage expected)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            ScummLanguageDetector.RefineFromContent(game); // the GUI runs this right after load

            Assert.Equal(expected, game.LoadedGameInfo.Language);
        }

        // ---- accent display via the DOS code page ----

        [Theory]
        [InlineData(850)]
        [InlineData(860)]
        public void DosCodePageRoundTripsEveryByte(int codePage)
        {
            // Display -> edit-encode must return every byte 0x00-0xFF unchanged, so an unedited localized
            // string stays byte-identical when shown through the code page.
            var chars = new char[256];
            for (int i = 0; i < 256; i++) chars[i] = (char)i;
            string latin1 = new string(chars);

            string display = DosCodePageText.ToDisplay(latin1, codePage);
            Assert.Equal(latin1, DosCodePageText.FromDisplay(display, codePage));
        }

        [Fact]
        public void CodePageForMapsLanguagesToTheRightDosCodePage()
        {
            // SCUMM v7 uses CP850 for every Western edition, including Portuguese (verified vs the real PT data).
            Assert.Equal(850, DosCodePageText.CodePageFor(ScummLanguage.Portuguese));
            Assert.Equal(850, DosCodePageText.CodePageFor(ScummLanguage.English));
            Assert.Equal(850, DosCodePageText.CodePageFor(ScummLanguage.French));
            Assert.Equal(850, DosCodePageText.CodePageFor(ScummLanguage.Spanish));
            Assert.Equal(0, DosCodePageText.CodePageFor(ScummLanguage.Chinese));   // CJK stays raw
            Assert.Equal(0, DosCodePageText.CodePageFor(ScummLanguage.Unknown));
        }

        [Fact]
        public void CodePageZeroLeavesTextRaw()
        {
            string s = "abcãÿ";
            Assert.Equal(s, DosCodePageText.ToDisplay(s, 0));
            Assert.Equal(s, DosCodePageText.FromDisplay(s, 0));
        }

        [SkippableFact]
        public void PortugueseDigBundleRendersAccentsViaCp850()
        {
            string folder = GameLibrary.Folder(GameLibrary.TheDigPortuguese);
            Skip.If(folder == null, "PT The Dig not present");

            var bnd = new LanguageBundleFile(Path.Combine(folder, "LANGUAGE.BND"));
            bnd.Load(File.ReadAllBytes(bnd.FilePath));

            bool correctAccent = false;
            foreach (LocalizedTextEntry e in bnd.Entries)
            {
                string display = DosCodePageText.ToDisplay(e.Text, 850);
                // unedited text must round-trip byte-identically through the code page
                Assert.Equal(e.Text, DosCodePageText.FromDisplay(display, 850));
                // CP850 must yield the CORRECT Portuguese spelling (with the right "ã"), which CP860 mangles.
                if (display.Contains("não alcanço") || display.Contains("compressão") || display.Contains("botão"))
                    correctAccent = true;
            }
            Assert.True(correctAccent, "CP850 did not render the expected accented Portuguese (ã) from the PT bundle");
        }
    }
}
