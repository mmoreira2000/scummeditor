using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) OBCD verb-code text editing (task #11). v8 uses an 8-byte verb
    /// offset table ([id:32le][offset:32le], 0-id terminator; offsets relative to the VERB body), unlike the
    /// v5/v6/v7 3-byte table. These verify the verb-code text is enumerated, every verb offset table is
    /// self-consistent (each entry point decodes cleanly to the end of the verb code), and that a
    /// size-changing text edit survives + relocates the table so every entry still points at a valid
    /// instruction boundary.
    /// </summary>
    public class V8VerbTests
    {
        private readonly ITestOutputHelper _out;
        public V8VerbTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void VerbCodeTextEnumeratedAndEveryTableIsSelfConsistent()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            List<GameTextEntry> entries = GameTextManager.ExtractV8(game, GameTextCodec.Default());
            List<GameTextEntry> verbText = entries.Where(IsVerbSource).ToList();
            _out.WriteLine("v8 OBCD verb-code text entries: {0}", verbText.Count);
            Assert.True(verbText.Count > 0, "v8 OBCD verb-code text was not enumerated (the stride-8 table parse failed)");

            int obcd, verbs;
            AssertAllVerbTablesConsistent(game, out obcd, out verbs);
            _out.WriteLine("v8 OBCDs with verb code: {0}; verb entry points checked: {1}", obcd, verbs);
            Assert.True(obcd > 100 && verbs > 100, "too few v8 verb tables exercised");
        }

        [SkippableFact]
        public void EditedVerbCodeTextSurvivesAndRelocatesTheTable()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(info == null, "COMI (v8) not present");
            ScummGameData game = ScummGameData.LoadFromGameInfo(info);

            // Export, then lengthen the first plain (letter-bearing, token-free) verb-code string.
            string tmp = Path.Combine(Path.GetTempPath(), "comi_v8_verb_edit.txt");
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
                if (!IsVerbId(id)) continue; // only an OBCD verb-code source
                // Append " ZZ" at the END (a size-changing edit). A leading /TAG/ voice ref is fine - it
                // stays at the start; only escape-token strings ({...}) are skipped to keep the edit simple.
                if (text.Length >= 3 && text.Any(char.IsLetter) && !text.Contains("{"))
                {
                    lines[i] = id + " = " + text + " ZZ"; // a real, size-changing edit
                    editedId = id;
                    break;
                }
            }
            Skip.If(editedId == null, "no plain v8 verb-code text to edit");
            File.WriteAllLines(tmp, lines);
            _out.WriteLine("editing verb-code string: {0}", editedId);

            GameTextImportReport report = GameTextManager.ImportFromFileV8(game, tmp);
            File.Delete(tmp);
            Assert.Empty(report.Errors);
            Assert.True(report.StringsChanged >= 1, "the verb-code edit was not applied");

            // The edited text re-extracts (the OBCD rebuilt + still disassembles)...
            List<GameTextEntry> after = GameTextManager.ExtractV8(game, GameTextCodec.Default());
            Assert.Contains(after, e => IsVerbSource(e) && e.Text.EndsWith(" ZZ"));

            // ...and every verb table is STILL self-consistent: the size-changing edit shifted the code, so
            // the offset table must have been relocated; a stale entry would now point mid-instruction and
            // fail to decode cleanly.
            int obcd, verbs;
            AssertAllVerbTablesConsistent(game, out obcd, out verbs);
            _out.WriteLine("after edit: verb tables consistent across {0} OBCDs / {1} entries", obcd, verbs);
        }

        /// <summary>Every v8 OBCD verb offset table must point each verb at a clean instruction boundary:
        /// disassembling the verb code from each entry's position decodes to the end without desync.</summary>
        private static void AssertAllVerbTablesConsistent(ScummGameData game, out int obcdCount, out int verbCount)
        {
            obcdCount = 0; verbCount = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                foreach (DiskBlock lflf in disk.Tree.GetLFLFs())
                {
                    foreach (ObjectCode obcd in AllObcd(lflf))
                    {
                        if (obcd.VerbCodeOffset < 0 || obcd.VerbCodeLength <= 0 || obcd.VerbEntries.Count == 0) continue;
                        obcdCount++;

                        var slice = new byte[obcd.VerbCodeLength];
                        Array.Copy(obcd.RawContent, obcd.VerbCodeOffset, slice, 0, obcd.VerbCodeLength);

                        foreach (VerbEntry ve in obcd.VerbEntries)
                        {
                            int rel = obcd.VerbEntryBase + ve.Offset - obcd.VerbCodeOffset; // position within the verb code
                            Assert.InRange(rel, 0, obcd.VerbCodeLength - 1);
                            ScummV6Disassembler.Result r = ScummV8Disassembler.Disassemble(slice, rel);
                            Assert.True(r.DecodedToEnd,
                                string.Format("verb id {0}: entry point @{1} did not decode cleanly to the end", ve.Id, rel));
                            verbCount++;
                        }
                    }
                }
            }
        }

        private static bool IsVerbSource(GameTextEntry e) { return IsVerbId(e.Id); }
        private static bool IsVerbId(string id) { return id.Contains(".OBJ") || id.Contains(".OBC"); }

        private static IEnumerable<ObjectCode> AllObcd(BlockBase b)
        {
            var oc = b as ObjectCode;
            if (oc != null) yield return oc;
            foreach (BlockBase c in b.Childrens)
                foreach (ObjectCode x in AllObcd(c)) yield return x;
        }
    }
}
