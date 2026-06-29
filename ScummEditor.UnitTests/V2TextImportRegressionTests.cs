using System;
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
    /// Regression tests for the v1/v2 (Maniac Mansion, Zak McKracken) translation text pipeline, covering two
    /// bugs found while translating Maniac Mansion Enhanced (v2):
    ///   A. An UNEDITED re-import must be a true no-op. The v1/v2 codec folds a trailing space into the
    ///      preceding glyph's 0x80 bit, so decode->encode of an untouched string is render-identical but not
    ///      byte-identical. The import must compare the decoded TEXT (not the re-encoded bytes), or every such
    ///      string is wrongly reported "changed" and its block rebuilt on a no-op.
    ///   B. A global script whose last (dead) instruction is cut a byte short by the resource boundary must
    ///      still be editable. The disassembler now treats a mid-instruction truncation at the very end of the
    ///      buffer (with no unknown opcode along the way) as decoded-to-end, since there are no strings or jump
    ///      targets past it and RebuildCode preserves the partial tail verbatim.
    /// </summary>
    public class V2TextImportRegressionTests
    {
        private readonly ITestOutputHelper _out;
        public V2TextImportRegressionTests(ITestOutputHelper o) { _out = o; }

        private static readonly string[] OldBundleGames =
        {
            GameLibrary.ManiacV2, GameLibrary.ZakV2, GameLibrary.ManiacV1, GameLibrary.ZakV1
        };

        // --- A: an unedited re-import changes nothing ---------------------------------------------------
        [SkippableFact]
        public void UneditedReimportIsATrueNoOp()
        {
            int tested = 0;
            foreach (string rel in OldBundleGames)
            {
                ScummGameData game = GameLibrary.Load(rel);
                if (game == null) continue;
                tested++;

                string tmp = Path.Combine(Path.GetTempPath(), "v12_noop_" + tested + ".txt");
                int exported = ScummV2TextManager.ExportToFile(game, tmp, "test");
                Assert.True(exported > 0, rel + ": nothing exported");

                // Re-import the exact file we just wrote, with no edits at all.
                GameTextImportReport rep = ScummV2TextManager.ImportFromFile(game, tmp);
                File.Delete(tmp);

                _out.WriteLine("{0}: exported {1}, no-op import -> changed={2} rebuilt={3} errors={4}",
                    rel, exported, rep.StringsChanged, rep.BlocksRebuilt, rep.Errors.Count);
                foreach (string e in rep.Errors.Take(5)) _out.WriteLine("   ERR: " + e);

                Assert.Equal(0, rep.StringsChanged);
                Assert.Equal(0, rep.BlocksRebuilt);
                Assert.Empty(rep.Errors);
            }
            Skip.If(tested == 0, "no v1/v2 GF_OLD_BUNDLE game present");
        }

        // --- D: verb code is extracted even when the object's NAME is packed BEFORE the verb code ---------
        [SkippableFact]
        public void VerbCodeStringsExtractedWhenNamePrecedesVerbCode()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.ManiacV2);
            Skip.If(game == null, "Maniac Mansion (v2) not present");

            List<GameTextEntry> entries = ScummV2TextManager.Extract(game, GameTextCodecV12.Portuguese());
            string all = string.Join("\n", entries.Select(e => e.Text));

            // These live in OBJECT VERB CODE whose object name string sits BEFORE the code (room 44 "placa"
            // sign; room 45 the character bios). Bounding the verb code above objptr used to stop at that
            // leading name and drop every string in the code; bounding above the last verb entry fixes it.
            Assert.Contains("Trespassers", all);          // the sign's "read" verb
            Assert.Contains("aspiring", all);             // Syd's bio
            Assert.Contains("physics club", all);         // Bernard's bio

            // Guard against the bug returning: bounding verb code above objptr dropped ~179 strings
            // (extraction fell to 941); the per-segment bound recovers them (~1120).
            Assert.True(entries.Count > 1050,
                "v2 extraction recovered too few entries (" + entries.Count + ") - the verb-code bounding bug may have regressed");
        }

        // SCUMM V1 shares ScummV2TextManager/ScummV2Room, so it had the IDENTICAL verb-code bug (the name is
        // always packed before the verb code in v1/v2, so 100% of objects were affected). Guard V1 too.
        [SkippableFact]
        public void V1VerbCodeIsExtracted()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.ManiacV1);
            Skip.If(game == null, "Maniac Mansion (v1) not present");

            List<GameTextEntry> entries = ScummV2TextManager.Extract(game, GameTextCodecV12.Portuguese());
            string all = string.Join("\n", entries.Select(e => e.Text));
            Assert.Contains("Nurse Edna", all);     // an object verb-code string (room 1 OBJ394 mailbox/sign)
            Assert.True(entries.Count > 1000,
                "v1 extraction recovered too few entries (" + entries.Count + ") - the verb-code bounding bug may have regressed");
        }

        // The export "; charmap:" header must omit any accent whose slot byte the game uses literally, so the
        // export does not show a false accent there and the header only advertises genuinely free slots.
        [SkippableFact]
        public void ExportCharmapOmitsAccentSlotsTheGameUsesLiterally()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.ManiacV2);
            Skip.If(game == null, "Maniac Mansion (v2) not present");

            string tmp = Path.Combine(Path.GetTempPath(), "v2_charmap_prune.txt");
            ScummV2TextManager.ExportToFile(game, tmp, "test");
            string[] lines = File.ReadAllLines(tmp);
            File.Delete(tmp);

            string charmap = lines.FirstOrDefault(l => l.Contains("charmap:"));
            Assert.NotNull(charmap);
            // '*' (0x2A) is used literally by the save-game UI labels, so its accent (u-acute) is pruned...
            Assert.DoesNotContain("0x2A", charmap);
            Assert.DoesNotContain("0x2a", charmap);
            // ...while accents on slots the game does NOT use are kept (e.g. a-acute on 0x7E).
            Assert.Contains("0x7E", charmap);
            // ...and the save-UI text is exported with the literal '*' (optionally a folded space before it),
            // never as a false u-acute accent.
            string all = string.Join("\n", lines);
            Assert.DoesNotContain("Game Aú", all);   // no false 'u-acute' from the pruned 0x2A slot
            Assert.Matches(@"Game A ?\*", all);           // the literal '*' is preserved
        }

        // --- B: a script with a dead, byte-short trailing instruction is still editable -----------------
        [SkippableFact]
        public void ScriptWithTruncatedTrailingInstructionIsStillEditable()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.ManiacV2);
            Skip.If(game == null, "Maniac Mansion (v2) not present");

            string tmp = Path.Combine(Path.GetTempPath(), "v2_trunc_edit.txt");
            ScummV2TextManager.ExportToFile(game, tmp, "test");
            string[] lines = File.ReadAllLines(tmp);

            // Edit the first plain (letter-bearing, token-free, slash-free) string of the SC059 block - the
            // one that ends in a dead, byte-short `delay` (the bug B repro). Append a letter: a size-changing
            // edit that forces the rebuild + the decode-to-end gate.
            string editedId = null;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0 || lines[i][0] == ';') continue;
                int eq = lines[i].IndexOf(" = ");
                if (eq <= 0) continue;
                string id = lines[i].Substring(0, eq);
                string text = lines[i].Substring(eq + 3);
                if (!id.StartsWith("007.SC059.")) continue;
                if (text.Length >= 2 && text.Any(char.IsLetter) && !text.Contains('{') && !text.Contains('/'))
                {
                    lines[i] = id + " = " + text + " X";
                    editedId = id;
                    break;
                }
            }
            Skip.If(editedId == null, "SC059 has no plain editable string in this build of the game");
            File.WriteAllLines(tmp, lines);
            _out.WriteLine("editing {0}", editedId);

            GameTextImportReport rep = ScummV2TextManager.ImportFromFile(game, tmp);
            File.Delete(tmp);

            foreach (string e in rep.Errors) _out.WriteLine("   ERR: " + e);
            // The whole point: SC059 must NOT be excluded with a "does not decode to the end" error anymore.
            Assert.DoesNotContain(rep.Errors, e => e.Contains("007.SC059") && e.Contains("decode to the end"));
            Assert.True(rep.StringsChanged >= 1, "the SC059 edit was not applied");

            // The edit re-extracts (the block was rebuilt and still disassembles cleanly).
            List<GameTextEntry> after = ScummV2TextManager.Extract(game, GameTextCodecV12.Portuguese());
            Assert.Contains(after, e => e.Id == editedId && e.Text.EndsWith(" X"));
        }

        // --- B (unit): the disassembler tolerates a mid-instruction truncation at the buffer end ---------
        [Fact]
        public void DisassemblerTreatsTrailingTruncationAsDecodedToEnd()
        {
            // printEgo "Hi" (0xD8 + "Hi" + NUL), then a `delay` (0x2E) with only 2 of its 3 operand bytes:
            // the buffer ends mid-instruction, exactly like a dead trailing `delay` cut short by the resource
            // boundary. The string before it must be found and the slice must count as decoded-to-end.
            byte[] code = { 0xD8, (byte)'H', (byte)'i', 0x00, 0x2E, 0x00, 0xA0 };
            ScummV6Disassembler.Result r = ScummV12Disassembler.Disassemble(code, 0, null, false);

            Assert.True(r.DecodedToEnd, "a trailing mid-instruction truncation should count as decoded-to-end");
            Assert.Empty(r.UnknownOpcodes);
            Assert.Single(r.Strings);
            Assert.Equal(1, r.Strings[0].Offset);
            Assert.Contains("truncated", r.Listing);
        }
    }
}
