using System;
using System.Collections.Generic;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Encodes SCUMM v4 EGA image strips - the inverse of Scumm4ImageDecoder.DecodeEgaStrip.
    ///
    /// It walks each 8-pixel-wide column top-to-bottom (then the next column) and, at every
    /// position, greedily picks the cheapest of the three drawStripEGA operations for the run that
    /// starts there:
    ///   - literal run : N pixels of one color;
    ///   - vertical run: N pixels each copied from the column to the left (same row);
    ///   - dither run  : N pixels alternating two colors (the dithering used for EGA gradients).
    /// This keeps re-encoded strips about as small as the originals (a plain literal-only encoder
    /// would bloat dithered areas). The output need not match the game's exact bytes - only decode
    /// back to the same pixels, which Scumm4ImageDecoder is the authority on.
    ///
    /// Op encoding (matches the decoder):
    ///   literal  : bit7=0. run in the high nibble (1..7), index in the low. run 0 = escape: the
    ///              control byte is just the index, then a run byte (8..255).
    ///   vertical : 0x80 | run (run 1..63). run 0 = escape: 0x80 then a run byte (64..255).
    ///   dither   : 0xC0 | run (run 1..63), then a color byte (high nibble = even pixels, low = odd).
    ///              run 0 = escape: 0xC0, the color byte, then a run byte (64..255).
    /// Runs longer than 255 are split.
    /// </summary>
    public static class Scumm4EgaStripEncoder
    {
        /// <summary>Encodes every 8-pixel-wide strip of the image; returns one raw byte run per strip.</summary>
        public static List<byte[]> EncodeImage(byte[,] indexMatrix, int width, int height)
        {
            int numStrips = width / 8;
            var strips = new List<byte[]>(numStrips);
            for (int strip = 0; strip < numStrips; strip++)
            {
                strips.Add(EncodeStrip(indexMatrix, strip * 8, height));
            }
            return strips;
        }

        /// <summary>Encodes one 8-pixel-wide, <paramref name="height"/>-tall strip, column-major.</summary>
        public static byte[] EncodeStrip(byte[,] m, int x0, int height)
        {
            var output = new List<byte>();
            int total = 8 * height;

            int p = 0;
            while (p < total)
            {
                byte color;
                int litRun = LiteralRun(m, x0, height, p, total, out color);
                int verRun = VerticalRun(m, x0, height, p, total);
                byte ditherByte;
                int ditRun = DitherRun(m, x0, height, p, total, out ditherByte);

                int litTake = Math.Min(litRun, 255);
                int verTake = Math.Min(verRun, 255);
                int ditTake = Math.Min(ditRun, 255);

                double litRatio = (double)litTake / LiteralCost(litTake);
                double verRatio = verTake > 0 ? (double)verTake / VerticalCost(verTake) : 0;
                double ditRatio = ditTake >= 2 ? (double)ditTake / DitherCost(ditTake) : 0;

                // Prefer the densest op; ties favour the cheaper ops (vertical, then dither).
                if (verRatio > 0 && verRatio >= litRatio && verRatio >= ditRatio)
                {
                    WriteVertical(output, verTake);
                    p += verTake;
                }
                else if (ditRatio > 0 && ditRatio >= litRatio)
                {
                    WriteDither(output, ditherByte, ditTake);
                    p += ditTake;
                }
                else
                {
                    WriteLiteral(output, color, litTake);
                    p += litTake;
                }
            }

            return output.ToArray();
        }

        // The colour index at column-major position p (x = p/height, y = p%height).
        private static byte PixelAt(byte[,] m, int x0, int height, int p)
        {
            return (byte)(m[x0 + p / height, p % height] & 0x0F);
        }

        private static int LiteralRun(byte[,] m, int x0, int height, int p, int total, out byte color)
        {
            color = PixelAt(m, x0, height, p);
            int run = 1;
            while (p + run < total && PixelAt(m, x0, height, p + run) == color)
            {
                run++;
            }
            return run;
        }

        // Pixels equal to the one in the column to the left, same row. Needs x >= 1, which holds for
        // the whole run because the column index only grows as the run advances.
        private static int VerticalRun(byte[,] m, int x0, int height, int p, int total)
        {
            int run = 0;
            int k = p;
            while (k < total)
            {
                int x = k / height;
                int y = k % height;
                if (x == 0 || (byte)(m[x0 + x, y] & 0x0F) != (byte)(m[x0 + x - 1, y] & 0x0F))
                {
                    break;
                }
                run++;
                k++;
            }
            return run;
        }

        // Pixels alternating two distinct colours A,B,A,B... (A = even positions = high nibble).
        private static int DitherRun(byte[,] m, int x0, int height, int p, int total, out byte ditherByte)
        {
            ditherByte = 0;
            if (p + 1 >= total)
            {
                return 0;
            }

            byte a = PixelAt(m, x0, height, p);
            byte b = PixelAt(m, x0, height, p + 1);
            if (a == b)
            {
                return 0;
            }

            ditherByte = (byte)((a << 4) | b);
            int run = 0;
            while (p + run < total)
            {
                byte expected = (run % 2 == 0) ? a : b;
                if (PixelAt(m, x0, height, p + run) != expected)
                {
                    break;
                }
                run++;
            }
            return run;
        }

        private static int LiteralCost(int run) { return run <= 7 ? 1 : 2; }
        private static int VerticalCost(int run) { return run <= 63 ? 1 : 2; }
        private static int DitherCost(int run) { return run <= 63 ? 2 : 3; }

        private static void WriteLiteral(List<byte> output, byte index, int run)
        {
            if (run <= 7)
            {
                output.Add((byte)((run << 4) | index)); // bit7 stays clear because run <= 7
            }
            else
            {
                output.Add(index);          // run nibble 0 => escape, bit7 clear
                output.Add((byte)run);      // real run (8..255)
            }
        }

        private static void WriteVertical(List<byte> output, int run)
        {
            if (run <= 63)
            {
                output.Add((byte)(0x80 | run));
            }
            else
            {
                output.Add(0x80);           // run field 0 => escape
                output.Add((byte)run);
            }
        }

        private static void WriteDither(List<byte> output, byte ditherByte, int run)
        {
            if (run <= 63)
            {
                output.Add((byte)(0xC0 | run));
                output.Add(ditherByte);
            }
            else
            {
                output.Add(0xC0);           // run field 0 => escape
                output.Add(ditherByte);     // colour byte is read before the run byte
                output.Add((byte)run);
            }
        }
    }
}
