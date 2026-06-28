using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) in-container text pipeline: extracts the script/verb text
    /// across both data files (SCRP at the LFLF level + ENCD/EXCD/LSCR/OBCD inside RMSC) through the v8
    /// disassembler and the 4-byte-arg codec, and proves a no-op export+import leaves both data files
    /// BYTE-IDENTICAL (so the decode/encode is a perfect inverse and the container stays intact). A second
    /// test edits a string and confirms the change survives a re-extraction (the block rebuilt + still
    /// disassembles).
    /// </summary>
    public class V8TextTests
    {
        private readonly ITestOutputHelper _output;

        public V8TextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public void TextExtractsAndNoOpImportIsByteIdentical()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(info == null, "COMI (v8) not present");

            ScummGameData game = ScummGameData.LoadFromGameInfo(info);
            List<GameTextEntry> entries = GameTextManager.ExtractV8(game, GameTextCodec.Default());
            _output.WriteLine("COMI v8 in-container text entries: {0}", entries.Count);
            Assert.True(entries.Count > 0, "no v8 in-container text extracted");

            string tmp = Path.Combine(Path.GetTempPath(), "comi_v8_noop_text.txt");
            GameTextManager.ExportToFileV8(game, tmp, GameTextCodec.Default(), "COMI");
            GameTextImportReport report = GameTextManager.ImportFromFileV8(game, tmp);
            File.Delete(tmp);

            Assert.Empty(report.Errors);

            // A no-op import must not change a single byte of either data file.
            foreach (DataDisk disk in game.DataDisks)
            {
                using (var ms = new MemoryStream())
                {
                    disk.Tree.SaveToBinaryWriter(ms);
                    byte[] expected = File.ReadAllBytes(disk.FilePath);
                    byte[] actual = ms.ToArray();
                    Assert.True(expected.Length == actual.Length,
                        string.Format("{0}: length {1} != {2} after no-op text import", Path.GetFileName(disk.FilePath), expected.Length, actual.Length));
                    for (int i = 0; i < expected.Length; i++)
                    {
                        if (expected[i] != actual[i])
                        {
                            Assert.Fail(string.Format("{0}: byte differs at 0x{1:X} after a no-op text import (expected 0x{2:X2}, got 0x{3:X2})",
                                Path.GetFileName(disk.FilePath), i, expected[i], actual[i]));
                        }
                    }
                }
            }
        }

        [SkippableFact]
        public void EditedScriptTextSurvivesReExtraction()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(info == null, "COMI (v8) not present");

            ScummGameData game = ScummGameData.LoadFromGameInfo(info);

            // Export, then lengthen the first entry that has real (letter-bearing) text after any /TAG/.
            string tmp = Path.Combine(Path.GetTempPath(), "comi_v8_edit_text.txt");
            GameTextManager.ExportToFileV8(game, tmp, GameTextCodec.Default(), "COMI");
            string[] lines = File.ReadAllLines(tmp);

            string editedId = null;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0 || lines[i][0] == ';') continue;
                int eq = lines[i].IndexOf(" = ");
                if (eq <= 0) continue;
                string id = lines[i].Substring(0, eq);
                string text = lines[i].Substring(eq + 3);
                // a "translatable" line: has ASCII letters and no escape/glyph tokens to disturb
                if (text.Length >= 3 && text.Any(char.IsLetter) && !text.Contains("{"))
                {
                    lines[i] = id + " = " + text + " ZZ";
                    editedId = id;
                    break;
                }
            }
            Skip.If(editedId == null, "no plain-text v8 entry to edit");
            File.WriteAllLines(tmp, lines);

            GameTextImportReport report = GameTextManager.ImportFromFileV8(game, tmp);
            File.Delete(tmp);

            _output.WriteLine("edited {0}; changed={1} errors={2}", editedId, report.StringsChanged, report.Errors.Count);
            Assert.Empty(report.Errors);
            Assert.True(report.StringsChanged >= 1, "the edit was not applied");

            // Re-extract: the edited text must be present (the block rebuilt and still disassembles).
            List<GameTextEntry> after = GameTextManager.ExtractV8(game, GameTextCodec.Default());
            Assert.Contains(after, e => e.Text.EndsWith(" ZZ"));
        }
    }
}
