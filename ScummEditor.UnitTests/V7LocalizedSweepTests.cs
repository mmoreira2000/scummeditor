using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Structures;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Library-wide adversarial sweep: runs the real LanguageBundleFile / TrsFile code against EVERY
    /// LANGUAGE.BND and .TRS in the whole v7 game library (all 24 editions, incl. the CJK bundles under
    /// VIDEO/ and the Full Throttle per-scene files), not just the two Portuguese files the focused suite
    /// covers. For each file it asserts: (1) a no-op BuildContent is byte-identical to the original; (2) an
    /// export -> re-import-unchanged -> BuildContent is byte-identical; (3) a structural edit (prepend a
    /// marker to the first entry) survives a rebuild+reload with the entry count and the neighbour entry
    /// intact. Collects ALL failures before asserting so the report is exhaustive. Skips when the
    /// (git-ignored) game library is absent.
    /// </summary>
    public class V7LocalizedSweepTests
    {
        private readonly ITestOutputHelper _out;
        public V7LocalizedSweepTests(ITestOutputHelper output) { _out = output; }

        private const string Marker = "ZZ9_EDIT_";

        [SkippableFact]
        public void SweepEveryLanguageBundle()
        {
            string root = GameLibrary.Folder("ScummV7");
            Skip.If(root == null, "ScummV7 game library not present");

            string[] files = Directory.GetFiles(root, "LANGUAGE.BND", SearchOption.AllDirectories);
            Skip.If(files.Length == 0, "no LANGUAGE.BND found");

            var failures = new List<string>();
            int encoded = 0, plain = 0, totalEntries = 0;

            foreach (string path in files.OrderBy(p => p))
            {
                string rel = path.Substring(root.Length + 1);
                try
                {
                    var bnd = new LanguageBundleFile(path);
                    bnd.Load(File.ReadAllBytes(path));
                    if (bnd.Encoded) encoded++; else plain++;
                    totalEntries += bnd.Entries.Count;

                    if (!bnd.BuildContent().SequenceEqual(bnd.OriginalContent))
                        failures.Add("[BND no-op build differs] " + rel);

                    string dump = bnd.ExportToText();
                    bnd.ImportFromText(dump);
                    if (!bnd.BuildContent().SequenceEqual(bnd.OriginalContent))
                        failures.Add("[BND export/import no-op differs] " + rel);

                    EditCheck(rel, "BND", bnd.Entries, bnd.BuildContent, failures, b =>
                    {
                        var r = new LanguageBundleFile(path); r.Load(b); return r.Entries;
                    });
                }
                catch (Exception ex)
                {
                    failures.Add("[BND THREW] " + rel + " : " + ex.GetType().Name + " " + ex.Message);
                }
            }

            _out.WriteLine($"LANGUAGE.BND swept: {files.Length} files, {encoded} encoded / {plain} plain, {totalEntries} entries total");
            Assert.True(failures.Count == 0, "Failures:\n" + string.Join("\n", failures));
        }

        [SkippableFact]
        public void SweepEveryTrs()
        {
            string root = GameLibrary.Folder("ScummV7");
            Skip.If(root == null, "ScummV7 game library not present");

            string[] files = Directory.GetFiles(root, "*.TRS", SearchOption.AllDirectories);
            Skip.If(files.Length == 0, "no .TRS found");

            var failures = new List<string>();
            int etrs = 0, plain = 0, noEntries = 0, totalEntries = 0;

            foreach (string path in files.OrderBy(p => p))
            {
                string rel = path.Substring(root.Length + 1);
                try
                {
                    var trs = new TrsFile(path);
                    trs.Load(File.ReadAllBytes(path));
                    if (trs.Encoded) etrs++; else plain++;
                    if (trs.Entries.Count == 0) noEntries++;
                    totalEntries += trs.Entries.Count;

                    if (!trs.BuildContent().SequenceEqual(trs.OriginalContent))
                        failures.Add("[TRS no-op build differs] " + rel);

                    string dump = trs.ExportToText();
                    trs.ImportFromText(dump);
                    if (!trs.BuildContent().SequenceEqual(trs.OriginalContent))
                        failures.Add("[TRS export/import no-op differs] " + rel);

                    EditCheck(rel, "TRS", trs.Entries, trs.BuildContent, failures, b =>
                    {
                        var r = new TrsFile(path); r.Load(b); return r.Entries;
                    });
                }
                catch (Exception ex)
                {
                    failures.Add("[TRS THREW] " + rel + " : " + ex.GetType().Name + " " + ex.Message);
                }
            }

            _out.WriteLine($".TRS swept: {files.Length} files, {etrs} ETRS / {plain} plain, {noEntries} with no #define entries, {totalEntries} entries total");
            Assert.True(failures.Count == 0, "Failures:\n" + string.Join("\n", failures));
        }

        /// <summary>Prepends a marker to the first entry, rebuilds, reloads, and checks the edit survived,
        /// the entry count is unchanged, and the second entry is byte-for-byte the same as before.</summary>
        private static void EditCheck(string rel, string kind, IReadOnlyList<LocalizedTextEntry> entries,
            Func<byte[]> build, List<string> failures, Func<byte[], IReadOnlyList<LocalizedTextEntry>> reload)
        {
            if (entries.Count < 2) return; // need a neighbour to check
            string before0 = entries[0].Text;
            string before1Key = entries[1].Key, before1Text = entries[1].Text;

            entries[0].Text = Marker + before0;       // prepend = safe (never touches the next-entry boundary)
            IReadOnlyList<LocalizedTextEntry> after = reload(build());
            entries[0].Text = before0;                // restore the in-memory state

            if (after.Count != entries.Count)
            { failures.Add($"[{kind} edit changed entry count {entries.Count}->{after.Count}] {rel}"); return; }
            if (after[0].Text != Marker + before0)
                failures.Add($"[{kind} edit did not survive] {rel} got=\"{Truncate(after[0].Text)}\"");
            if (after[1].Key != before1Key || after[1].Text != before1Text)
                failures.Add($"[{kind} edit disturbed neighbour] {rel}");
        }

        private static string Truncate(string s)
        {
            s = (s ?? "").Replace("\r", "\\r").Replace("\n", "\\n");
            return s.Length > 40 ? s.Substring(0, 40) + "..." : s;
        }
    }
}
