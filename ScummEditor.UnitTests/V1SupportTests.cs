using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v1 "classic" support (Maniac Mansion / Zak McKracken DOS floppy, index magic 0x0A31). M0
    /// foundation: detection (the same XOR-0xFF GF_OLD_BUNDLE container as v2/v3old, but a count-less
    /// index with hardcoded per-game resource counts and a 1-byte global-object table), byte-identical
    /// container + index round-trip, and the hardcoded directory counts the classic parse depends on.
    /// Real-data tests skip when the GameData library is absent.
    /// </summary>
    public class V1SupportTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1, ScummGame.ManiacMansion)]
        [InlineData(GameLibrary.ZakV1, ScummGame.ZakMcKracken)]
        public void DetectsV1Game(string relativePath, ScummGame expectedGame)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.Equal(1, info.ScummVersion);
            Assert.True(info.UsesOldBundle);       // GF_OLD_BUNDLE container, like v2/v3old
            Assert.True(info.UsesClassicIndex);    // count-less 0x0A31 index
            Assert.False(info.UsesSmallHeader);
            Assert.Equal(0xFF, info.XorKey);       // whole file XOR 0xFF
            Assert.Equal(1, info.GlobalObjectEntrySize); // 1 byte/object (v3old = 4)
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1ContainerRoundTripsByteIdentical(string relativePath)
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
        /// The v1 index stores NO counts (hardcoded per game). Asserting the exact directory counts proves
        /// the count-less classic parse walks the file correctly. The index size equals
        /// 2 + numObjects + sum(count*3): Maniac = 2+800+165+105+600+300 = 1972; Zak = 2+775+183+111+465+360 = 1896.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1, 55, 35, 200, 100)]
        [InlineData(GameLibrary.ZakV1, 61, 37, 155, 120)]
        public void V1ClassicIndexParsesHardcodedCounts(string relativePath, int rooms, int costumes, int scripts, int sounds)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            Assert.NotNull(index.RoomDirectory);
            Assert.Equal(rooms, index.RoomDirectory.Count);
            Assert.Equal(costumes, index.CostumeDirectory.Count);
            Assert.Equal(scripts, index.ScriptDirectory.Count);
            Assert.Equal(sounds, index.SoundDirectory.Count);
        }

        /// <summary>The new v1 (0x0A31) branch must not change v2/v3 old-bundle detection.</summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2, 2, ScummGame.ManiacMansion)]
        [InlineData(GameLibrary.LoomEga, 3, ScummGame.Loom)]
        [InlineData(GameLibrary.Indy3Ega, 3, ScummGame.IndianaJones3)]
        public void OlderBundleGamesStillDetectCorrectly(string relativePath, int expectedVersion, ScummGame expectedGame)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            GameInfo info = GameLibrary.Detect(relativePath);

            Assert.NotNull(info);
            Assert.Equal(expectedVersion, info.ScummVersion);
            Assert.Equal(expectedGame, info.LoadedGame);
            Assert.False(info.UsesClassicIndex);
        }

        // --- text (M1 disassembler v1 mode + M2 translation) -----------------------

        /// <summary>
        /// v1 text extraction reuses the v2 pipeline (object/verb/script layout is byte-identical) with the
        /// disassembler in v1 mode (actorOps Color takes no extra byte). Clean object names + much dialogue
        /// prove the v1 disassembler stays in sync; a desync (wrong Color byte count) would garble strings.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1, "key")]
        [InlineData(GameLibrary.ZakV1, "door")]
        public void V1TextExtractsCleanStrings(string relativePath, string expectedName)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            var entries = ScummV2TextManager.Extract(game, GameTextCodecV12.Default());

            Assert.True(entries.Count > 400, "expected many text entries, got " + entries.Count);
            Assert.True(entries.Count(e => e.Kind == "objectName") > 80,
                "expected many object names, got " + entries.Count(e => e.Kind == "objectName"));
            Assert.True(entries.Count(e => e.Kind == "print" || e.Kind == "printEgo") > 150, "expected much dialogue");
            Assert.Contains(entries, e => e.Kind == "objectName" && e.Text == expectedName);
        }

        /// <summary>
        /// A v1 translation imports byte-safe: only the edited resources (and their shared-region siblings)
        /// change, the entry count is preserved, and 90%+ of the edits land. Mirrors the v2 guarantee.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1TextImportIsByteSafe(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var codec = GameTextCodecV12.Default();

            var baseEntries = ScummV2TextManager.Extract(game, codec);
            var baseline = new Dictionary<string, string>();
            foreach (GameTextEntry e in baseEntries) baseline[e.Id] = e.Text;

            // Edit distinct, pure-ASCII, token-free names + dialogue (grow + shrink).
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

            ScummV2TextManager.Import(game, edits, codec);

            var after = new Dictionary<string, string>();
            var afterEntries = ScummV2TextManager.Extract(game, codec);
            foreach (GameTextEntry e in afterEntries) after[e.Id] = e.Text;

            foreach (var kv in baseline)
            {
                if (edits.ContainsKey(kv.Key)) continue;
                string got; after.TryGetValue(kv.Key, out got);
                if (got == kv.Value) continue;
                if (editedBaseTexts.Contains(kv.Value)) continue;
                Assert.Fail("text edit corrupted " + kv.Key + ": was '" + kv.Value + "' now '" + (got ?? "<gone>") + "'");
            }
            Assert.Equal(baseEntries.Count, afterEntries.Count);

            int applied = 0;
            foreach (var kv in edits) if (after.TryGetValue(kv.Key, out var g) && g == kv.Value) applied++;
            Assert.True(applied >= edits.Count * 0.9, "too few edits applied: " + applied + "/" + edits.Count);
        }

        // --- images (M3 GdiV1 tilemap decode) --------------------------------------

        /// <summary>
        /// The v1 GdiV1 tilemap codec (charMap/picMap/colorMap + object 3-plane + walk-behind mask) decodes
        /// every real room background and many object images to their declared dimensions without throwing
        /// or desyncing - proving the from-scratch decodeV1Gfx + reconstruction stays in step with the data.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1BackgroundsAndObjectsDecode(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);

            int rooms = 0, objects = 0, masks = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                var room = new ScummV1Room(df.RawContent);
                if (room.WidthInChars <= 0 || room.HeightInChars <= 0) continue;

                using (Bitmap bg = decoder.DecodeBackground(room))
                {
                    if (bg != null)
                    {
                        Assert.Equal(room.Width, bg.Width);
                        Assert.Equal(room.Height, bg.Height);
                        rooms++;
                    }
                }
                using (Bitmap mask = decoder.DecodeBackgroundZPlane(room)) { if (mask != null) masks++; }
                for (int i = 0; i < room.NumObjects; i++)
                    using (Bitmap o = decoder.DecodeObject(room, i)) { if (o != null) objects++; }
            }

            Assert.True(rooms > 20, "expected many decodable rooms, got " + rooms);
            Assert.True(masks > 20, "expected many decodable walk-behind masks, got " + masks);
            Assert.True(objects > 50, "expected many decodable object images, got " + objects);
        }

        /// <summary>
        /// Re-encoding an UNEDITED v1 background reproduces it pixel-for-pixel: decode -> ScummV1ImageEncoder
        /// (re-quantize into charMap/picMap/colorMap, rebuild the room) -> decode again yields the identical
        /// index matrix. This proves the GdiV1 tile re-quantization is lossless within the format's limits
        /// (an original background satisfies the &lt;=256-tile / 4-colour-per-cell constraints by construction).
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1BackgroundReencodeIsLossless(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);
            var encoder = new ScummV1ImageEncoder(isManiac);

            int tested = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                var room = new ScummV1Room(df.RawContent);
                if (room.WidthInChars <= 0 || room.HeightInChars <= 0) continue;
                byte[,] m1 = decoder.BackgroundMatrix(room);
                if (m1 == null) continue;

                string err;
                byte[] rebuilt = encoder.EncodeBackground(room, m1, out err);
                Assert.True(rebuilt != null, "room re-encode failed: " + err);

                byte[,] m2 = decoder.BackgroundMatrix(new ScummV1Room(rebuilt));
                Assert.NotNull(m2);
                Assert.True(MatricesEqual(m1, m2), "v1 background re-encode was not pixel-lossless");
                tested++;
            }
            Assert.True(tested > 20, "expected many v1 rooms re-encoded losslessly, got " + tested);
        }

        /// <summary>
        /// Full v1 background WRITE-BACK round-trip: decode a room background, re-import it through
        /// OldBundleImageImporter (re-encode -> rebuild the room resource -> splice with ApplyEdit, which
        /// resizes the room-0 resource and relocates the costume/script/sound offsets packed after it in the
        /// NN.LFL), then re-decode from offset 0 - the background must be pixel-for-pixel identical.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1BackgroundImportRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);

            int tested = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                if (tested >= 5) break;
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                var room = new ScummV1Room(df.RawContent);
                if (room.WidthInChars <= 0 || room.HeightInChars <= 0) continue;
                Bitmap before = decoder.DecodeBackground(room);
                if (before == null) continue;

                string err;
                bool ok = OldBundleImageImporter.Import(df, index, roomNo, true, OldBundleImageKind.Background, 0, before, out err);
                Assert.True(ok, "v1 background import failed (room " + roomNo + "): " + err);

                using (Bitmap after = decoder.DecodeBackground(new ScummV1Room(df.RawContent)))
                {
                    Assert.NotNull(after);
                    Assert.True(BitmapsEqual(before, after), "v1 background not pixel-identical after import round-trip (room " + roomNo + ")");
                }
                before.Dispose();
                tested++;
            }
            Assert.True(tested > 0, "no v1 background could be round-tripped");
        }

        /// <summary>
        /// v1 OBJECT-image import is EXPORT-ONLY for now: the importer refuses it with a clear message (its
        /// compact write-back is pending - the OBIM lives after the contiguous map block) and must NOT mutate
        /// the data file. Decode/export still works. (Background + mask import are the compact, validated paths.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1ObjectImageImportIsExportOnly(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);

            int checkedCount = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                var room = new ScummV1Room(df.RawContent);

                for (int i = 0; i < room.NumObjects; i++)
                {
                    Bitmap before = decoder.DecodeObject(room, i);
                    if (before == null) continue;

                    byte[] snapshot = (byte[])df.RawContent.Clone();
                    string err;
                    bool ok = OldBundleImageImporter.Import(df, index, roomNo, true, OldBundleImageKind.Object, i, before, out err);
                    before.Dispose();

                    Assert.False(ok, "v1 object-image import is meant to be export-only for now (room " + roomNo + " obj " + i + ")");
                    Assert.Contains("export-only", err);
                    Assert.Equal(snapshot, df.RawContent); // a refused import must not mutate the room
                    checkedCount++;
                    break;
                }
            }
            Assert.True(checkedCount > 20, "expected many decodable v1 object images to check, got " + checkedCount);
        }

        /// <summary>
        /// v1 background walk-behind (z-plane) mask import round-trips pixel-for-pixel (the compact map-block
        /// rewrite re-encodes maskMap/maskChar in place). The per-object mask import is export-only for now.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1ZPlaneImportRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);

            int bgMasks = 0, objMasks = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                var room = new ScummV1Room(df.RawContent);

                Bitmap bgBefore = decoder.DecodeBackgroundZPlane(room);
                if (bgBefore != null)
                {
                    string err;
                    bool ok = OldBundleImageImporter.Import(df, index, roomNo, true, OldBundleImageKind.BackgroundZPlane, 0, bgBefore, out err);
                    Assert.True(ok, "v1 background z-plane import failed (room " + roomNo + "): " + err);
                    using (Bitmap after = decoder.DecodeBackgroundZPlane(new ScummV1Room(df.RawContent)))
                    {
                        Assert.NotNull(after);
                        Assert.True(BitmapsEqual(bgBefore, after), "v1 background mask not pixel-identical after import (room " + roomNo + ")");
                    }
                    bgBefore.Dispose();
                    bgMasks++;
                }

                var room2 = new ScummV1Room(df.RawContent);
                for (int i = 0; i < room2.NumObjects; i++) // object z-plane import is export-only for now
                {
                    Bitmap before = decoder.DecodeObjectZPlane(room2, i);
                    if (before == null) continue;

                    byte[] snapshot = (byte[])df.RawContent.Clone();
                    string err;
                    bool ok = OldBundleImageImporter.Import(df, index, roomNo, true, OldBundleImageKind.ObjectZPlane, i, before, out err);
                    before.Dispose();
                    Assert.False(ok, "v1 object z-plane import is meant to be export-only for now (room " + roomNo + " obj " + i + ")");
                    Assert.Contains("export-only", err);
                    Assert.Equal(snapshot, df.RawContent); // a refused import must not mutate the room
                    objMasks++;
                    break;
                }
            }
            Assert.True(bgMasks > 20, "expected many v1 background masks round-tripped, got " + bgMasks);
            Assert.True(objMasks > 0, "expected at least one v1 object mask to check, got " + objMasks);
        }

        /// <summary>
        /// REGRESSION (the shared-charMap bug): importing a room BACKGROUND must leave EVERY object image in
        /// that room pixel-identical. v1 backgrounds and object images share one 256-tile charMap; an earlier
        /// encoder renumbered it from scratch, silently corrupting every object in any edited room.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1BackgroundImportPreservesObjectImages(string relativePath)
        {
            AssertImportPreservesOtherImages(relativePath, OldBundleImageKind.Background, -1);
        }

        /// <summary>
        /// Importing a walk-behind MASK must leave every colour image (background + object images) pixel-identical
        /// - the maskChar grows by appending, never overwriting tiles other images reference.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1MaskImportPreservesColourImages(string relativePath)
        {
            AssertImportPreservesOtherImages(relativePath, OldBundleImageKind.BackgroundZPlane, -1);
        }

        /// <summary>
        /// Imports one image of <paramref name="editedKind"/> (no-op: its own decoded pixels) into the first
        /// room that has both a background and object images, then asserts the background and ALL object images
        /// the edit did not target are still pixel-identical (shared charMap / maskChar untouched).
        /// </summary>
        private static void AssertImportPreservesOtherImages(string relativePath, OldBundleImageKind editedKind, int editedObject)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);

            int roomsChecked = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                var room = new ScummV1Room(df.RawContent);

                // The edit target must exist; if we edit an object, edit the first decodable one.
                int targetObject = editedObject;
                if (editedKind == OldBundleImageKind.Object)
                {
                    targetObject = -1;
                    for (int i = 0; i < room.NumObjects; i++)
                        using (Bitmap b = decoder.DecodeObject(room, i)) { if (b != null) { targetObject = i; break; } }
                    if (targetObject < 0) continue;
                }

                // Snapshot the background and every object image we are NOT editing.
                Bitmap bg = decoder.DecodeBackground(room);
                var objBefore = new Dictionary<int, Bitmap>();
                for (int i = 0; i < room.NumObjects; i++)
                {
                    if (editedKind == OldBundleImageKind.Object && i == targetObject) continue;
                    Bitmap o = decoder.DecodeObject(room, i);
                    if (o != null) objBefore[i] = o;
                }
                if (bg == null || objBefore.Count == 0) { bg?.Dispose(); foreach (var o in objBefore.Values) o.Dispose(); continue; }

                Bitmap source =
                    editedKind == OldBundleImageKind.Background ? decoder.DecodeBackground(room) :
                    editedKind == OldBundleImageKind.BackgroundZPlane ? decoder.DecodeBackgroundZPlane(room) :
                    decoder.DecodeObject(room, targetObject);
                if (source == null) { bg.Dispose(); foreach (var o in objBefore.Values) o.Dispose(); continue; }

                string err;
                bool ok = OldBundleImageImporter.Import(df, index, roomNo, true, editedKind, targetObject, source, out err);
                Assert.True(ok, "v1 import failed (room " + roomNo + ", " + editedKind + "): " + err);
                source.Dispose();

                var room2 = new ScummV1Room(df.RawContent);
                if (editedKind != OldBundleImageKind.Background)
                    using (Bitmap bgAfter = decoder.DecodeBackground(room2))
                        Assert.True(BitmapsEqual(bg, bgAfter), "background changed by a " + editedKind + " import (room " + roomNo + ")");
                foreach (var kv in objBefore)
                    using (Bitmap oAfter = decoder.DecodeObject(room2, kv.Key))
                    {
                        Assert.NotNull(oAfter);
                        Assert.True(BitmapsEqual(kv.Value, oAfter), "object " + kv.Key + " changed by a " + editedKind + " import (room " + roomNo + ")");
                    }

                bg.Dispose();
                foreach (var o in objBefore.Values) o.Dispose();
                roomsChecked++;
                if (roomsChecked >= 6) break;
            }
            Assert.True(roomsChecked > 0, "no v1 room with a background and object images was found to check preservation");
        }

        /// <summary>
        /// A REAL v1 background edit persists: painting an 8x8 cell with a representable room colour, re-encoding
        /// and re-decoding shows exactly that change (and nothing else). Proves new-tile allocation into a free
        /// charMap slot actually writes through, not just the no-op identity path.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1BackgroundEditPersists(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);
            var encoder = new ScummV1ImageEncoder(isManiac);

            int tested = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                if (tested >= 3) break;
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                var room = new ScummV1Room(df.RawContent);
                if (room.WidthInChars <= 0 || room.HeightInChars <= 0) continue;
                byte[,] m = decoder.BackgroundMatrix(room);
                if (m == null) continue;

                // Paint the top-left 8x8 cell with the colour already at (0,0): always representable, and it
                // collapses that cell to a single uniform tile.
                byte paint = m[0, 0];
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        m[x, y] = paint;

                string err;
                byte[] rebuilt = encoder.EncodeBackground(room, m, out err);
                Assert.True(rebuilt != null, "edit re-encode failed: " + err);

                byte[,] back = decoder.BackgroundMatrix(new ScummV1Room(rebuilt));
                Assert.NotNull(back);
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        Assert.True(back[x, y] == paint, "v1 background edit did not persist at (" + x + "," + y + ")");
                tested++;
            }
            Assert.True(tested > 0, "no v1 room available for an edit-persist check");
        }

        private static bool MatricesEqual(byte[,] a, byte[,] b)
        {
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int x = 0; x < a.GetLength(0); x++)
                for (int y = 0; y < a.GetLength(1); y++)
                    if (a[x, y] != b[x, y]) return false;
            return true;
        }

        // --- costumes (M4 format 0x57 decode) --------------------------------------

        /// <summary>
        /// v1 costumes are format 0x57 (C64-style 2-bit RLE, 6-byte CELs, frame table at base+8 with
        /// limbBase-relative offsets) - a different container and codec from the v2/v3-old 0x58. Many
        /// costumes and frames decode to their declared dimensions, proving the 0x57 parse + C64 RLE.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1CostumesDecode(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            Assert.NotNull(index.CostumeDirectory);

            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            byte[] pal = CostumeImageDecoderV1.DefaultPalette(isManiac);
            var decoder = new CostumeImageDecoderV1();

            int costumes = 0, frames = 0;
            V3OldResourceDirectory dir = index.CostumeDirectory;
            for (int c = 0; c < dir.Count; c++)
            {
                int off = dir.Offsets[c];
                if (off == 0xFFFF || off == 0) continue;
                byte[] roomData = RoomData(game, dir.RoomNumbers[c]);
                if (roomData == null || off >= roomData.Length) continue;

                var cost = new CostumeV3Old(roomData, off);
                if (cost.Format != 0x57 || cost.Frames.Count == 0) continue;
                costumes++;
                foreach (CostumeImageData f in cost.Frames)
                    using (Bitmap b = decoder.Decode(f, pal))
                    {
                        if (b == null) continue;
                        Assert.Equal(f.Width, b.Width);
                        Assert.Equal(f.Height, b.Height);
                        frames++;
                    }
            }

            Assert.True(costumes > 15, "expected many 0x57 costumes, got " + costumes);
            Assert.True(frames > 50, "expected many decodable costume frames, got " + frames);
        }

        // --- costume re-encode codec (M6) ------------------------------------------

        /// <summary>
        /// CostumeImageEncoderV1 is the exact inverse of the decoder: re-encoding a decoded frame and decoding
        /// it again reproduces the pixels exactly (lossless C64 2-bit RLE). NOTE: full in-place costume
        /// WRITE-BACK is deferred - v1's heuristic frame enumeration is not invariant to re-packing, so a
        /// faithful resource rebuild needs the deterministic animCmds-driven CEL walk (a later effort).
        /// Decode/export and this re-encode codec work today.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1CostumeReencodeIsLossless(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            byte[] pal = CostumeImageDecoderV1.DefaultPalette(isManiac);
            var decoder = new CostumeImageDecoderV1();
            var encoder = new CostumeImageEncoderV1();

            int tested = 0;
            V3OldResourceDirectory dir = index.CostumeDirectory;
            for (int c = 0; c < dir.Count && tested < 200; c++)
            {
                int off = dir.Offsets[c];
                if (off == 0xFFFF || off == 0) continue;
                byte[] roomData = RoomData(game, dir.RoomNumbers[c]);
                if (roomData == null || off >= roomData.Length) continue;
                var cost = new CostumeV3Old(roomData, off);
                if (cost.Format != 0x57 || cost.Frames.Count == 0) continue;

                foreach (CostumeImageData f in cost.Frames)
                {
                    using (Bitmap b1 = decoder.Decode(f, pal))
                    {
                        if (b1 == null) continue;
                        byte[] reRle = encoder.Encode(b1, f.Width, f.Height);
                        var f2 = new CostumeImageData { Width = f.Width, Height = f.Height, ImageData = reRle };
                        using (Bitmap b2 = decoder.Decode(f2, pal))
                        {
                            Assert.NotNull(b2);
                            Assert.True(BitmapsEqual(b1, b2), "v1 costume re-encode was not lossless");
                            tested++;
                        }
                    }
                }
            }
            Assert.True(tested > 30, "expected many frames re-encoded, got " + tested);
        }

        /// <summary>
        /// Full v1 costume frame WRITE-BACK round-trip: decode a frame, re-import it (re-encode -> rebuild the
        /// 0x57 resource -> splice with index relocation via ApplyEdit), then re-parse from the costume offset
        /// and re-decode - the frame count must be unchanged (the deterministic CEL enumeration is invariant to
        /// the re-pack) and the edited frame pixel-for-pixel identical. Also exercises classic-index relocation.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1CostumeImportRoundTrips(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            byte[] pal = CostumeImageDecoderV1.DefaultPalette(isManiac);
            var decoder = new CostumeImageDecoderV1();

            int tested = 0;
            V3OldResourceDirectory dir = index.CostumeDirectory;
            for (int c = 0; c < dir.Count && tested < 6; c++)
            {
                int off = dir.Offsets[c];
                if (off == 0xFFFF || off == 0) continue;
                DataDisk disk = FindDisk(game, dir.RoomNumbers[c]);
                var df = disk == null ? null : disk.Tree as ScummV3OldBundleDataFile;
                if (df == null || off >= df.RawContent.Length) continue;

                var cost = new CostumeV3Old(df.RawContent, off);
                if (cost.Format != 0x57 || cost.Frames.Count == 0) continue;

                int frameCount = cost.Frames.Count;
                int fi = frameCount / 2;
                Bitmap before = decoder.Decode(cost.Frames[fi], pal);
                if (before == null) continue;

                string err;
                bool ok = OldBundleCostumeImporter.ImportFrame(df, index, dir.RoomNumbers[c], true, off, fi, before, out err);
                Assert.True(ok, "v1 costume import failed: " + err);

                // The edited costume keeps its own offset (only later resources shift); re-parse it.
                var cost2 = new CostumeV3Old(df.RawContent, off);
                Assert.Equal(frameCount, cost2.Frames.Count); // deterministic enumeration stable across the re-pack
                using (Bitmap after = decoder.Decode(cost2.Frames[fi], pal))
                {
                    Assert.NotNull(after);
                    Assert.True(BitmapsEqual(before, after), "costume frame not pixel-identical after import round-trip");
                }
                before.Dispose();
                tested++;
            }
            Assert.True(tested > 0, "no v1 costume frame could be round-tripped");
        }

        // --- GUI room model (M8) ---------------------------------------------------

        /// <summary>
        /// The GUI block tree's data source (OldBundleNavigator.BuildRoomModel) builds a correct v1 room
        /// model: it is flagged v1, has real dimensions and a decodable background, lists named objects, and
        /// its scripts disassemble to the end with the v1 disassembler - so v1 renders in the editor like v2/v3.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1RoomModelBuildsForTheGuiTree(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);

            int roomsWithBackground = 0, objectsWithName = 0, scriptsTried = 0, scriptsToEnd = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;

                OldBundleRoomModel model = OldBundleNavigator.BuildRoomModel(game, df, roomNo);
                Assert.True(model.IsV1);
                Assert.True(model.IsV2); // v1 is also <= 2

                if (model.HasBackground && model.Width > 0 && model.Height > 0) roomsWithBackground++;
                foreach (OldBundleObjectInfo o in model.Objects)
                    if (!string.IsNullOrEmpty(o.Name)) objectsWithName++;
                foreach (OldBundleCodeRange s in model.Scripts)
                {
                    if (s.End <= s.Start) continue;
                    var r = OldBundleNavigator.DisassembleRange(df.RawContent, s.Start, s.End, model.IsV2, model.IsIndy3, model.IsV1);
                    scriptsTried++;
                    if (r != null && r.DecodedToEnd) scriptsToEnd++;
                }
            }

            Assert.True(roomsWithBackground > 20, "expected many v1 rooms with a decodable background, got " + roomsWithBackground);
            Assert.True(objectsWithName > 50, "expected many named v1 objects, got " + objectsWithName);
            Assert.True(scriptsTried > 0 && scriptsToEnd >= scriptsTried * 0.85,
                "too many v1 scripts failed to decode to end: " + scriptsToEnd + "/" + scriptsTried);
        }

        // --- sound (M7) ------------------------------------------------------------

        /// <summary>
        /// v1 sounds are PC-speaker data (Player_V2 WA chunks) - there is NO AdLib/MIDI to decode, so (like
        /// v2) the editor offers raw export only, no playback. This confirms the sound resources the index
        /// lists resolve to real positions inside their room files, so they can be located and exported raw.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1SoundResourcesAreLocatable(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            Assert.NotNull(index);
            Assert.NotNull(index.SoundDirectory);

            int located = 0;
            V3OldResourceDirectory dir = index.SoundDirectory;
            for (int s = 0; s < dir.Count; s++)
            {
                int off = dir.Offsets[s];
                if (off == 0xFFFF || off == 0) continue;
                byte[] roomData = RoomData(game, dir.RoomNumbers[s]);
                if (roomData != null && off > 0 && off < roomData.Length) located++;
            }
            Assert.True(located > 20, "expected many locatable v1 sound resources, got " + located);
        }

        // --- EXE-embedded font (M5) ------------------------------------------------

        /// <summary>
        /// The v1 font lives inside MANIAC.EXE / ZAK.EXE in the same format as v2 (8x8 glyphs, located by the
        /// box-glyph signature, RLE-decoded, edited in place). The existing ScummV2ExeFont codec handles it
        /// unchanged - v1 is simply the all-literal case (CompressedLength 1016 vs v2's 1005), so every glyph
        /// is editable. The font export/import menu already routes ScummVersion &lt;= 2 here. (ScummVM ignores
        /// this font; an edit is verified natively in DOSBox.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1ExeFontDecodesAndEditsInPlace(string relativePath)
        {
            string folder = GameLibrary.Folder(relativePath);
            Skip.If(folder == null, "GameData folder not present: " + relativePath);
            string exePath = ScummV2ExeFontCodec.FindGameExe(folder);
            Skip.If(exePath == null, "no game EXE in: " + relativePath);

            byte[] exe = File.ReadAllBytes(exePath);
            string error;
            ScummV2ExeFont font = ScummV2ExeFont.Read(exe, out error);

            Assert.NotNull(font);
            Assert.True(font.StreamStart > 0);
            Assert.Equal(1016, font.CompressedLength); // v1 = all-literal (v2 Enhanced = 1005 with 4 RLE runs)
            Assert.Equal(ScummV2ExeFont.GlyphCount * 8, font.GlyphBytes.Length);

            byte[] sig = { 0x01, 0x03, 0x06, 0x0C, 0x18, 0x3E, 0x03, 0x00, 0x80, 0xC0, 0x60, 0x30, 0x18, 0x7C, 0xC0, 0x00 };
            for (int i = 0; i < sig.Length; i++) Assert.Equal(sig[i], font.GlyphBytes[8 + i]); // glyphs 1+2

            // All-literal: editing any non-signature glyph is accepted as a same-size in-place splice.
            byte[] edited = (byte[])font.GlyphBytes.Clone();
            edited[0x41 * 8] ^= 0xFF; // 'A'
            string applyError;
            bool ok = font.TryApplyEditedGlyphs(edited, out applyError);
            Assert.True(ok, "a v1 glyph edit was refused: " + applyError);
            Assert.Equal(exe.Length, font.ExeBytes.Length);
            Assert.False(BytesEqual(exe, font.ExeBytes)); // the edit landed
        }

        private static byte[] RoomData(ScummGameData game, int roomNo)
        {
            DataDisk disk = FindDisk(game, roomNo);
            var df = disk == null ? null : disk.Tree as ScummV3OldBundleDataFile;
            return df == null ? null : df.RawContent;
        }

        private static DataDisk FindDisk(ScummGameData game, int roomNo)
        {
            foreach (DataDisk disk in game.DataDisks)
            {
                int n;
                if (int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out n) && n == roomNo) return disk;
            }
            return null;
        }

        private static bool BitmapsEqual(Bitmap a, Bitmap b)
        {
            if (a.Width != b.Width || a.Height != b.Height) return false;
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) return false;
            return true;
        }

        private static ScummGameData SkipOrLoad(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);
            ScummGameData game = GameLibrary.Load(relativePath);
            Skip.If(game == null, "could not load: " + relativePath);
            return game;
        }

        private static byte[] Save(BlockBase tree)
        {
            using (var ms = new MemoryStream()) { tree.SaveToBinaryWriter(ms); return ms.ToArray(); }
        }

        private static byte[] ReadDecrypted(string path, int xorKey)
        {
            byte[] data = File.ReadAllBytes(path);
            if (xorKey != 0) for (int i = 0; i < data.Length; i++) data[i] ^= (byte)xorKey;
            return data;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// REGRESSION (charMap tile-steal): editing ONE background cell must not disturb any OTHER cell. The
        /// edited cell allocates a fresh shared-charMap tile; that allocation must never overwrite a slot a
        /// still-unchanged cell references. (Found by adversarial review: the encoder allocated before locking
        /// the tiles unchanged cells reuse, so editing an early cell silently corrupted later ones.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1EditingOneCellDoesNotCorruptOthers(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);
            var encoder = new ScummV1ImageEncoder(isManiac);

            int tested = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                if (tested >= 5) break;
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                var room = new ScummV1Room(df.RawContent);
                if (room.WidthInChars < 2 || room.HeightInChars < 2) continue;
                byte[,] m1 = decoder.BackgroundMatrix(room);
                if (m1 == null) continue;

                int w = room.Width, h = room.Height;
                int r0 = RenderRemap(isManiac, room.Color(0));
                int r1 = RenderRemap(isManiac, room.Color(1));

                // Repaint ONLY the first 8x8 cell with a per-pair r0/r1 checkerboard (representable, almost
                // certainly a tile not already present -> forces a fresh charMap allocation).
                var edited = (byte[,])m1.Clone();
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 8; y++)
                        edited[x, y] = (byte)((((x / 2) + y) & 1) == 0 ? r0 : r1);

                string err;
                byte[] rebuilt = encoder.EncodeBackground(room, edited, out err);
                if (rebuilt == null) continue; // this room's charMap is already full - a legit format limit, not corruption

                byte[,] m2 = decoder.BackgroundMatrix(new ScummV1Room(rebuilt));
                Assert.NotNull(m2);
                for (int x = 0; x < w; x++)
                    for (int y = 0; y < h; y++)
                    {
                        if (x < 8 && y < 8) continue; // the deliberately edited cell
                        Assert.True(m2[x, y] == m1[x, y],
                            "editing one cell corrupted pixel (" + x + "," + y + ") elsewhere in the background");
                    }
                tested++;
            }
            Assert.True(tested > 0, "no v1 room available for a tile-steal check");
        }

        /// <summary>
        /// The v1 object importer rejects EXACTLY what the decoder rejects: an imageless object whose OBIM
        /// aliases some object's code (OBCD) block. The GUI already gates Import on a non-null decode, but a
        /// direct / batch caller must not be able to splice a garbage image over a code block. A rejected
        /// import must also leave the data file untouched.
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV1)]
        [InlineData(GameLibrary.ZakV1)]
        public void V1ObjectImportRejectsImagelessObjects(string relativePath)
        {
            ScummGameData game = SkipOrLoad(relativePath);
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            bool isManiac = game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);

            int checkedCount = 0;
            foreach (DataDisk disk in game.DataDisks)
            {
                if (checkedCount >= 10) break;
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df == null) continue;
                int roomNo;
                if (!int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out roomNo)) continue;
                var room = new ScummV1Room(df.RawContent);

                for (int i = 0; i < room.NumObjects && checkedCount < 10; i++)
                {
                    int obim = room.ObjectImageOffset(i);
                    int w = room.ObjectWidth(i), h = room.ObjectHeight(i);
                    if (obim <= 0 || w <= 0 || h <= 0) continue;
                    bool aliasesObcd = false;
                    for (int k = 0; k < room.NumObjects; k++)
                        if (room.ObjectCodeOffset(k) == obim) aliasesObcd = true;
                    if (!aliasesObcd) continue; // only the imageless (OBCD-aliasing) objects

                    using (var o = decoder.DecodeObject(room, i)) Assert.Null(o); // decoder rejects it

                    byte[] before = (byte[])df.RawContent.Clone();
                    using (var png = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format8bppIndexed))
                    {
                        string err;
                        bool ok = OldBundleImageImporter.Import(df, index, roomNo, true, OldBundleImageKind.Object, i, png, out err);
                        Assert.False(ok, "importer accepted an imageless (OBCD-aliasing) object (room " + roomNo + " obj " + i + ")");
                    }
                    Assert.True(BytesEqual(before, df.RawContent), "a rejected import must not mutate the data file");
                    checkedCount++;
                }
            }
            Assert.True(checkedCount > 0, "no imageless (OBCD-aliasing) object found to check");
        }

        /// <summary>The decoder's render-remap (EGA colour the room colour index renders to), for building representable test pixels.</summary>
        private static int RenderRemap(bool isManiac, int colorIndex)
        {
            byte[] map = isManiac
                ? new byte[] { 0x00, 0x0F, 0x04, 0x03, 0x05, 0x02, 0x01, 0x0E, 0x0C, 0x06, 0x0C, 0x08, 0x07, 0x0A, 0x09, 0x08 }
                : new byte[] { 0x00, 0x0F, 0x04, 0x03, 0x05, 0x02, 0x01, 0x0E, 0x0C, 0x06, 0x0D, 0x08, 0x07, 0x0A, 0x09, 0x07 };
            return map[colorIndex & 0x0F];
        }
    }
}
