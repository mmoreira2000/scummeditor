using System.IO;
using System.Linq;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 external localized text: The Dig's LANGUAGE.BND (XOR 0x13) and the .TRS subtitle/UI files.
    /// These hold the translated strings for the non-English editions and were not editable before. Tests
    /// run on the real Portuguese The Dig data.
    /// </summary>
    public class V7LocalizedTextTests
    {
        private const string PtDig = GameLibrary.TheDigPortuguese; // ScummV7/Dig, The (1995)/Other Languages/Portuguese/CD

        private static LanguageBundleFile LoadBnd()
        {
            string folder = GameLibrary.Folder(PtDig);
            if (folder == null) return null;
            string path = Path.Combine(folder, "LANGUAGE.BND");
            if (!File.Exists(path)) return null;
            var bnd = new LanguageBundleFile(path);
            bnd.Load(File.ReadAllBytes(path));
            return bnd;
        }

        [SkippableFact]
        public void LanguageBundleDecodesRealPortuguese()
        {
            LanguageBundleFile bnd = LoadBnd();
            Skip.If(bnd == null, "Portuguese The Dig LANGUAGE.BND not present");

            Assert.True(bnd.IsValid);
            Assert.True(bnd.Encoded, "the PT bundle should be XOR-0x13 encoded");
            Assert.True(bnd.Entries.Count > 100, "too few strings parsed");
            // Keys are ROOM.index; the first AIRLOCK strings are known plain-ASCII lines.
            Assert.Contains(bnd.Entries, e => e.Key.StartsWith("AIRLOCK.") && e.Text == "Maggie.");
            Assert.Contains(bnd.Entries, e => e.Text == "Venha para este lado da porta.");
        }

        [SkippableFact]
        public void LanguageBundleRoundTripIsByteIdentical()
        {
            LanguageBundleFile bnd = LoadBnd();
            Skip.If(bnd == null, "PT The Dig LANGUAGE.BND not present");

            byte[] rebuilt = bnd.BuildContent();
            Assert.Equal(bnd.OriginalContent.Length, rebuilt.Length);
            Assert.True(rebuilt.SequenceEqual(bnd.OriginalContent), "no-op rebuild changed the bundle bytes");
        }

        [SkippableFact]
        public void LanguageBundleEditRoundTrips()
        {
            LanguageBundleFile bnd = LoadBnd();
            Skip.If(bnd == null, "PT The Dig LANGUAGE.BND not present");

            // Edit one entry; the change must survive a rebuild+reload and not disturb its neighbours.
            LocalizedTextEntry target = bnd.Entries.First(e => e.Text == "Maggie.");
            string otherKey = bnd.Entries.First(e => e.Key != target.Key).Key;
            string otherText = bnd.Entries.First(e => e.Key != target.Key).Text;
            target.Text = "Teste 123";

            var reloaded = new LanguageBundleFile(bnd.FilePath);
            reloaded.Load(bnd.BuildContent());

            Assert.Equal("Teste 123", reloaded.Entries.First(e => e.Key == target.Key).Text);
            Assert.Equal(otherText, reloaded.Entries.First(e => e.Key == otherKey).Text);
        }

        [SkippableFact]
        public void LanguageBundleExportImportNoOpIsByteIdentical()
        {
            LanguageBundleFile bnd = LoadBnd();
            Skip.If(bnd == null, "PT The Dig LANGUAGE.BND not present");

            string dump = bnd.ExportToText();
            string report = bnd.ImportFromText(dump); // re-import the unchanged dump
            Assert.Contains("0 of", report);          // nothing changed
            Assert.True(bnd.BuildContent().SequenceEqual(bnd.OriginalContent), "export/import no-op changed bytes");
        }
    }
}
