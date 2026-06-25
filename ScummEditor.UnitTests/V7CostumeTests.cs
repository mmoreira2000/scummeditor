using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// SCUMM v7 (The Dig, Full Throttle) AKOS costumes: each costume's cels are decoded from its AKOS
    /// sub-blocks (AKHD/AKOF/AKCI/AKCD/AKPL/RGBS). The Dig and Full Throttle use cel codec 1 (BYLE RLE,
    /// the v5/v6 column-RLE scheme). This proves the AKOS cels decode to bitmaps without errors.
    /// </summary>
    public class V7CostumeTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void AkosCostumeCelsDecode(string path)
        {
            Skip.If(GameLibrary.Folder(path) == null, "not present: " + path);
            ScummGameData game = GameLibrary.Load(path);

            int akosCount = 0, totalCels = 0, decoded = 0;
            var celsByCodec = new Dictionary<int, int>();
            var decodedByCodec = new Dictionary<int, int>();
            var errors = new List<string>();

            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                foreach (BlockBase child in disk.Childrens)
                {
                    if (child.BlockType != "AKOS") continue;
                    akosCount++;
                    int codec = AkosImageDecoder.GetCodec(child);

                    int cels = AkosImageDecoder.GetCelCount(child);
                    for (int i = 0; i < cels; i++)
                    {
                        totalCels++;
                        celsByCodec[codec] = celsByCodec.GetValueOrDefault(codec) + 1;
                        try
                        {
                            using (Bitmap bmp = AkosImageDecoder.DecodeCel(child, i))
                            {
                                if (bmp != null)
                                {
                                    decoded++;
                                    decodedByCodec[codec] = decodedByCodec.GetValueOrDefault(codec) + 1;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (errors.Count < 10) errors.Add("akos#" + akosCount + " cel#" + i + " codec" + codec + ": " + ex.GetType().Name + " " + ex.Message);
                        }
                    }
                }
            }

            Assert.True(akosCount > 5, "too few AKOS costumes found: " + akosCount);
            Assert.True(totalCels > 50, "too few cels: " + totalCels);
            Assert.True(errors.Count == 0, "cel decode errors:\n" + string.Join("\n", errors));

            // The Dig and Full Throttle use three cel codecs (1 = BYLE RLE, 5 = BOMP, 16 = MAJMIN); each
            // must be present and decode every cel. This guards the regression where only codec 1 was
            // handled, so codec 5/16 costumes (e.g. Full Throttle LFLF 10 and 19) showed blank.
            foreach (int codec in new[] { 1, 5, 16 })
            {
                int cels = celsByCodec.GetValueOrDefault(codec);
                int ok = decodedByCodec.GetValueOrDefault(codec);
                Assert.True(cels > 0, "no codec-" + codec + " cels found");
                Assert.True(ok == cels, string.Format("codec {0}: only {1}/{2} cels decoded", codec, ok, cels));
            }
            Assert.True(decoded == totalCels, string.Format("only {0}/{1} cels decoded overall", decoded, totalCels));

            // Pixel-correctness guard for the codec-1 BYLE-RLE bit split. A 64-colour AKPL costume uses 6
            // colour bits, so its cels can reach palette indices > 31 (=> > 32 distinct colours). The old
            // binary 5/3 split could only reach indices 0-31, capping distinct colours at 32, so this fails
            // unless the 64 -> 6/2 split is correct. (Decode-returns-non-null alone never caught this.)
            int richestCodec1 = 0;
            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                foreach (BlockBase child in disk.Childrens)
                {
                    if (child.BlockType != "AKOS") continue;
                    if (AkosImageDecoder.GetCodec(child) != 1 || AkosImageDecoder.GetColorCount(child) != 64) continue;

                    int n = AkosImageDecoder.GetCelCount(child);
                    for (int i = 0; i < n && richestCodec1 <= 32; i++)
                    {
                        using (Bitmap bmp = AkosImageDecoder.DecodeCel(child, i))
                        {
                            if (bmp != null) richestCodec1 = Math.Max(richestCodec1, DistinctColors(bmp, 40));
                        }
                    }
                    if (richestCodec1 > 32) break;
                }
                if (richestCodec1 > 32) break;
            }
            Assert.True(richestCodec1 > 32,
                "codec-1 64-colour cels never exceed 32 distinct colours - the 64 -> 6/2 bit split is wrong (only 32 palette indices reachable)");
        }

        /// <summary>
        /// AKOS cel ENCODER round-trip for codec 1 (BYLE RLE) and codec 5 (BOMP): decoding a cel to its
        /// index matrix, re-encoding it with AkosImageEncoder.ReplaceCel (a no-op edit) and decoding again
        /// must reproduce the exact same indices. This exercises both encoders AND the AKCD/AKOF splice (the
        /// re-encoded run-length usually has a different byte length, so the offset table must be fixed up
        /// correctly). Edits are applied sequentially per costume, so a wrong offset fix corrupts later cels
        /// and fails here. (Codec 16 / MAJMIN is not encodable yet, so it is skipped.)
        /// </summary>
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void AkosCelEncodeRoundTrips(string path)
        {
            Skip.If(GameLibrary.Folder(path) == null, "not present: " + path);
            ScummGameData game = GameLibrary.Load(path);

            var testedByCodec = new Dictionary<int, int>();
            var mismatches = new List<string>();
            int total = 0;

            foreach (DiskBlock disk in game.DataFile.GetLFLFs())
            {
                foreach (BlockBase child in disk.Childrens)
                {
                    if (child.BlockType != "AKOS") continue;
                    int codec = AkosImageDecoder.GetCodec(child);
                    if (codec != 1 && codec != 5) continue;
                    if (testedByCodec.GetValueOrDefault(codec) >= 150) continue; // enough of this codec

                    int cels = AkosImageDecoder.GetCelCount(child);
                    for (int i = 0; i < cels; i++)
                    {
                        byte[,] before = AkosImageDecoder.DecodeCelIndices(child, i);
                        if (before == null) continue; // empty/placeholder cel

                        AkosImageEncoder.ReplaceCel(child, i, before);
                        byte[,] after = AkosImageDecoder.DecodeCelIndices(child, i);

                        testedByCodec[codec] = testedByCodec.GetValueOrDefault(codec) + 1;
                        total++;
                        if (after == null || !MatricesEqual(before, after))
                        {
                            if (mismatches.Count < 10) mismatches.Add("codec " + codec + " cel #" + i + " (" + before.GetLength(0) + "x" + before.GetLength(1) + ")");
                        }
                    }
                }
            }

            Assert.True(mismatches.Count == 0, "encode round-trip changed pixels for: " + string.Join(", ", mismatches));
            Assert.True(testedByCodec.GetValueOrDefault(1) > 20, "too few codec-1 cels exercised: " + testedByCodec.GetValueOrDefault(1));
            Assert.True(testedByCodec.GetValueOrDefault(5) > 20, "too few codec-5 cels exercised: " + testedByCodec.GetValueOrDefault(5));
        }

        /// <summary>
        /// End-to-end write-back: import (no-op re-encode) a codec-1 cel, SaveDataToDisk to a temp copy,
        /// reload from disk and decode the same cel - it must match. This proves the edited AKOS serializes
        /// into a valid game file (AKOS/sub-block sizes, LFLF/LECF positions and the index DCOS offset all
        /// recomputed), not just that the in-memory splice is right.
        /// </summary>
        [SkippableFact]
        public void AkosImportSurvivesSaveAndReload()
        {
            string src = GameLibrary.Folder(GameLibrary.FullThrottle);
            Skip.If(src == null, "not present: " + GameLibrary.FullThrottle);

            string dir = Path.Combine(Path.GetTempPath(), "v7akossave_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                // Only the index + data containers (.LA0/.LA1) are needed to detect, load and save back.
                foreach (string f in Directory.GetFiles(src, "*.LA?"))
                {
                    File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
                }

                GameInfo info = Functions.FindScummGameInFolder(dir);
                Assert.NotNull(info);
                Assert.Equal(7, info.ScummVersion);
                ScummGameData game = ScummGameData.LoadFromGameInfo(info);

                // Locate the first codec-1 AKOS cel with real content.
                int lflfIndex = -1, akosPos = -1, celIndex = -1;
                byte[,] expected = null;
                var lflfs = game.DataFile.GetLFLFs();
                for (int li = 0; li < lflfs.Count && expected == null; li++)
                {
                    int pos = -1;
                    foreach (BlockBase child in lflfs[li].Childrens)
                    {
                        if (child.BlockType != "AKOS") continue;
                        pos++;
                        if (AkosImageDecoder.GetCodec(child) != 1) continue;
                        int cels = AkosImageDecoder.GetCelCount(child);
                        for (int c = 0; c < cels; c++)
                        {
                            byte[,] idx = AkosImageDecoder.DecodeCelIndices(child, c);
                            if (idx != null && idx.GetLength(0) * idx.GetLength(1) > 100)
                            {
                                lflfIndex = li; akosPos = pos; celIndex = c; expected = idx;
                                AkosImageEncoder.ReplaceCel(child, c, idx); // no-op re-encode
                                break;
                            }
                        }
                        if (expected != null) break;
                    }
                }
                Assert.True(expected != null, "no codec-1 cel found to edit");

                game.SaveDataToDisk();

                // Reload from the saved temp copy and decode the same cel.
                ScummGameData reloaded = ScummGameData.LoadFromGameInfo(Functions.FindScummGameInFolder(dir));
                DiskBlock disk = reloaded.DataFile.GetLFLFs()[lflfIndex];
                BlockBase akos2 = disk.Childrens.Where(c => c.BlockType == "AKOS").ElementAt(akosPos);
                byte[,] actual = AkosImageDecoder.DecodeCelIndices(akos2, celIndex);

                Assert.True(actual != null && MatricesEqual(expected, actual),
                    "edited cel did not survive save+reload");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        /// <summary>
        /// ScriptPaletteScanner finds a literal setCurrentPalette(roomN) - pushByte/pushWord then roomOps
        /// (0x9C) + sub-op 213 - and ignores non-literal (variable) pushes. Pure synthetic bytecode.
        /// </summary>
        [Fact]
        public void ScriptPaletteScannerFindsLiteralSetCurrentPalette()
        {
            // pushByte 5; roomOps; SO_ROOM_NEW_PALETTE  -> room 5
            Assert.Equal(new[] { 5 }, ScriptPaletteScanner.FindCurrentPaletteRooms(new byte[] { 0x00, 5, 0x9C, 213 }, 0).ToArray());

            // pushWord 300; roomOps; SO_ROOM_NEW_PALETTE -> room 300 (0x012C)
            Assert.Equal(new[] { 300 }, ScriptPaletteScanner.FindCurrentPaletteRooms(new byte[] { 0x01, 0x2C, 0x01, 0x9C, 213 }, 0).ToArray());

            // pushByteVar 5 (0x02) is NOT a literal -> nothing recovered
            Assert.Empty(ScriptPaletteScanner.FindCurrentPaletteRooms(new byte[] { 0x02, 5, 0x9C, 213 }, 0).ToArray());

            // roomOps with a different sub-op (not 213) -> nothing
            Assert.Empty(ScriptPaletteScanner.FindCurrentPaletteRooms(new byte[] { 0x00, 5, 0x9C, 175 }, 0).ToArray());

            // honours startOffset (skip a leading id byte) and finds two references
            int[] two = ScriptPaletteScanner.FindCurrentPaletteRooms(new byte[] { 0xFF, 0x00, 7, 0x9C, 213, 0x00, 9, 0x9C, 213 }, 1).ToArray();
            Assert.Equal(new[] { 7, 9 }, two);
        }

        private static bool MatricesEqual(byte[,] a, byte[,] b)
        {
            if (a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int x = 0; x < a.GetLength(0); x++)
                for (int y = 0; y < a.GetLength(1); y++)
                    if (a[x, y] != b[x, y]) return false;
            return true;
        }

        /// <summary>Counts distinct ARGB colours in the bitmap, stopping once <paramref name="cap"/> is reached.</summary>
        private static int DistinctColors(Bitmap bmp, int cap)
        {
            var seen = new HashSet<int>();
            for (int y = 0; y < bmp.Height; y++)
            {
                for (int x = 0; x < bmp.Width; x++)
                {
                    seen.Add(bmp.GetPixel(x, y).ToArgb());
                    if (seen.Count >= cap) return seen.Count;
                }
            }
            return seen.Count;
        }
    }
}
