using System.IO;
using System.Linq;
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
