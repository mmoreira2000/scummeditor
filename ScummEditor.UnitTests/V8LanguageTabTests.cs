using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) LANGUAGE.TAB - the external localized text that holds almost
    /// all of COMI's on-screen lines (the high-value translation file). Verifies it loads, that a no-op
    /// rebuild is BYTE-IDENTICAL (so an untouched file is preserved exactly), and that an edit survives a
    /// rebuild + reparse.
    /// </summary>
    public class V8LanguageTabTests
    {
        private readonly ITestOutputHelper _out;
        public V8LanguageTabTests(ITestOutputHelper o) { _out = o; }

        private static LanguageTabFile Load(out byte[] original)
        {
            original = null;
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIsland);
            if (info == null) return null;
            ScummGameData game = ScummGameData.LoadFromGameInfo(info);
            var tab = game.LocalizedTextFiles.OfType<LanguageTabFile>().FirstOrDefault();
            if (tab != null) original = File.ReadAllBytes(info.LanguageTabPath);
            return tab;
        }

        // COMI is a Windows-95 game: its LANGUAGE.TAB is Windows-1252 (ANSI), NOT the DOS CP850 the v1-v7
        // games use. Decoding it as CP850 garbled every accent in the viewer (the byte 0xF3 'o-acute' showed
        // as the CP850 glyph '¾'). CodePageFor(language, version) must pick 1252 for v8, 850 for v1-v7.
        [Fact]
        public void CodePageIsAnsiForV8AndDosForV7()
        {
            Assert.Equal(1252, DosCodePageText.CodePageFor(ScummLanguage.Portuguese, 8));
            Assert.Equal(1252, DosCodePageText.CodePageFor(ScummLanguage.English, 8));
            Assert.Equal(850, DosCodePageText.CodePageFor(ScummLanguage.Portuguese, 7));
            Assert.Equal(850, DosCodePageText.CodePageFor(ScummLanguage.English, 5));
            Assert.Equal(0, DosCodePageText.CodePageFor(ScummLanguage.Korean, 8)); // CJK stays raw
        }

        [Fact]
        public void V8PortugueseAccentsDecodeAsAnsiNotDos()
        {
            // The file stores ANSI bytes; the editor holds them byte-faithfully (one char per byte).
            // Windows-1252: 'c-cedilla'=0xE7, 'a-tilde'=0xE3.
            string raw = new string(new[] { (char)0xE7, (char)0xE3, 'o' });
            Assert.Equal("ção", DosCodePageText.ToDisplay(raw, 1252));   // v8 (ANSI): correct accents
            Assert.Equal("þÒo", DosCodePageText.ToDisplay(raw, 850));    // v7 (DOS CP850): the garble
            Assert.Equal(raw, DosCodePageText.FromDisplay("ção", 1252)); // an edit round-trips to ANSI
        }

        [SkippableFact]
        public void RealComiPortugueseLanguageTabDecodesAccentsAsAnsi()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIslandPortuguese);
            Skip.If(info == null, "COMI Portuguese edition not present");
            ScummGameData game = ScummGameData.LoadFromGameInfo(info);
            var tab = game.LocalizedTextFiles.OfType<LanguageTabFile>().FirstOrDefault();
            Skip.If(tab == null, "COMI PT LANGUAGE.TAB not present");

            int v8cp = DosCodePageText.CodePageFor(ScummLanguage.Portuguese, 8); // 1252
            const string ptAccents = "ãõáéíóúâêôàç"; // ãõáéíóúâêôàç
            bool foundAnsiAccent = tab.Entries.Any(e =>
                DosCodePageText.ToDisplay(e.Text, v8cp).Any(c => ptAccents.IndexOf(c) >= 0));
            _out.WriteLine("COMI PT LANGUAGE.TAB decoded a Portuguese accent via CP{0}: {1}", v8cp, foundAnsiAccent);
            Assert.True(foundAnsiAccent, "no Portuguese accent decoded from COMI PT LANGUAGE.TAB via Windows-1252 (is it really ANSI?)");
        }

        [SkippableFact]
        public void PortugueseEditionIsDetectedFromLanguageTab()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIslandPortuguese);
            Skip.If(info == null, "COMI Portuguese edition not present");

            ScummGameData game = ScummGameData.LoadFromGameInfo(info);
            ScummEditor.Engine.Encoders.ScummLanguageDetector.RefineFromContent(game);

            _out.WriteLine("COMI PT edition detected as: {0}", game.LoadedGameInfo.Language);
            Assert.Equal(ScummLanguage.Portuguese, game.LoadedGameInfo.Language);
        }

        [SkippableFact]
        public void LoadsAndNoOpRebuildIsByteIdentical()
        {
            byte[] original;
            LanguageTabFile tab = Load(out original);
            Skip.If(tab == null, "COMI LANGUAGE.TAB not present");

            _out.WriteLine("LANGUAGE.TAB entries: {0}", tab.Entries.Count);
            Assert.True(tab.IsValid && tab.Entries.Count > 100, "LANGUAGE.TAB parsed too few entries");
            Assert.True(tab.Entries.Any(e => e.Text.Any(char.IsLetter)), "no readable text in LANGUAGE.TAB");

            byte[] rebuilt = tab.BuildContent();
            Assert.True(original.Length == rebuilt.Length, string.Format("length {0} != {1}", original.Length, rebuilt.Length));
            for (int i = 0; i < original.Length; i++)
            {
                if (original[i] != rebuilt[i])
                {
                    Assert.Fail(string.Format("LANGUAGE.TAB byte differs at 0x{0:X} after a no-op rebuild", i));
                }
            }
        }

        [SkippableFact]
        public void EditSurvivesRebuildAndReparse()
        {
            byte[] original;
            LanguageTabFile tab = Load(out original);
            Skip.If(tab == null, "COMI LANGUAGE.TAB not present");

            // Edit the first entry that has real letters.
            LocalizedTextEntry target = tab.Entries.FirstOrDefault(e => e.Text.Any(char.IsLetter));
            Skip.If(target == null, "no editable LANGUAGE.TAB entry");
            string key = target.Key;
            target.Text = target.Text + " ZZ";

            byte[] rebuilt = tab.BuildContent();
            Assert.NotEqual(original.Length, rebuilt.Length); // a size-changing edit

            var reloaded = new LanguageTabFile("LANGUAGE.TAB");
            reloaded.Load(rebuilt);
            LocalizedTextEntry round = reloaded.Entries.FirstOrDefault(e => e.Key == key);
            Assert.NotNull(round);
            Assert.EndsWith(" ZZ", round.Text);

            // Export/import round-trip is also a no-op when unchanged.
            var fresh = new LanguageTabFile("LANGUAGE.TAB");
            fresh.Load(original);
            fresh.ImportFromText(fresh.ExportToText());
            Assert.True(System.Linq.Enumerable.SequenceEqual(original, fresh.BuildContent()), "export+import no-op changed the file");
        }
    }
}
