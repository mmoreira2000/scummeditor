using System;
using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Charset handling: ScummGameData.GetAllEditableCharsets (the enumeration extracted in Stage 2c)
    /// must equal the embedded charsets plus the standalone fonts, and exporting the charsets to PNG
    /// and importing them back must be lossless (the editor must not alter a font it round-trips).
    /// </summary>
    public class CharsetTests
    {
        private static byte[] Serialize(BlockBase block)
        {
            block.CalculateBlockSize();
            using (var ms = new MemoryStream())
            {
                block.SaveToBinaryWriter(ms);
                return ms.ToArray();
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.DayOfTheTentacleFloppy)] // v6: charsets embedded as CHAR blocks
        [InlineData(GameLibrary.MonkeyIsland1FloppyVga)] // v4: charsets are standalone 90x.LFL fonts
        public void GetAllEditableCharsetsEqualsEmbeddedPlusFonts(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.NotNull(game);

            int embedded = CharsetPngCodec.CollectCharsets(game.DataFile).Count;
            int standalone = game.Fonts.Count;

            List<Charset> all = game.GetAllEditableCharsets();

            Assert.Equal(embedded + standalone, all.Count);
            Assert.True(all.Count > 0, "expected at least one editable charset in " + relativePath);
        }

        [SkippableTheory]
        [InlineData(GameLibrary.DayOfTheTentacleFloppy)]
        [InlineData(GameLibrary.MonkeyIsland2Floppy)]
        public void CharsetPngExportThenImportIsLossless(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.NotNull(game);

            List<Charset> charsets = game.GetAllEditableCharsets();
            Assert.NotEmpty(charsets);

            var before = new byte[charsets.Count][];
            for (int i = 0; i < charsets.Count; i++) before[i] = Serialize(charsets[i]);

            string tempDir = Path.Combine(Path.GetTempPath(), "scummeditor_charset_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                CharsetPngCodec.ExportAll(charsets, tempDir);
                CharsetPngCodec.ImportAll(charsets, tempDir);

                for (int i = 0; i < charsets.Count; i++)
                {
                    byte[] after = Serialize(charsets[i]);
                    Assert.True(ByteArraysEqual(before[i], after),
                        string.Format("charset {0} changed after a PNG export/import round-trip in {1}", i, relativePath));
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
