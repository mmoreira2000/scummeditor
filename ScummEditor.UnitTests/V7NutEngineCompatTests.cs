using System;
using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 .NUT font re-encode ENGINE compatibility. A re-encoded glyph must stay loadable by the real
    /// engine, not merely decodable by our own decoder (the gap that let a broken re-encode ship and crash
    /// Full Throttle, which draws .NUT text at boot). After re-encoding every glyph these assert, mirroring
    /// ScummVM's NutRenderer::loadFont:
    ///   (1) the two frame-walk loops stay consistent - the decodedLength COUNT loop (advances by the FOBJ
    ///       size + its even pad) and the DECODE loop (advances by the FRME size + its even pad) must agree
    ///       on every char, or ScummVM overflows its glyph buffer. This requires the canonical layout: the
    ///       FOBJ chunk even-padded INSIDE the FRME so the FRME size is always even;
    ///   (2) decoding our re-encoded payload with ScummVM's exact codec1 (bompDecodeLine, setZero=false) /
    ///       codec21 yields pixels identical to decoding the original.
    /// </summary>
    public class V7NutEngineCompatTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.FullThrottle)]
        [InlineData(GameLibrary.TheDig)]
        public void ReencodedNutStaysEngineLoadable(string relativePath)
        {
            string folder = GameLibrary.Folder(relativePath);
            Skip.If(folder == null, "not present: " + relativePath);

            string[] nuts = Directory.GetFiles(folder, "*.NUT", SearchOption.AllDirectories);
            Skip.If(nuts.Length == 0, "no .NUT fonts");

            int fontsChecked = 0, glyphsChecked = 0;
            foreach (string path in nuts)
            {
                byte[] orig = File.ReadAllBytes(path);
                var f1 = new NutFont { FilePath = path };
                f1.LoadFromFileBytes(orig);
                if (!f1.IsValid) continue;

                var f2 = new NutFont { FilePath = path };
                f2.LoadFromFileBytes((byte[])orig.Clone());

                for (int i = 0; i < f1.Glyphs.Count; i++)
                {
                    NutGlyph g = f1.Glyphs[i];
                    if (!g.HasPixels || !NutImageEncoder.CanEncode(g.Codec)) continue;
                    int w = g.Width, h = g.Height;
                    if (w <= 0 || h <= 0) continue;

                    byte[] origPayload = Slice(f1.RawContent, g.FobjOffset + 22, g.FobjSize - 14);
                    byte[,] m = NutImageDecoder.DecodeGlyphIndices(f2, i);
                    NutImageEncoder.ReplaceGlyph(f2, i, m); // no-op re-encode: must reproduce engine-valid output
                    NutGlyph g2 = f2.Glyphs[i];
                    byte[] newPayload = Slice(f2.RawContent, g2.FobjOffset + 22, g2.FobjSize - 14);

                    byte[] a = ScummVmDecode(origPayload, w, h, g.Codec);
                    byte[] b = ScummVmDecode(newPayload, w, h, g.Codec);
                    Assert.True(SeqEqual(a, b),
                        string.Format("{0} glyph#{1} codec={2}: ScummVM decode of the re-encode differs from the original",
                            Path.GetFileName(path), i, g.Codec));
                    glyphsChecked++;
                }

                // The whole font must still walk consistently under ScummVM's two loadFont loops.
                AssertWalkConsistent(orig, "original " + Path.GetFileName(path));
                AssertWalkConsistent(f2.RawContent, "re-encoded " + Path.GetFileName(path));
                fontsChecked++;
            }

            Assert.True(fontsChecked > 0, "no NUT fonts checked");
            Assert.True(glyphsChecked > 0, "no NUT glyphs checked");
        }

        private static void AssertWalkConsistent(byte[] file, string label)
        {
            int baseOff = 8, length = file.Length - baseOff;
            int numChars = U16(file, baseOff + 10);

            int offset = 0, c1 = 0;
            for (int l = 0; l < numChars; l++)
            {
                if (offset + 8 > length) break;
                int cs = (int)U32BE(file, baseOff + offset + 4);
                long next = (long)offset + cs + 16 + (cs & 1);
                if (next + 18 > length) break;
                offset = (int)next; c1++;
            }

            offset = 0; int c2 = 0; bool ok = true;
            for (int l = 0; l < c1; l++)
            {
                if (offset + 8 > length) { ok = false; break; }
                int cs = (int)U32BE(file, baseOff + offset + 4);
                long next = (long)offset + cs + 8 + (cs & 1);
                if (next + 8 > length) { ok = false; break; }
                offset = (int)next;
                if (U32BE(file, baseOff + offset) != Tag("FRME")) { ok = false; break; }
                offset += 8;
                if (offset + 22 > length || U32BE(file, baseOff + offset) != Tag("FOBJ")) { ok = false; break; }
                c2++;
            }
            Assert.True(c1 == numChars && c2 == numChars && ok,
                string.Format("{0}: ScummVM frame walk inconsistent (numChars={1} count={2} decode={3} ok={4})", label, numChars, c1, c2, ok));
        }

        // ScummVM NutRenderer decode: codec1 = smushDecodeRLE(bompDecodeLine, setZero=false); codec21/44 = codec21.
        private static byte[] ScummVmDecode(byte[] p, int w, int h, int codec)
        {
            var dst = new byte[w * h];
            int sp = 0;
            try
            {
                if (codec == 1 || codec == 3)
                {
                    for (int y = 0; y < h; y++)
                    {
                        int lineLen = U16(p, sp);
                        BompLine(dst, y * w, p, sp + 2, w);
                        sp += lineLen + 2;
                    }
                }
                else
                {
                    for (int y = 0; y < h; y++)
                    {
                        int lineLen = U16(p, sp);
                        int next = sp + 2 + lineLen;
                        sp += 2;
                        int len = w, d = y * w;
                        do
                        {
                            int offs = U16(p, sp); sp += 2;
                            d += offs; len -= offs;
                            if (len <= 0) break;
                            int rw = U16(p, sp) + 1; sp += 2;
                            len -= rw;
                            if (len < 0) rw += len;
                            for (int k = 0; k < rw; k++) dst[d + k] = p[sp + k];
                            d += rw; sp += rw;
                        } while (len > 0);
                        sp = next;
                    }
                }
            }
            catch { return null; } // an out-of-bounds decode is itself a failure (would crash the engine)
            return dst;
        }

        private static void BompLine(byte[] dst, int dstOff, byte[] src, int sp, int len)
        {
            while (len > 0)
            {
                byte code = src[sp++];
                int num = (code >> 1) + 1;
                if (num > len) num = len;
                len -= num;
                if ((code & 1) != 0) { byte color = src[sp++]; if (color != 0) for (int k = 0; k < num; k++) dst[dstOff + k] = color; dstOff += num; }
                else { for (int k = 0; k < num; k++) { byte color = src[sp++]; if (color != 0) dst[dstOff] = color; dstOff++; } }
            }
        }

        private static int U16(byte[] a, int p) => a[p] | (a[p + 1] << 8);
        private static uint U32BE(byte[] a, int p) => (uint)((a[p] << 24) | (a[p + 1] << 16) | (a[p + 2] << 8) | a[p + 3]);
        private static uint Tag(string t) => (uint)((t[0] << 24) | (t[1] << 16) | (t[2] << 8) | t[3]);
        private static byte[] Slice(byte[] a, int o, int l) { if (o < 0 || l < 0 || o + l > a.Length) l = Math.Max(0, Math.Min(l, a.Length - o)); var r = new byte[l]; Array.Copy(a, o, r, 0, l); return r; }
        private static bool SeqEqual(byte[] a, byte[] b) { if (a == null || b == null) return false; if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
    }
}
