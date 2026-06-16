using System.IO;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// The core data-integrity guarantee: loading a v5/v6 data file and saving it back produces the
    /// exact same bytes (the editor must never corrupt a game it merely opened). The on-disk file is
    /// whole-file XOR-encrypted, so we compare the decrypted original against the re-serialized blocks.
    /// The v4 multi-disk round-trip is exercised by the validation harness.
    /// </summary>
    public class RoundTripTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland2Floppy)]
        [InlineData(GameLibrary.MonkeyIsland1CdVga)]
        [InlineData(GameLibrary.FateOfAtlantisFloppy)]
        [InlineData(GameLibrary.DayOfTheTentacleFloppy)]
        [InlineData(GameLibrary.SamAndMaxFloppy)]
        public void ResavingAV5V6DataFileIsByteIdentical(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.NotNull(game);
            Assert.NotNull(game.DataFile);

            byte[] raw = File.ReadAllBytes(game.LoadedGameInfo.DataFile);
            int key = game.LoadedGameInfo.XorKey;
            var original = new byte[raw.Length];
            for (int i = 0; i < raw.Length; i++) original[i] = (byte)(raw[i] ^ key);

            game.DataFile.CalculateBlockSize();
            game.DataFile.CalculateOffsets();
            using (var ms = new MemoryStream())
            {
                game.DataFile.SaveToBinaryWriter(ms);
                byte[] resaved = ms.ToArray();

                Assert.Equal(original.Length, resaved.Length);

                int diff = FirstDifference(original, resaved);
                Assert.True(diff < 0, string.Format("first differing byte at offset 0x{0:X} in {1}", diff, relativePath));
            }
        }

        private static int FirstDifference(byte[] a, byte[] b)
        {
            int n = a.Length < b.Length ? a.Length : b.Length;
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i]) return i;
            }
            return a.Length == b.Length ? -1 : n;
        }
    }
}
