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

        // ---- .TRS ----

        private static TrsFile LoadTrs(string relativePath)
        {
            string full = GameLibrary.Folder(relativePath.Substring(0, relativePath.IndexOf('|')));
            if (full == null) return null;
            string path = Path.Combine(full, relativePath.Substring(relativePath.IndexOf('|') + 1).Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;
            var trs = new TrsFile(path);
            trs.Load(File.ReadAllBytes(path));
            return trs;
        }

        [SkippableTheory]
        [InlineData(PtDig + "|DIG.TRS")]
        [InlineData(PtDig + "|VIDEO/DIGTXT.TRS")]
        public void TrsFileRoundTripsByteIdentical(string spec)
        {
            TrsFile trs = LoadTrs(spec);
            Skip.If(trs == null, ".TRS not present: " + spec);

            Assert.True(trs.IsValid, "no #define entries parsed");
            Assert.True(trs.Entries.Count > 0);
            Assert.True(trs.BuildContent().SequenceEqual(trs.OriginalContent), "no-op rebuild changed the .TRS bytes");
        }

        [SkippableFact]
        public void DigtxtTrsHasReadableStrings()
        {
            TrsFile trs = LoadTrs(PtDig + "|VIDEO/DIGTXT.TRS");
            Skip.If(trs == null, "DIGTXT.TRS not present");
            // The credits block and a subtitle line are present in the decoded text.
            Assert.Contains(trs.Entries, e => e.Text.Contains("Charlie Ramos"));
        }

        [SkippableFact]
        public void TrsFileEditRoundTrips()
        {
            TrsFile trs = LoadTrs(PtDig + "|VIDEO/DIGTXT.TRS");
            Skip.If(trs == null, "DIGTXT.TRS not present");

            LocalizedTextEntry target = trs.Entries[1]; // a subtitle block
            string otherKey = trs.Entries[0].Key;
            string otherText = trs.Entries[0].Text;
            target.Text = "^f00Texto de teste.\r\n\r\n";

            var reloaded = new TrsFile(trs.FilePath);
            reloaded.Load(trs.BuildContent());
            Assert.Equal("^f00Texto de teste.\r\n\r\n", reloaded.Entries.First(e => e.Key == target.Key).Text);
            Assert.Equal(otherText, reloaded.Entries.First(e => e.Key == otherKey).Text);
        }

        [Fact]
        public void EtrsEncodedTrsRoundTripsAndDecodes()
        {
            // Build a synthetic ETRS .TRS (16-byte header + XOR-0xCC body) to cover the encoded path.
            byte[] body = System.Text.Encoding.Latin1.GetBytes("#define A 1\r\nHello\r\n\r\n#define B 2\r\nWorld\r\n");
            var file = new byte[16 + body.Length];
            file[0] = (byte)'E'; file[1] = (byte)'T'; file[2] = (byte)'R'; file[3] = (byte)'S';
            for (int i = 0; i < body.Length; i++) file[16 + i] = (byte)(body[i] ^ 0xCC);

            var trs = new TrsFile("synthetic.trs");
            trs.Load(file);

            Assert.True(trs.Encoded);
            Assert.Equal(2, trs.Entries.Count);
            Assert.Contains(trs.Entries, e => e.Text.Contains("Hello"));
            Assert.True(trs.BuildContent().SequenceEqual(file), "ETRS no-op rebuild changed bytes");

            // edit + rebuild + reload survives, still ETRS-encoded
            trs.Entries[0].Text = "Bonjour\r\n\r\n";
            var reloaded = new TrsFile("synthetic.trs");
            reloaded.Load(trs.BuildContent());
            Assert.True(reloaded.Encoded);
            Assert.Contains(reloaded.Entries, e => e.Text.Contains("Bonjour"));
            Assert.Contains(reloaded.Entries, e => e.Text.Contains("World"));
        }
    }
}
