using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Extraction / editing of the SCUMM v2 font embedded in the game executable (MANIAC.EXE / ZAK.EXE).
    /// The font is RLE-compressed; it is located by a unique signature, decoded to a 128-glyph 8x8 buffer,
    /// edited through the v3 PNG atlas codec, and spliced back in place at the same EXE size. ScummVM
    /// ignores this font (it hardcodes its own), so this is the only path to accented glyphs under the
    /// original DOS engine.
    /// </summary>
    public class V2ExeFontTests
    {
        // The two accent-candidate slots used here are stored as literal bytes (editable in place).
        private const int AccentSlot = 0x7E; // '~'
        private const int RunSlot = 0x20;    // space: an all-zero compressed run (must refuse edits)

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void DecodesV2ExeFont(string relativePath)
        {
            byte[] exe = SkipOrLoadExe(relativePath);
            string error;
            ScummV2ExeFont font = ScummV2ExeFont.Read(exe, out error);

            Assert.NotNull(font);
            Assert.True(font.StreamStart > 0);
            Assert.Equal(1005, font.CompressedLength);                 // stock footprint, all editions
            Assert.Equal(ScummV2ExeFont.GlyphCount * 8, font.GlyphBytes.Length); // 1024

            for (int i = 0; i < 8; i++) Assert.Equal(0, font.GlyphBytes[i]); // glyph 0 = synthetic blank
            byte[] sig = { 0x01, 0x03, 0x06, 0x0C, 0x18, 0x3E, 0x03, 0x00, 0x80, 0xC0, 0x60, 0x30, 0x18, 0x7C, 0xC0, 0x00 };
            for (int i = 0; i < sig.Length; i++) Assert.Equal(sig[i], font.GlyphBytes[8 + i]); // glyphs 1+2
        }

        [Fact]
        public void LocateReturnsMinusOneWhenNoFont()
        {
            var noise = new byte[4096];
            for (int i = 0; i < noise.Length; i++) noise[i] = (byte)(i * 7);
            Assert.Equal(-1, ScummV2ExeFont.Locate(noise));
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void NoOpImportLeavesExeByteIdentical(string relativePath)
        {
            byte[] exe = SkipOrLoadExe(relativePath);
            WithExportedAtlas(exe, (font, png) =>
            {
                string report = ScummV2ExeFontCodec.ImportPng(font, png); // unedited atlas
                Assert.True(BytesEqual(exe, font.ExeBytes), "no-op import changed the EXE");
                Assert.Contains("No changes", report);
            });
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void AccentSlotEditRoundTripsInPlace(string relativePath)
        {
            byte[] exe = SkipOrLoadExe(relativePath);
            string error;
            byte[] before = ScummV2ExeFont.Read(exe, out error).GlyphBytes;

            WithExportedAtlas(exe, (font, png) =>
            {
                TogglePixel(png, AccentSlot);
                ScummV2ExeFontCodec.ImportPng(font, png);

                Assert.Equal(exe.Length, font.ExeBytes.Length);           // same-size in-place splice
                Assert.False(BytesEqual(exe, font.ExeBytes));             // something changed

                // The program byte immediately after the font stream is untouched.
                int after = font.StreamStart + font.CompressedLength;
                Assert.Equal(exe[after], font.ExeBytes[after]);

                // Re-read: only the edited glyph differs from the original decode.
                ScummV2ExeFont reread = ScummV2ExeFont.Read(font.ExeBytes, out error);
                Assert.NotNull(reread);
                for (int g = 0; g < ScummV2ExeFont.GlyphCount; g++)
                {
                    bool same = true;
                    for (int b = 0; b < 8; b++) if (reread.GlyphBytes[g * 8 + b] != before[g * 8 + b]) same = false;
                    if (g == AccentSlot) Assert.False(same, "the accent glyph did not change");
                    else Assert.True(same, "glyph 0x" + g.ToString("X2") + " changed unexpectedly");
                }
            });
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void RunByteEditIsRefused(string relativePath)
        {
            byte[] exe = SkipOrLoadExe(relativePath);
            string error;
            ScummV2ExeFont font = ScummV2ExeFont.Read(exe, out error);

            byte[] edited = (byte[])font.GlyphBytes.Clone();
            edited[RunSlot * 8] = 0xFF; // touch a byte that comes from a compressed run

            string applyError;
            bool ok = font.TryApplyEditedGlyphs(edited, out applyError);

            Assert.False(ok);
            Assert.Contains("0x20", applyError);
            Assert.True(BytesEqual(exe, font.ExeBytes), "a refused edit must leave the EXE untouched");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.ManiacV2)]
        [InlineData(GameLibrary.ZakV2)]
        public void EditingASignatureGlyphIsRefused(string relativePath)
        {
            byte[] exe = SkipOrLoadExe(relativePath);
            string error;
            ScummV2ExeFont font = ScummV2ExeFont.Read(exe, out error);

            // Glyph 0x01 is one of the box-drawing chars the locate signature is built from; changing it
            // would render in DOS but make the font unfindable for a later re-edit, so it must be refused.
            byte[] edited = (byte[])font.GlyphBytes.Clone();
            edited[0x01 * 8] = (byte)(edited[0x01 * 8] ^ 0xFF);

            string applyError;
            bool ok = font.TryApplyEditedGlyphs(edited, out applyError);

            Assert.False(ok);
            Assert.True(BytesEqual(exe, font.ExeBytes), "a refused edit must leave the EXE untouched");
            Assert.Equal(StreamStartLocatable(font.ExeBytes), true); // the original is still locatable
        }

        private static bool StreamStartLocatable(byte[] exe)
        {
            return ScummV2ExeFont.Locate(exe) >= 0;
        }

        // ------------------------------------------------------------------ helpers

        private static byte[] SkipOrLoadExe(string relativePath)
        {
            string folder = GameLibrary.Folder(relativePath);
            Skip.If(folder == null, "GameData folder not present: " + relativePath);
            string exe = null;
            foreach (string f in Directory.GetFiles(folder, "*.exe"))
            {
                string n = Path.GetFileName(f).ToUpperInvariant();
                if (n == "MANIAC.EXE" || n == "ZAK.EXE") { exe = f; break; }
                if (exe == null) exe = f;
            }
            Skip.If(exe == null, "no game .exe in " + relativePath);
            return File.ReadAllBytes(exe);
        }

        /// <summary>Reads the font, exports the atlas to a temp dir, runs the body, then cleans up.</summary>
        private static void WithExportedAtlas(byte[] exe, Action<ScummV2ExeFont, string> body)
        {
            string error;
            ScummV2ExeFont font = ScummV2ExeFont.Read((byte[])exe.Clone(), out error);
            Assert.NotNull(font);

            string dir = Path.Combine(Path.GetTempPath(), "v2exefont_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string png = Path.Combine(dir, "font.png");
                ScummV2ExeFontCodec.ExportPng(font, png, Path.Combine(dir, "font.guide.png"));
                body(font, png);
            }
            finally { Directory.Delete(dir, true); }
        }

        /// <summary>Toggles pixel (0,0) of an atlas slot, saved as an indexed PNG the codec accepts.</summary>
        private static void TogglePixel(string png, int slot)
        {
            int cellX = (slot % 16) * 8, cellY = (slot / 16) * 8;
            byte[,] mtx;
            using (var bmp = (Bitmap)Image.FromFile(png)) mtx = IndexedImageHelper.GetIndexMatrix(bmp);
            mtx[cellX, cellY] = (byte)(mtx[cellX, cellY] == 0 ? 1 : 0);
            using (Bitmap edited = IndexedImageHelper.FromIndexMatrix(mtx, CharsetV3PngCodec.BuildEditPalette(), -1))
                edited.Save(png, ImageFormat.Png);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
