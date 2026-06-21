using System.Collections.Generic;
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
    /// SCUMM v1/v2 support (Maniac Mansion, Zak McKracken classic). M0 foundation: detection (the v2
    /// games share the v3old magic 0x0100 but ship no charsets and use a 1-byte global-object table),
    /// byte-identical container + index round-trip, and the object-table stride that the index parse
    /// depends on. All real-data tests skip when the GameData library is absent.
    /// </summary>
    public class V2SupportTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2, ScummGame.ManiacMansion)]
        [InlineData(GameLibrary.ZakV2, ScummGame.ZakMcKracken)]
        public void DetectsV2Game(string relativePath, ScummGame expectedGame)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.Equal(2, info.ScummVersion);
            Assert.True(info.UsesOldBundle);     // GF_OLD_BUNDLE container, like v3old
            Assert.False(info.UsesSmallHeader);
            Assert.Equal(0xFF, info.XorKey);     // whole file XOR 0xFF
            Assert.Equal(1, info.GlobalObjectEntrySize); // v2 = 1 byte/object (v3old = 4)
        }

        /// <summary>
        /// v2 detection must NOT be confused with the v3old EGA games (same 0x0100 magic) - the charset
        /// count splits them - and v3old/v3small detection must be unchanged.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.LoomEga, 3, ScummGame.Loom)]
        [InlineData(GameLibrary.Indy3Ega, 3, ScummGame.IndianaJones3)]
        [InlineData(GameLibrary.ZakFmTowns, 3, ScummGame.ZakMcKracken)]
        public void V3GamesStillDetectAsV3(string relativePath, int expectedVersion, ScummGame expectedGame)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedVersion, info.ScummVersion);
            Assert.Equal(expectedGame, info.LoadedGame);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2ContainerRoundTripsByteIdentical(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            foreach (DataDisk disk in game.DataDisks)
            {
                byte[] original = ReadDecrypted(disk.FilePath, game.LoadedGameInfo.XorKey);
                byte[] resaved = Save(disk.Tree);
                Assert.True(BytesEqual(original, resaved),
                    Path.GetFileName(disk.FilePath) + " did not round-trip byte-identical");
            }

            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            byte[] idxOriginal = ReadDecrypted(game.LoadedGameInfo.IndexFile, game.LoadedGameInfo.IndexXorKey);
            byte[] idxResaved;
            using (var ms = new MemoryStream()) { index.SaveToBinaryWriter(ms); idxResaved = ms.ToArray(); }
            Assert.True(BytesEqual(idxOriginal, idxResaved), "00.LFL index did not round-trip byte-identical");
        }

        /// <summary>
        /// The v2 index uses a 1-byte global-object table; with the v3old 4-byte stride the four resource
        /// directories parse off-position (garbage counts or a null overlay). Asserting the exact counts
        /// proves the stride is handled. (Real counts: Maniac 61/40/179/120, Zak 61/40/155/120.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2, 179)]
        [InlineData(GameLibrary.ZakV2, 155)]
        public void V2IndexParsesDirectoriesWithOneByteObjectTable(string relativePath, int expectedScripts)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            Assert.NotNull(index.RoomDirectory);
            Assert.Equal(61, index.RoomDirectory.Count);
            Assert.Equal(40, index.CostumeDirectory.Count);
            Assert.Equal(expectedScripts, index.ScriptDirectory.Count);
            Assert.Equal(120, index.SoundDirectory.Count);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2, "key")]
        [InlineData(GameLibrary.ZakV2, "bed")]
        public void V2TextExtractsCleanStrings(string relativePath, string expectedName)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            var entries = ScummV2TextManager.Extract(game, GameTextCodecV12.Default());

            // A real v2 game has hundreds of translatable strings (object names + dialogue + verb code).
            Assert.True(entries.Count > 500, "expected many text entries, got " + entries.Count);
            Assert.True(entries.Count(e => e.Kind == "objectName") > 100, "expected many object names");
            Assert.True(entries.Count(e => e.Kind == "print" || e.Kind == "printEgo") > 200, "expected much dialogue");
            Assert.Contains(entries, e => e.Kind == "objectName" && e.Text == expectedName);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2TextImportIsByteSafe(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var codec = GameTextCodecV12.Default();

            var baseEntries = ScummV2TextManager.Extract(game, codec);
            var baseline = new Dictionary<string, string>();
            foreach (GameTextEntry e in baseEntries) baseline[e.Id] = e.Text;

            // Edit distinct, pure-ASCII, token-free names + dialogue (grow + shrink) so shared regions do
            // not muddy the comparison.
            var edits = new Dictionary<string, string>();
            var usedBase = new HashSet<string>();
            int names = 0, talk = 0;
            foreach (GameTextEntry e in baseEntries)
            {
                if (e.Text.Contains("{") || !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[A-Za-z0-9 .,'!?]+$") || e.Text.Length < 4) continue;
                if (!usedBase.Add(e.Text)) continue;
                if (e.Kind == "objectName" && names < 20) { edits[e.Id] = e.Text + " X"; names++; }
                else if ((e.Kind == "print" || e.Kind == "printEgo") && talk < 30) { edits[e.Id] = (talk % 2 == 0) ? e.Text + " [longer]" : "Short."; talk++; }
            }
            Skip.If(edits.Count == 0, "no editable strings found");

            var editedBaseTexts = new HashSet<string>();
            foreach (var kv in edits) editedBaseTexts.Add(baseline[kv.Key]);

            GameTextImportReport report = ScummV2TextManager.Import(game, edits, codec);

            var after = new Dictionary<string, string>();
            var afterEntries = ScummV2TextManager.Extract(game, codec);
            foreach (GameTextEntry e in afterEntries) after[e.Id] = e.Text;

            // No precisely-bounded resource may change except the edits and their shared-region siblings.
            foreach (var kv in baseline)
            {
                if (edits.ContainsKey(kv.Key)) continue;
                string got; after.TryGetValue(kv.Key, out got);
                if (got == kv.Value) continue;
                if (editedBaseTexts.Contains(kv.Value)) continue;
                Assert.True(false, "text edit corrupted " + kv.Key + ": was '" + kv.Value + "' now '" + (got ?? "<gone>") + "'");
            }
            Assert.Equal(baseEntries.Count, afterEntries.Count);

            int applied = 0;
            foreach (var kv in edits) if (after.TryGetValue(kv.Key, out var g) && g == kv.Value) applied++;
            Assert.True(applied >= edits.Count * 0.9, "too few edits applied: " + applied + "/" + edits.Count);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2BackgroundsDecodeToRoomDimensions(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var decoder = new ScummV2ImageDecoder();
            int rooms = 0, objects = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                var room = new ScummV2Room(df.RawContent);
                if (room.Width <= 0 || room.Height <= 0) continue;
                using (Bitmap bg = decoder.DecodeBackground(room))
                {
                    Assert.NotNull(bg);
                    Assert.Equal(room.Width, bg.Width);
                    Assert.Equal(room.Height, bg.Height);
                    rooms++;
                }
                for (int i = 0; i < room.NumObjects; i++)
                {
                    if (room.ObjectWidth(i) <= 0 || room.ObjectHeight(i) <= 0) continue;
                    using (Bitmap o = decoder.DecodeObject(room, i)) { if (o != null) objects++; }
                }
            }
            Assert.True(rooms > 20, "expected many decodable rooms, got " + rooms);
            Assert.True(objects > 100, "expected many decodable objects, got " + objects);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2ImageImportRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            int rooms = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                int roomNo;
                if (df == null || !int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                var room = new ScummV2Room(df.RawContent);
                if (room.Width <= 0 || room.Height <= 0 || room.ImageOffset <= 0) continue;
                byte[,] m = ScummV2ImageDecoder.DecodeRle(df.RawContent, room.ImageOffset, room.Width, room.Height);
                if (m == null) continue;

                byte newValue = (byte)((m[0, 0] + 1) & 0x0F);
                m[0, 0] = newValue;
                int imageEnd = room.NextStructuralOffsetAbove(room.ImageOffset);
                byte[] newImage = ScummV2ImageEncoder.EncodeImage(df.RawContent, room.ImageOffset, imageEnd, room.Width, room.Height, m);
                ScummV2Writer.ApplyEdit(df, index, roomNo, room.ImageOffset, imageEnd - room.ImageOffset, newImage, -1);

                var room2 = new ScummV2Room(df.RawContent);
                byte[,] m2 = ScummV2ImageDecoder.DecodeRle(df.RawContent, room2.ImageOffset, room2.Width, room2.Height);
                Assert.NotNull(m2);
                Assert.Equal(newValue, m2[0, 0]);
                rooms++;
                if (rooms >= 8) break; // a sample proves the encode + splice + relocation path
            }
            Assert.True(rooms > 0, "no editable backgrounds found");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2ZPlaneDecodesAndImportRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            int rooms = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                int roomNo;
                if (df == null || !int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                var room = new ScummV2Room(df.RawContent);
                if (room.Width <= 0 || room.Height <= 0 || room.ImageOffset <= 0) continue;
                int gfxLen = ScummV2ImageDecoder.GraphicsRleLength(df.RawContent, room.ImageOffset, room.Width, room.Height);
                int maskStart = room.ImageOffset + gfxLen;
                if (maskStart >= room.NextStructuralOffsetAbove(room.ImageOffset)) continue;
                byte[,] mask = ScummV2ImageDecoder.DecodeMaskRle(df.RawContent, maskStart, room.Width, room.Height);
                if (mask == null) continue;

                byte[,] bgBefore = ScummV2ImageDecoder.DecodeRle(df.RawContent, room.ImageOffset, room.Width, room.Height);
                byte newBit = (byte)(mask[0, 0] ^ 1);
                mask[0, 0] = newBit;
                int imageEnd = room.NextStructuralOffsetAbove(room.ImageOffset);
                byte[] newImage = ScummV2ImageEncoder.EncodeImageWithMask(df.RawContent, room.ImageOffset, room.Width, room.Height, mask);
                ScummV2Writer.ApplyEdit(df, index, roomNo, room.ImageOffset, imageEnd - room.ImageOffset, newImage, -1);

                var room2 = new ScummV2Room(df.RawContent);
                int gfxLen2 = ScummV2ImageDecoder.GraphicsRleLength(df.RawContent, room2.ImageOffset, room2.Width, room2.Height);
                byte[,] mask2 = ScummV2ImageDecoder.DecodeMaskRle(df.RawContent, room2.ImageOffset + gfxLen2, room2.Width, room2.Height);
                Assert.NotNull(mask2);
                Assert.Equal(newBit, mask2[0, 0]); // the mask edit persisted

                // the background pixels must be untouched by a mask edit
                byte[,] bgAfter = ScummV2ImageDecoder.DecodeRle(df.RawContent, room2.ImageOffset, room2.Width, room2.Height);
                Assert.NotNull(bgAfter);
                for (int x = 0; x < room.Width; x++)
                    for (int y = 0; y < room.Height; y++)
                        Assert.Equal(bgBefore[x, y], bgAfter[x, y]);

                rooms++;
                if (rooms >= 5) break;
            }
            Assert.True(rooms > 0, "no rooms with a decodable z-plane found");
        }

        /// <summary>
        /// v2 OBJECTS also carry a walk-behind (z-plane) mask, stored after the object graphics in the OBIM
        /// exactly as the background mask follows IM00 (ScummVM GdiV2::prepareDrawBitmap decodes the z-plane
        /// for objects too). This decodes an object mask, edits one bit, re-encodes via the importer's path and
        /// confirms the edit persists while the object GRAPHICS stay byte-for-byte untouched.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2ObjectZPlaneDecodesAndImportRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            var decoder = new ScummV2ImageDecoder();

            int objects = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                int roomNo;
                if (df == null || !int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                var room = new ScummV2Room(df.RawContent);

                for (int i = 0; i < room.NumObjects; i++)
                {
                    using (Bitmap z = decoder.DecodeObjectZPlane(room, i)) { if (z == null) continue; }

                    int w = room.ObjectWidth(i), h = room.ObjectHeight(i);
                    int obim = room.ObjectImageOffset(i);
                    int gfxLen = ScummV2ImageDecoder.GraphicsRleLength(df.RawContent, obim, w, h);
                    int objEnd = room.NextStructuralOffsetAbove(obim);
                    byte[,] gfxBefore = ScummV2ImageDecoder.DecodeRle(df.RawContent, obim, w, h);
                    byte[,] mask = ScummV2ImageDecoder.DecodeMaskRle(df.RawContent, obim + gfxLen, w, h);
                    Assert.NotNull(mask);

                    byte newBit = (byte)(mask[0, 0] ^ 1);
                    mask[0, 0] = newBit;
                    byte[] newRegion = ScummV2ImageEncoder.EncodeImageWithMask(df.RawContent, obim, w, h, mask);
                    ScummV2Writer.ApplyEdit(df, index, roomNo, obim, objEnd - obim, newRegion, -1);

                    var room2 = new ScummV2Room(df.RawContent);
                    int obim2 = room2.ObjectImageOffset(i);
                    int gfxLen2 = ScummV2ImageDecoder.GraphicsRleLength(df.RawContent, obim2, w, h);
                    byte[,] mask2 = ScummV2ImageDecoder.DecodeMaskRle(df.RawContent, obim2 + gfxLen2, w, h);
                    Assert.NotNull(mask2);
                    Assert.Equal(newBit, mask2[0, 0]); // the object mask edit persisted

                    byte[,] gfxAfter = ScummV2ImageDecoder.DecodeRle(df.RawContent, obim2, w, h);
                    Assert.NotNull(gfxAfter);
                    for (int x = 0; x < w; x++)
                        for (int y = 0; y < h; y++)
                            Assert.Equal(gfxBefore[x, y], gfxAfter[x, y]); // graphics untouched by a mask edit

                    objects++;
                    break; // one object per room keeps the resource from accumulating appended regions
                }
                if (objects >= 8) break;
            }
            Assert.True(objects > 0, "no v2 object with a decodable walk-behind mask was found");
        }

        /// <summary>
        /// A v2 text edit that grows past a 1-byte verb/name offset's range must be reported, not crash the
        /// whole import: ScummV2Writer.ApplyEdit is transactional (a rejected edit leaves the file untouched)
        /// and ScummV2TextManager catches it per-string. The rest of the import still applies and the game
        /// re-extracts cleanly. (Regression for the unhandled exception found while testing accents.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void V2GrowingTextEditsAreReportedNotThrown(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            string dir = Path.Combine(Path.GetTempPath(), "v2grow_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string txt = Path.Combine(dir, "texts.txt");
                ScummV2TextManager.ExportToFile(game, txt, "grow-test");

                // Force growth on every object name (append a long suffix) to exercise the 1-byte-offset
                // overflow path across many objects.
                string[] lines = File.ReadAllLines(txt);
                for (int i = 0; i < lines.Length; i++)
                {
                    int eq = lines[i].IndexOf(" = ");
                    if (eq <= 0 || lines[i][0] == ';') continue;
                    string id = lines[i].Substring(0, eq);
                    if (id.EndsWith(".name")) lines[i] = id + " = " + lines[i].Substring(eq + 3) + "XXXXXXXXXXXX";
                }
                File.WriteAllLines(txt, lines);

                // Must not throw, regardless of how many edits overflow.
                GameTextImportReport report = ScummV2TextManager.ImportFromFile(game, txt);
                Assert.NotNull(report);

                // The game data is still consistent: it re-extracts without throwing.
                List<GameTextEntry> after = ScummV2TextManager.Extract(game, GameTextCodecV12.Portuguese());
                Assert.True(after.Count > 0);
            }
            finally { Directory.Delete(dir, true); }
        }

        /// <summary>
        /// The Portuguese accent map must park accents only on slots that NEVER appear as a literal glyph in
        /// the shipped game text - otherwise exporting the untouched original would show false accents the
        /// translator might "fix", corrupting structural bytes (object-name padding, dialogue sentinels).
        /// This re-checks the default map against the real Maniac/Zak data (the adversarial-review blocker).
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void PortugueseAccentSlotsNeverAppearAsLiteralGlyphs(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            var slots = new HashSet<int>();
            foreach (string token in GameTextCodecV12.Portuguese().ToAccentSpec().Split(' '))
            {
                int x = token.IndexOf("0x", System.StringComparison.OrdinalIgnoreCase);
                if (x >= 0) slots.Add(System.Convert.ToInt32(token.Substring(x + 2), 16));
            }
            Assert.NotEmpty(slots);

            // Decode the original text with the PLAIN codec, strip {tokens}, and assert no accent slot byte
            // occurs as a literal glyph.
            foreach (GameTextEntry entry in ScummV2TextManager.Extract(game, GameTextCodecV12.Default()))
            {
                string t = entry.Text;
                for (int i = 0; i < t.Length; i++)
                {
                    if (t[i] == '{') { int c = t.IndexOf('}', i); if (c > i) { i = c; continue; } }
                    Assert.False(slots.Contains(t[i]),
                        "PT accent slot 0x" + ((int)t[i]).ToString("X2") + " ('" + t[i] + "') appears literally in " + entry.Id);
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
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
