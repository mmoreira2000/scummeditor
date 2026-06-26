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

        [SkippableTheory]
        [InlineData(PtDig + "|DIG.TRS")]
        [InlineData(PtDig + "|VIDEO/DIGTXT.TRS")]
        public void TrsFileExportImportNoOpIsByteIdentical(string spec)
        {
            TrsFile trs = LoadTrs(spec);
            Skip.If(trs == null, ".TRS not present: " + spec);

            string dump = trs.ExportToText();
            string report = trs.ImportFromText(dump); // re-import the unchanged dump
            Assert.Contains("0 of", report);          // nothing changed
            Assert.True(trs.BuildContent().SequenceEqual(trs.OriginalContent), "export/import no-op changed the .TRS bytes");
        }

        [Fact]
        public void TrsExportImportPreservesLfLineEndings()
        {
            // A plain .TRS with Unix (LF) line endings - the Steam Mac The Dig ships LF-LF .TRS files.
            // Export+import of an unchanged dump must stay byte-identical (the file's own line ending is
            // restored on import, not forced to CRLF), and an edited entry must keep the LF convention.
            byte[] file = System.Text.Encoding.Latin1.GetBytes("#define A 1\nHello\nthere\n\n#define B 2\nWorld\n");
            var trs = new TrsFile("unix.trs");
            trs.Load(file);

            Assert.False(trs.Encoded);
            Assert.Equal(2, trs.Entries.Count);

            string report = trs.ImportFromText(trs.ExportToText());
            Assert.Contains("0 of", report);
            Assert.True(trs.BuildContent().SequenceEqual(file), "LF .TRS export/import no-op changed bytes");

            // editing one entry through the import path keeps LF, never introduces CR
            string editedDump = trs.ExportToText().Replace("World", "Mundo");
            trs.ImportFromText(editedDump);
            byte[] rebuilt = trs.BuildContent();
            Assert.DoesNotContain((byte)'\r', rebuilt);
            Assert.Contains("Mundo", System.Text.Encoding.Latin1.GetString(rebuilt));
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

        // ---- load/save wiring ----

        [SkippableFact]
        public void GameLoadsLocalizedTextFiles()
        {
            ScummGameData game = GameLibrary.Load(PtDig);
            Skip.If(game == null, "PT The Dig not present");

            // The loader exposes LANGUAGE.BND (as a LanguageBundleFile) plus the .TRS files.
            Assert.Contains(game.LocalizedTextFiles, f => f is LanguageBundleFile && f.IsValid);
            Assert.Contains(game.LocalizedTextFiles, f => f is TrsFile && f.FileName.Equals("DIG.TRS", System.StringComparison.OrdinalIgnoreCase));
            Assert.True(game.LocalizedTextFiles.Count >= 3, "expected LANGUAGE.BND + at least DIG.TRS + DIGTXT.TRS");
        }

        // ---- adversarial-validation fixes ----

        [SkippableFact]
        public void DetectionFindsLanguageBundleInVideoSubfolder()
        {
            // The Chinese/Japanese/Korean editions keep LANGUAGE.BND under VIDEO/, not the root. Detection
            // must find it (it used to look only at the root, so the CJK editions' text was unreachable).
            GameInfo info = GameLibrary.Detect(GameLibrary.TheDigChinese);
            Skip.If(info == null, "Chinese The Dig not present");

            Assert.False(string.IsNullOrEmpty(info.LanguageBundlePath), "LANGUAGE.BND not detected for the CJK edition");
            Assert.Contains("VIDEO", info.LanguageBundlePath, System.StringComparison.OrdinalIgnoreCase);

            ScummGameData game = ScummGameData.LoadFromGameInfo(info);
            Assert.Contains(game.LocalizedTextFiles, f => f is LanguageBundleFile && f.IsValid);
        }

        [Fact]
        public void NormalizeEditedTextCollapsesNewlinesForBundle()
        {
            // A LANGUAGE.BND message is one line; a newline typed in the GUI must collapse to a space so the
            // saved file keeps its line-based structure (ScummVM reads a message only up to the first CR/LF).
            var bnd = new LanguageBundleFile("x.bnd");
            Assert.Equal("a b c", bnd.NormalizeEditedText("a\r\nb\nc"));
        }

        [Fact]
        public void NormalizeEditedTextKeepsTrsNativeLineEnding()
        {
            // A .TRS edited in the GUI (Windows Forms forces CRLF) must be re-expressed in the file's own
            // line ending, so editing a Mac LF-LF file does not silently rewrite every line to CRLF.
            byte[] lf = System.Text.Encoding.Latin1.GetBytes("#define A 1\nHello\n\n");
            var trs = new TrsFile("unix.trs");
            trs.Load(lf);

            Assert.Equal("Linha1\nLinha2\n", trs.NormalizeEditedText("Linha1\r\nLinha2\r\n"));
        }

        [Fact]
        public void TrsImportRejectsEmbeddedDefineLine()
        {
            // A translated value whose own text begins a line with "#define" would be re-parsed as a new
            // entry on reload, corrupting the file - the import must skip it and keep the structure intact.
            byte[] file = System.Text.Encoding.Latin1.GetBytes("#define A 1\r\nHello\r\n\r\n#define B 2\r\nWorld\r\n");
            var trs = new TrsFile("g.trs");
            trs.Load(file);
            int before = trs.Entries.Count;

            string dump = "A 1\t#define EVIL 99\\nInjected\r\n"; // escaped \n -> a real newline starting "#define"
            string report = trs.ImportFromText(dump);

            Assert.Contains("skipped", report);
            Assert.Equal(before, trs.Entries.Count);                       // no phantom entry
            Assert.True(trs.BuildContent().SequenceEqual(file), "rejected edit must leave the file unchanged");
        }

        [Fact]
        public void ImportWarnsAboutCharactersOutsideCodePage()
        {
            // A smart quote (U+2019) pasted from a word processor cannot be stored in the DOS code page;
            // Latin-1 would write it as '?', so the import must warn instead of silently corrupting.
            byte[] file = System.Text.Encoding.Latin1.GetBytes("#define A 1\r\nHello\r\n\r\n#define B 2\r\nWorld\r\n");
            var trs = new TrsFile("g.trs");
            trs.Load(file);

            string report = trs.ImportFromText("A 1\tIt’s here\r\n");
            Assert.Contains("outside the game's 8-bit code page", report);
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
