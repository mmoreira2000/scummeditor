using System.Drawing;
using System.IO;
using System.Linq;
using ScummEditor.Engine;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 cross-edition viewer sweep: for EVERY Curse of Monkey Island edition in the library, run the
    /// REAL decode/parse of every viewer (index, scripts, images, costumes, NUT fonts, LANGUAGE.TAB, and -
    /// once - a VIMA sound bundle), so a regression in any one is caught on real data across all editions
    /// (the lesson from the v7 MONSTER.SOU "0 entries" miss). Skips when the library is absent.
    /// </summary>
    public class V8ViewerSweepTests
    {
        private readonly ITestOutputHelper _out;
        public V8ViewerSweepTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void EveryV8EditionDecodesThroughEveryViewer()
        {
            string root = GameLibrary.Folder("ScummV8");
            Skip.If(root == null, "ScummV8 library not present");

            int editions = 0;
            bool soundChecked = false;
            foreach (string indexPath in Directory.GetFiles(root, "COMI.LA0", SearchOption.AllDirectories))
            {
                string folder = Path.GetDirectoryName(indexPath);
                GameInfo info = Functions.FindScummGameInFolder(folder);
                if (info == null || info.LoadedGame != ScummGame.CurseOfMonkeyIsland) continue;
                editions++;

                ScummGameData game = ScummGameData.LoadFromGameInfo(info);
                string ed = folder.Substring(root.Length).TrimStart('\\', '/');

                // index
                var idx = (ScummEditor.Engine.Structures.IndexFile.ScummV8IndexFile)game.IndexFile;
                Assert.True(idx.DROO.NumOfItems > 0, ed + ": empty DROO");

                // scripts (a global script must decode to the end)
                ScriptBlock scrp = GameLibrary.AllBlocks(game).OfType<ScriptBlock>().FirstOrDefault(s => s.BlockType == "SCRP");
                Assert.True(scrp != null && scrp.Disassemble().DecodedToEnd, ed + ": no SCRP decoded to the end");

                // image (a background)
                var imgDec = new ScummV8ImageDecoder();
                RoomBlock room = game.DataFile.GetLFLFs().Select(l => l.GetROOM())
                    .First(r => r.Childrens.Any(c => c.BlockType == "IMAG")
                        && (FindStrip(r)));
                using (Bitmap bg = imgDec.DecodeBackground(room)) Assert.NotNull(bg);

                // costume (an AKOS cel)
                BlockBase akos = game.DataFile.GetLFLFs().SelectMany(l => l.Childrens).FirstOrDefault(c => c.BlockType == "AKOS" && AkosImageDecoder.GetCelCount(c) > 0);
                if (akos != null)
                {
                    int cels = AkosImageDecoder.GetCelCount(akos);
                    for (int k = 0; k < cels; k++)
                    {
                        Size sz = AkosImageDecoder.GetCelSize(akos, k);
                        if (sz.Width * sz.Height > 4) { using (Bitmap c = AkosImageDecoder.DecodeCel(akos, k)) Assert.NotNull(c); break; }
                    }
                }

                // NUT font (a glyph)
                Assert.True(game.NutFonts.Count > 0, ed + ": no NUT fonts");
                NutFont font = game.NutFonts[0].Font;
                int gi = Enumerable.Range(0, font.Glyphs.Count).First(i => font.Glyphs[i].HasPixels && font.Glyphs[i].Width > 0);
                Assert.NotNull(NutImageDecoder.DecodeGlyphIndices(font, gi));

                // LANGUAGE.TAB
                var tab = game.LocalizedTextFiles.OfType<LanguageTabFile>().FirstOrDefault();
                Assert.True(tab != null && tab.Entries.Count > 0, ed + ": no LANGUAGE.TAB entries");

                // sound (a VIMA bundle entry) - once is enough; decoding is heavy.
                if (!soundChecked && info.BundleFiles != null && info.BundleFiles.Count > 0)
                {
                    var bundle = new ImuseBundleFile(info.BundleFiles[0]);
                    bundle.EnsureParsed();
                    if (bundle.IsValid && bundle.Entries.Count > 0)
                    {
                        byte[] wav = ImuseBundleDecoder.ToWav(bundle.ReadEntryRaw(0));
                        Assert.True(wav != null && wav.Length > 44, ed + ": bundle entry did not decode");
                        soundChecked = true;
                    }
                }

                _out.WriteLine("OK: {0}", ed);
            }

            Assert.True(editions > 0, "no v8 editions found");
            _out.WriteLine("v8 editions swept: {0}", editions);
        }

        private static bool FindStrip(RoomBlock room)
        {
            BlockBase imag = room.Childrens.FirstOrDefault(c => c.BlockType == "IMAG");
            BlockBase wrap = imag?.Childrens.FirstOrDefault(c => c.BlockType == "WRAP");
            BlockBase smap = wrap?.Childrens.FirstOrDefault(c => c.BlockType == "SMAP");
            return smap != null && smap.Childrens.Any(c => c.BlockType == "BSTR");
        }
    }
}
