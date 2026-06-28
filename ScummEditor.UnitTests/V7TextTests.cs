using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Engine;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 (The Dig, Full Throttle) text pipeline: the scripts/objects are typed (ScriptBlock /
    /// ObjectCode) so GameTextManager can extract and re-import their strings using the v6-compatible
    /// disassembler (extended for the v7 opcodes/wait sub-ops and the 2-byte LSCR id). These tests prove
    /// every script and verb decodes to the end (a prerequisite for safe import), that a no-op text
    /// round-trip is byte-identical, and that a real edit applies and re-verifies.
    /// </summary>
    public class V7TextTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void AllScriptsAndVerbsDecodeToEnd(string path)
        {
            Skip.If(GameLibrary.Folder(path) == null, "not present: " + path);
            ScummGameData game = GameLibrary.Load(path);

            int scripts = 0, scriptsOk = 0, verbs = 0, verbsOk = 0;
            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                var blocks = new List<BlockBase>(disk.Childrens);
                RoomBlock room = disk.GetROOM();
                if (room != null) blocks.AddRange(room.Childrens);

                foreach (BlockBase b in blocks)
                {
                    var s = b as ScriptBlock;
                    if (s != null)
                    {
                        scripts++;
                        if (s.Disassemble().DecodedToEnd) scriptsOk++;
                    }

                    var obcd = b as ObjectCode;
                    if (obcd != null && obcd.VerbCodeOffset >= 0 && obcd.VerbCodeLength > 0)
                    {
                        verbs++;
                        var slice = new byte[obcd.VerbCodeLength];
                        Array.Copy(obcd.RawContent, obcd.VerbCodeOffset, slice, 0, obcd.VerbCodeLength);
                        if (ScummV6Disassembler.Disassemble(slice, 0).DecodedToEnd) verbsOk++;
                    }
                }
            }

            Assert.True(scripts > 100, "too few scripts found: " + scripts);
            Assert.True(scriptsOk == scripts, string.Format("only {0}/{1} scripts decoded to the end", scriptsOk, scripts));
            Assert.True(verbsOk == verbs, string.Format("only {0}/{1} verb-code blocks decoded to the end", verbsOk, verbs));
        }

        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void TextNoOpRoundTripIsByteIdentical(string path)
        {
            Skip.If(GameLibrary.Folder(path) == null, "not present: " + path);
            GameInfo info = GameLibrary.Detect(path);
            ScummGameData game = ScummGameData.LoadFromGameInfo(info);
            byte[] originalData = File.ReadAllBytes(info.DataFile);

            string file = Path.Combine(Path.GetTempPath(), "v7_text_noop.txt");
            int count = GameTextManager.ExportToFile(game.DataFile, file, GameTextCodec.Default(), "test");
            Assert.True(count > 1000, "too few text entries exported: " + count);

            // Re-importing the exact export must change nothing and round-trip the data byte-for-byte.
            GameTextImportReport report = GameTextManager.ImportFromFile(game.DataFile, file);
            Assert.Empty(report.Errors);
            Assert.Equal(0, report.StringsChanged);

            game.PostProcessChanges();
            byte[] saved = SaveToBytes(s => game.DataFile.SaveToBinaryWriter(s));
            Assert.True(BytesEqual(originalData, saved), "data file differs after a no-op text round-trip");

            try { File.Delete(file); } catch { }
        }

        [SkippableFact]
        public void SingleTextEditAppliesAndReverifies()
        {
            Skip.If(GameLibrary.Folder(GameLibrary.TheDig) == null, "The Dig not present");
            ScummGameData game = GameLibrary.Load(GameLibrary.TheDig);

            string file = Path.Combine(Path.GetTempPath(), "v7_text_edit.txt");
            GameTextManager.ExportToFile(game.DataFile, file, GameTextCodec.Default(), "test");

            // Lengthen the first plain-ASCII translatable line (exercises the jump remapping in
            // RebuildCode), keeping its id, then re-import.
            string[] lines = File.ReadAllLines(file);
            string editedId = null, newText = null;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0 || lines[i][0] == ';') continue;
                int eq = lines[i].IndexOf('=');
                if (eq <= 0) continue;
                string id = lines[i].Substring(0, eq).Trim();
                string text = lines[i].Substring(eq + 1).TrimStart();
                if (text.Length >= 4 && IsPlainAscii(text))
                {
                    editedId = id;
                    newText = text + " EDITADO";
                    lines[i] = id + " = " + newText;
                    break;
                }
            }
            Assert.NotNull(editedId);
            File.WriteAllLines(file, lines);

            GameTextImportReport report = GameTextManager.ImportFromFile(game.DataFile, file);
            Assert.Empty(report.Errors);
            Assert.True(report.StringsChanged >= 1, "no string was changed");

            // The edited text must now be what the pipeline extracts, and the rebuilt block must still
            // round-trip through save (offsets recomputed).
            List<GameTextEntry> after = GameTextManager.Extract(game.DataFile, GameTextCodec.Default());
            Assert.Contains(after, e => e.Id == editedId && e.Text == newText);
            Assert.Null(Record.Exception(() => game.PostProcessChanges()));

            try { File.Delete(file); } catch { }
        }

        private static bool IsPlainAscii(string s)
        {
            foreach (char c in s)
            {
                bool letter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                if (!letter && c != ' ') return false;
            }
            return true;
        }

        private static byte[] SaveToBytes(Action<Stream> save)
        {
            using (var ms = new MemoryStream()) { save(ms); return ms.ToArray(); }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
