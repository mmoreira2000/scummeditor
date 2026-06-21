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
            foreach (DataDisk disk in game.DataDisks)
            {
                int n;
                if (int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out n) && n == roomNo)
                {
                    var df = disk.Tree as ScummV3OldBundleDataFile;
                    return df == null ? null : df.RawContent;
                }
            }
            return null;
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
    }
}
