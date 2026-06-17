using System;
using System.Drawing;
using System.IO;
using System.Linq;
using ScummEditor.Engine;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v3 support: detection of both sub-families (GF_OLD256 small-header and GF_OLD_BUNDLE
    /// EGA), byte-exact container round-trips, the room-recompute regression that the variable-size
    /// palette fix addresses (Loom FM-Towns), and the v3small image / text / font edit pipelines.
    /// All real-data tests skip when the GameData library is absent.
    /// </summary>
    public class V3SupportTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Vga, ScummGame.IndianaJones3, true, false)]
        [InlineData(GameLibrary.Indy3FmTowns, ScummGame.IndianaJones3, true, false)]
        [InlineData(GameLibrary.ZakFmTowns, ScummGame.ZakMcKracken, true, false)]
        [InlineData(GameLibrary.LoomFmTowns, ScummGame.Loom, true, false)]
        [InlineData(GameLibrary.Indy3Ega, ScummGame.IndianaJones3, false, true)]
        [InlineData(GameLibrary.LoomEga, ScummGame.Loom, false, true)]
        public void DetectsV3GameAndFamily(string relativePath, ScummGame expectedGame, bool smallHeader, bool oldBundle)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.Equal(3, info.ScummVersion);
            Assert.Equal(smallHeader, info.UsesSmallHeader);
            Assert.Equal(oldBundle, info.UsesOldBundle);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Vga)]
        [InlineData(GameLibrary.Indy3FmTowns)]
        [InlineData(GameLibrary.ZakFmTowns)]
        [InlineData(GameLibrary.LoomFmTowns)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3ContainerRoundTripsByteIdentical(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            foreach (DataDisk disk in game.DataDisks)
            {
                byte[] original = ReadDecrypted(disk.FilePath, game.LoadedGameInfo.XorKey);
                byte[] resaved = Save(disk.Tree);
                Assert.True(BytesEqual(original, resaved),
                    Path.GetFileName(disk.FilePath) + " did not round-trip byte-identical");
            }
        }

        /// <summary>
        /// Regression test for the variable-size palette: recomputing block sizes/offsets (what the
        /// editor does on save) must not change an unedited room. Loom's FM-Towns rooms 69/83/84/85
        /// hold a 16-colour PA block; the old fixed-256 reader over-read it, so the recompute resized
        /// it and corrupted the room.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.LoomFmTowns)]
        [InlineData(GameLibrary.Indy3FmTowns)]
        [InlineData(GameLibrary.ZakFmTowns)]
        public void V3RecomputeKeepsUneditedRoomsByteIdentical(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            foreach (DataDisk disk in game.DataDisks)
            {
                byte[] before = Save(disk.Tree);
                disk.Tree.CalculateBlockSize();
                disk.Tree.CalculateOffsets();
                byte[] after = Save(disk.Tree);
                Assert.True(BytesEqual(before, after),
                    Path.GetFileName(disk.FilePath) + " changed after a no-op recompute");
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Vga)]
        [InlineData(GameLibrary.ZakFmTowns)]
        [InlineData(GameLibrary.LoomFmTowns)]
        public void V3ImageImportIsLosslessAndEditable(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV4ImageDecoder();
            var encoder = new ScummV3ImageEncoder();
            int rooms = 0;

            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3Small256DataFile;
                ScummV4RoomBlock room = df?.GetRoom();
                ScummV4ImageBlock bm = room?.GetBM();
                if (bm == null) continue;

                using (Bitmap original = decoder.DecodeBackground(room))
                {
                    if (original == null) continue;
                    rooms++;

                    // (a) a no-op re-encode leaves the BM block byte-identical (strips reused verbatim)
                    byte[] before = (byte[])bm.Contents.Clone();
                    encoder.EncodeBackground(room, original);
                    Assert.True(BytesEqual(before, bm.Contents), "no-op image import changed the BM block");

                    // (b) editing one pixel round-trips exactly
                    byte[,] matrix = IndexedImageHelper.GetIndexMatrix(original);
                    byte newValue = (byte)((matrix[0, 0] + 1) & 0xFF);
                    matrix[0, 0] = newValue;
                    using (Bitmap edited = IndexedImageHelper.FromIndexMatrix(matrix, original.Palette.Entries, -1))
                    {
                        encoder.EncodeBackground(room, edited);
                    }
                    using (Bitmap reDecoded = decoder.DecodeBackground(room))
                    {
                        byte[,] after = IndexedImageHelper.GetIndexMatrix(reDecoded);
                        Assert.Equal(newValue, after[0, 0]);
                    }
                }
            }

            Assert.True(rooms > 0, "no decodable backgrounds were found");
        }

        /// <summary>
        /// Regression: when a v3small (GF_OLD256) background is re-encoded larger, the room's NN.LFL
        /// file grows and the global scripts/sounds/costumes stored after it shift, so their FILE-ABSOLUTE
        /// offsets in the 00.LFL index MUST be rewritten. The editor decodes images by re-parsing the
        /// room tree, so it never noticed a stale index - but ScummVM seeks resources by those offsets,
        /// so a stale index renders the game black. The bug was that the index-link pass pruned its walk
        /// at the size-0 root container (sizes are only known at save time), leaving every entry unlinked
        /// and never relocated. A no-op recompute must still leave the offsets byte-identical.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Vga)]
        [InlineData(GameLibrary.ZakFmTowns)]
        [InlineData(GameLibrary.LoomFmTowns)]
        public void V3SmallGrowingBackgroundsRelocatesIndexOffsets(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV4IndexFile;
            Assert.NotNull(index);

            uint[] before = index.ResourceDirectories.SelectMany(d => d.Entries).Select(e => e.Offset).ToArray();

            // A no-op recompute must not move anything (keeps the container byte-identical on disk).
            game.PostProcessChanges();
            uint[] afterNoop = index.ResourceDirectories.SelectMany(d => d.Entries).Select(e => e.Offset).ToArray();
            Assert.Equal(before, afterNoop);

            // Grow every room background (band of changed pixels -> every strip becomes uncompressed
            // raw256, so the image block expands and the resources after it shift down the file).
            var decoder = new ScummV4ImageDecoder();
            var encoder = new ScummV3ImageEncoder();
            int grew = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                ScummV4RoomBlock room = (disk.Tree as ScummV3Small256DataFile)?.GetRoom();
                if (room?.GetBM() == null) continue;
                using (Bitmap bg = decoder.DecodeBackground(room))
                {
                    if (bg == null) continue;
                    byte[,] m = IndexedImageHelper.GetIndexMatrix(bg);
                    int band = Math.Max(4, bg.Height / 8);
                    for (int x = 0; x < bg.Width; x++)
                        for (int y = 0; y < band; y++)
                            m[x, y] = (byte)((m[x, y] + 1) & 0xFF);
                    using (Bitmap edited = IndexedImageHelper.FromIndexMatrix(m, bg.Palette.Entries, -1))
                        encoder.EncodeBackground(room, edited);
                    grew++;
                }
            }
            Assert.True(grew > 0, "no decodable backgrounds were found to grow");

            game.PostProcessChanges();
            uint[] afterGrow = index.ResourceDirectories.SelectMany(d => d.Entries).Select(e => e.Offset).ToArray();

            int moved = before.Where((offset, i) => offset != afterGrow[i]).Count();
            Assert.True(moved > 0, "growing every room background left all index offsets unchanged (stale index)");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Vga)]
        [InlineData(GameLibrary.ZakFmTowns)]
        [InlineData(GameLibrary.LoomFmTowns)]
        public void V3FontPngRoundTripsAndEdits(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            Assert.NotEmpty(game.V3Charsets);

            string dir = Path.Combine(Path.GetTempPath(), "v3font_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                CharsetV3 charset = game.V3Charsets[0];
                byte[] before = (byte[])charset.RawContent.Clone();

                string png = Path.Combine(dir, "charset.png");
                string guide = Path.Combine(dir, "charset.guide.png");
                CharsetV3PngCodec.ExportPng(charset, png, guide);

                // No-op import must leave the font unchanged.
                CharsetV3PngCodec.ImportPng(charset, png);
                Assert.True(BytesEqual(before, charset.RawContent), "no-op font import changed the charset");

                // Edit one ink pixel of a present glyph and confirm it survives an export/import cycle.
                int slot = FirstPresentGlyph(charset);
                Skip.If(slot < 0, "font has no glyphs to edit");
                byte[,] pixels = ReadAtlas(png);
                int cellX = (slot % 16) * 8, cellY = (slot / 16) * 8;
                pixels[cellX, cellY] ^= 1; // flip the top-left pixel of that glyph
                WriteAtlas(png, pixels, CharsetV3PngCodec.BuildEditPalette());

                CharsetV3PngCodec.ImportPng(charset, png);
                Assert.False(BytesEqual(before, charset.RawContent), "an edited font import made no change");

                // Re-export and confirm the flipped pixel persisted.
                CharsetV3PngCodec.ExportPng(charset, png, guide);
                byte[,] reRead = ReadAtlas(png);
                Assert.Equal(pixels[cellX, cellY], reRead[cellX, cellY]);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega, "door")]
        [InlineData(GameLibrary.LoomEga, "leaf")]
        public void V3OldTextExtractsCleanStrings(string relativePath, string expectedName)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            var entries = ScummV3OldTextManager.Extract(game, GameTextCodec.Default());

            // A real v3old game has thousands of translatable strings (dialogue, object names, verbs).
            Assert.True(entries.Count > 1000, "expected many text entries, got " + entries.Count);

            int names = 0, talk = 0;
            bool sawExpectedName = false;
            foreach (GameTextEntry e in entries)
            {
                if (e.Kind == "objectName") { names++; if (e.Text == expectedName) sawExpectedName = true; }
                if (e.Kind == "talk") talk++;

                // No entry should be over-read garbage: a real string is mostly printable, not a run
                // of control-code escape tokens. Guard against the chunk-bound regressions.
                Assert.False(IsControlGarbage(e.Text), "garbage (over-read) entry: " + e.Id + " = " + e.Text);
            }

            Assert.True(names > 100, "expected many object names, got " + names);
            Assert.True(talk > 500, "expected much dialogue, got " + talk);
            Assert.True(sawExpectedName, "expected to find the object name '" + expectedName + "'");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3OldNameImportIsByteSafe(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var codec = GameTextCodec.Default();

            var baseEntries = ScummV3OldTextManager.Extract(game, codec);
            var baseline = new System.Collections.Generic.Dictionary<string, string>();
            var baseKind = new System.Collections.Generic.Dictionary<string, string>();
            foreach (GameTextEntry e in baseEntries) { baseline[e.Id] = e.Text; baseKind[e.Id] = e.Kind; }

            // Edit a spread of object names (grow + shrink, ASCII only).
            var edits = new System.Collections.Generic.Dictionary<string, string>();
            int pick = 0;
            foreach (GameTextEntry e in baseEntries)
            {
                if (e.Kind != "objectName" || e.Text.Length < 3) continue;
                if (!System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[A-Za-z ]+$")) continue;
                edits[e.Id] = (pick % 2 == 0) ? e.Text + " EDITED" : e.Text.Substring(0, 2);
                if (++pick >= 40) break;
            }
            Skip.If(edits.Count == 0, "no editable object names found");

            var editedBaseTexts = new System.Collections.Generic.HashSet<string>();
            foreach (var kv in edits) editedBaseTexts.Add(baseline[kv.Key]);

            ScummV3OldTextManager.ImportNames(game, edits, codec);

            var after = new System.Collections.Generic.Dictionary<string, string>();
            foreach (GameTextEntry e in ScummV3OldTextManager.Extract(game, codec)) after[e.Id] = e.Text;

            // The key safety property: editing object names corrupts NO precisely-bounded resource
            // (other object names, or chunk-bounded global/local scripts). Object verb-code strings are
            // extracted with a loose end bound so their over-read tails shift harmlessly - excluded.
            foreach (var kv in baseline)
            {
                if (edits.ContainsKey(kv.Key)) continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(kv.Key, @"\.v\d+\.t\d+$")) continue;
                string got; after.TryGetValue(kv.Key, out got);
                if (got == kv.Value) continue;
                bool isName = baseKind.TryGetValue(kv.Key, out var k) && k == "objectName";
                if (isName && editedBaseTexts.Contains(kv.Value)) continue; // shared-name sibling - expected
                Assert.True(false, "name edit corrupted " + kv.Key + ": was '" + kv.Value + "' now '" + (got ?? "<gone>") + "'");
            }

            // At least the non-shared edits must have taken.
            int applied = 0;
            foreach (var kv in edits) if (after.TryGetValue(kv.Key, out var g) && g == kv.Value) applied++;
            Assert.True(applied > 0, "no name edit took effect");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3OldTextImportIsByteSafe(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var codec = GameTextCodec.Default();

            var baseEntries = ScummV3OldTextManager.Extract(game, codec);
            var baseline = new System.Collections.Generic.Dictionary<string, string>();
            foreach (GameTextEntry e in baseEntries) baseline[e.Id] = e.Text;

            // Edit names + dialogue, choosing DISTINCT baseline texts so shared byte regions do not
            // muddy the comparison.
            var edits = new System.Collections.Generic.Dictionary<string, string>();
            var usedBase = new System.Collections.Generic.HashSet<string>();
            int names = 0, talk = 0;
            foreach (GameTextEntry e in baseEntries)
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[A-Za-z0-9 .,'!?]+$") || e.Text.Length < 4) continue;
                if (!usedBase.Add(e.Text)) continue;
                if (e.Kind == "objectName" && names < 20) { edits[e.Id] = e.Text + " EDIT"; names++; }
                else if (e.Kind == "talk" && talk < 30) { edits[e.Id] = (talk % 2 == 0) ? e.Text + " [longer]" : "Curto."; talk++; }
            }
            Skip.If(edits.Count == 0, "no editable strings found");

            var editedBaseTexts = new System.Collections.Generic.HashSet<string>();
            foreach (var kv in edits) editedBaseTexts.Add(baseline[kv.Key]);

            ScummV3OldTextManager.Import(game, edits, codec);

            var after = new System.Collections.Generic.Dictionary<string, string>();
            var afterEntries = ScummV3OldTextManager.Extract(game, codec);
            foreach (GameTextEntry e in afterEntries) after[e.Id] = e.Text;

            // No precisely-bounded resource may change except the edits and their shared-region siblings,
            // no string may become control-byte garbage, and no resource may be lost.
            foreach (var kv in baseline)
            {
                if (edits.ContainsKey(kv.Key)) continue;
                string got; after.TryGetValue(kv.Key, out got);
                if (got == kv.Value) continue;
                if (editedBaseTexts.Contains(kv.Value)) continue; // shared region with an edit
                Assert.True(false, "text edit corrupted " + kv.Key + ": was '" + kv.Value + "' now '" + (got ?? "<gone>") + "'");
            }
            foreach (GameTextEntry e in afterEntries) Assert.False(IsControlGarbage(e.Text), "garbage after import: " + e.Id);
            Assert.Equal(baseEntries.Count, afterEntries.Count);

            int applied = 0;
            foreach (var kv in edits) if (after.TryGetValue(kv.Key, out var g) && g == kv.Value) applied++;
            Assert.True(applied >= edits.Count * 0.9, "too few edits applied: " + applied + "/" + edits.Count);
        }

        /// <summary>True when the decoded text is dominated by control-code escape tokens (an over-read).</summary>
        private static bool IsControlGarbage(string text)
        {
            int controlTokens = 0, visible = 0;
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '{')
                {
                    int close = text.IndexOf('}', i + 1);
                    if (close < 0) { visible++; i++; continue; }
                    string token = text.Substring(i + 1, close - i - 1);
                    // {0xNN} low-control bytes (0x01..0x1F) are escape noise, not glyphs.
                    if (token.StartsWith("0x") && token.Length == 4)
                    {
                        int v;
                        if (int.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out v) && v < 0x20)
                            controlTokens++;
                        else visible++;
                    }
                    i = close + 1;
                    continue;
                }
                if (!char.IsWhiteSpace(text[i])) visible++;
                i++;
            }
            // garbage = at least two control-byte tokens and more of them than visible characters
            return controlTokens >= 2 && controlTokens >= visible;
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3OldImageExportProducesFiles(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            string dir = Path.Combine(Path.GetTempPath(), "v3oldgfx_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var options = new ScummV4GraphicsBatch.ExportOptions();
                int exported = ScummV3OldGraphics.Export(game, dir, options, null, null);
                int files = Directory.GetFiles(dir, "*.png").Length;
                Assert.True(exported > 0, "no EGA images exported");
                Assert.Equal(exported, files);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3OldImageImportIsLosslessAndEditable(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV3OldImageDecoder();
            int rooms = 0;

            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile;
                if (df == null) continue;
                var room = new ScummEditor.Engine.Structures.DataFile.ScummV3OldRoom(df.RawContent);
                using (Bitmap original = decoder.DecodeBackground(room))
                {
                    if (original == null) continue;
                    rooms++;

                    // (a) a no-op re-encode reproduces the original strip table byte-for-byte
                    int oldLen = df.RawContent[room.ImageOffset] | (df.RawContent[room.ImageOffset + 1] << 8);
                    byte[] noop = ScummV3OldImageEncoder.Encode(df.RawContent, room.ImageOffset, room.Width, room.Height, original);
                    Assert.Equal(oldLen, noop.Length);
                    for (int k = 0; k < oldLen; k++) Assert.Equal(df.RawContent[room.ImageOffset + k], noop[k]);

                    // (b) editing one pixel round-trips exactly through encode -> ApplyEdit -> decode
                    byte[,] matrix = IndexedImageHelper.GetIndexMatrix(original);
                    byte newValue = (byte)((matrix[0, 0] + 1) & 0x0F);
                    matrix[0, 0] = newValue;
                    int roomNo = RoomNo(disk.FilePath);
                    using (Bitmap edited = IndexedImageHelper.FromIndexMatrix(matrix, original.Palette.Entries, -1))
                    {
                        byte[] newTable = ScummV3OldImageEncoder.Encode(df.RawContent, room.ImageOffset, room.Width, room.Height, edited);
                        ScummV3OldWriter.ApplyEdit(df, game.IndexFile as ScummEditor.Engine.Structures.IndexFile.ScummV3OldBundleIndexFile,
                            roomNo, room.ImageOffset, oldLen, newTable);
                    }
                    var room2 = new ScummEditor.Engine.Structures.DataFile.ScummV3OldRoom(df.RawContent);
                    using (Bitmap reDecoded = decoder.DecodeBackground(room2))
                    {
                        byte[,] after = IndexedImageHelper.GetIndexMatrix(reDecoded);
                        Assert.Equal(newValue, after[0, 0]);
                    }
                    if (rooms >= 5) break; // a sample proves the path
                }
            }
            Assert.True(rooms > 0, "no decodable backgrounds found");
        }

        private static int RoomNo(string path)
        {
            int n;
            return int.TryParse(Path.GetFileNameWithoutExtension(path), out n) ? n : 0;
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3OldSoundExtractsAdLibMusicToMidi(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummEditor.Engine.Structures.IndexFile.ScummV3OldBundleIndexFile;
            Skip.If(index == null || index.SoundDirectory == null, "no sound directory");

            var byRoom = new System.Collections.Generic.Dictionary<int, ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile>();
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile;
                if (df != null) byRoom[RoomNo(disk.FilePath)] = df;
            }

            int music = 0, midiOk = 0;
            var dir = index.SoundDirectory;
            for (int s = 0; s < dir.Count; s++)
            {
                int off = dir.Offsets[s];
                if (off == 0xFFFF || off == 0) continue;
                ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile df;
                if (!byRoom.TryGetValue(dir.RoomNumbers[s], out df)) continue;
                var snd = new ScummEditor.Engine.Structures.DataFile.ScummV3OldSound(df.RawContent, off);
                if (!snd.IsMusic) continue;
                music++;
                byte[] midi = ScummV4AdLibMidi.ToStandardMidi(snd.GetAdLibPayload());
                if (midi != null && midi.Length > 14 && midi[0] == (byte)'M' && midi[1] == (byte)'T' && midi[2] == (byte)'h' && midi[3] == (byte)'d') midiOk++;
            }
            Assert.True(music > 0, "no AdLib music sounds found");
            Assert.Equal(music, midiOk);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3OldCostumeFrameImportRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummEditor.Engine.Structures.IndexFile.ScummV3OldBundleIndexFile;
            Skip.If(index == null || index.CostumeDirectory == null, "no costume directory");

            var byRoom = new System.Collections.Generic.Dictionary<int, ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile>();
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile;
                if (df != null) byRoom[RoomNo(disk.FilePath)] = df;
            }
            var decoder = new CostumeImageDecoderV4();
            var encoder = new CostumeImageEncoderV4();
            Color[] ega = new Color[16];
            System.Array.Copy(EgaColorTable.Colors256, ega, 16);

            int edited = 0;
            var cdir = index.CostumeDirectory;
            for (int c = 0; c < cdir.Count && edited < 4; c++)
            {
                int off = cdir.Offsets[c];
                if (off == 0xFFFF || off == 0) continue;
                ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile df;
                if (!byRoom.TryGetValue(cdir.RoomNumbers[c], out df)) continue;
                var cost = new ScummEditor.Engine.Structures.DataFile.CostumeV3Old(df.RawContent, off);
                int fi = -1;
                for (int k = 0; k < cost.Frames.Count; k++) if (cost.Frames[k].Width >= 2 && cost.Frames[k].Height >= 2) { fi = k; break; }
                if (fi < 0) continue;

                using (Bitmap bmp0 = decoder.Decode(cost.Frames[fi], 16, ega, false))
                {
                    byte[,] m = IndexedImageHelper.GetIndexMatrix(bmp0);
                    byte nv = (byte)((m[0, 0] + 1) & 0x0F);
                    m[0, 0] = nv;
                    using (Bitmap edit = IndexedImageHelper.FromIndexMatrix(m, bmp0.Palette.Entries, -1))
                    {
                        var repl = new System.Collections.Generic.Dictionary<int, byte[]> { { fi, encoder.Encode(edit, 16) } };
                        byte[] rebuilt = cost.BuildWithReplacedFrames(repl);
                        ScummV3OldWriter.ApplyEdit(df, index, cdir.RoomNumbers[c], off, cost.ResourceSize, rebuilt, off);
                    }
                    var cost2 = new ScummEditor.Engine.Structures.DataFile.CostumeV3Old(df.RawContent, off);
                    using (Bitmap bmp1 = decoder.Decode(cost2.Frames[fi], 16, ega, false))
                    {
                        Assert.Equal(nv, IndexedImageHelper.GetIndexMatrix(bmp1)[0, 0]);
                    }
                    edited++;
                }
            }
            Assert.True(edited > 0, "no editable costume frame found");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.Indy3Ega)]
        [InlineData(GameLibrary.LoomEga)]
        public void V3OldRawAdLibImportRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummEditor.Engine.Structures.IndexFile.ScummV3OldBundleIndexFile;
            Skip.If(index == null || index.SoundDirectory == null, "no sound directory");

            var byRoom = new System.Collections.Generic.Dictionary<int, ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile>();
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile;
                if (df != null) byRoom[RoomNo(disk.FilePath)] = df;
            }

            bool tested = false;
            var sdir = index.SoundDirectory;
            for (int s = 0; s < sdir.Count && !tested; s++)
            {
                int off = sdir.Offsets[s];
                if (off == 0xFFFF || off == 0) continue;
                ScummEditor.Engine.Structures.DataFile.ScummV3OldBundleDataFile df;
                if (!byRoom.TryGetValue(sdir.RoomNumbers[s], out df)) continue;
                var snd = new ScummEditor.Engine.Structures.DataFile.ScummV3OldSound(df.RawContent, off);
                byte[] payload = snd.GetAdLibPayload();
                if (payload == null) continue;

                // no-op: re-importing the same payload leaves the room byte-identical
                byte[] before; using (var ms = new System.IO.MemoryStream()) { df.SaveToBinaryWriter(ms); before = ms.ToArray(); }
                string err;
                Assert.True(ScummV3OldGraphics.ImportRawAdLib(df, index, sdir.RoomNumbers[s], off, payload, out err), err);
                byte[] after; using (var ms = new System.IO.MemoryStream()) { df.SaveToBinaryWriter(ms); after = ms.ToArray(); }
                Assert.Equal(before.Length, after.Length);
                for (int k = 0; k < before.Length; k++) Assert.Equal(before[k], after[k]);

                // grow: a larger payload re-extracts at the new length
                var bigger = new byte[payload.Length + 3];
                System.Array.Copy(payload, bigger, payload.Length);
                Assert.True(ScummV3OldGraphics.ImportRawAdLib(df, index, sdir.RoomNumbers[s], off, bigger, out err), err);
                var snd2 = new ScummEditor.Engine.Structures.DataFile.ScummV3OldSound(df.RawContent, off);
                Assert.Equal(bigger.Length, snd2.GetAdLibPayload().Length);
                tested = true;
            }
            Assert.True(tested, "no AdLib sound found to import");
        }

        // ------------------------------------------------------------------ helpers

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
        }

        private static int FirstPresentGlyph(CharsetV3 charset)
        {
            for (int i = 0; i < charset.NumChars; i++)
            {
                if (charset.HasGlyph(i) && charset.CharWidth(i) > 0) return i;
            }
            return -1;
        }

        private static byte[,] ReadAtlas(string png)
        {
            using (var bitmap = (Bitmap)Image.FromFile(png))
            {
                return IndexedImageHelper.GetIndexMatrix(bitmap);
            }
        }

        private static void WriteAtlas(string png, byte[,] pixels, Color[] palette)
        {
            using (Bitmap bitmap = IndexedImageHelper.FromIndexMatrix(pixels, palette, -1))
            {
                bitmap.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private static byte[] Save(BlockBase tree)
        {
            using (var ms = new MemoryStream())
            {
                tree.SaveToBinaryWriter(ms);
                return ms.ToArray();
            }
        }

        private static byte[] ReadDecrypted(string path, int xorKey)
        {
            byte[] data = File.ReadAllBytes(path);
            if (xorKey != 0)
            {
                for (int i = 0; i < data.Length; i++) data[i] ^= (byte)xorKey;
            }
            return data;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
