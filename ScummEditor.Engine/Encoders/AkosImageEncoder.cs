using System;
using System.Collections.Generic;
using System.Linq;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited SCUMM v7 AKOS cel and splices it back into the costume's AKCD pixel pool,
    /// fixing the AKOF offset table. Codec 1 (BYLE RLE) is the inverse of AkosImageDecoder.DecodeByleRle;
    /// codecs 5 (BOMP) and 16 (MAJMIN) are not encoded yet. The cel keeps its original width/height (AKCI is
    /// untouched). Only the AKCD/AKOF sub-block bytes change; the AKOS block size, the LFLF/LECF positions
    /// and the index (DCOS) offset to the costume are recomputed by the normal save path (CalculateBlockSize
    /// / CalculateOffsets / FixUpIndexOffsets), so the saved game stays consistent.
    /// </summary>
    public static class AkosImageEncoder
    {
        /// <summary>True when this costume's cels can be re-encoded (codec 1 BYLE RLE or codec 5 BOMP).</summary>
        public static bool CanEncode(BlockBase akos)
        {
            int codec = AkosImageDecoder.GetCodec(akos);
            return codec == 1 || codec == 5;
        }

        /// <summary>
        /// Replaces cel <paramref name="celIndex"/>'s pixels with <paramref name="indices"/> (a
        /// width-by-height matrix of costume-colour indices), re-encoding with the costume's codec and
        /// splicing the result into AKCD. The matrix must match the cel's stored width/height.
        /// </summary>
        public static void ReplaceCel(BlockBase akos, int celIndex, byte[,] indices)
        {
            int codec = AkosImageDecoder.GetCodec(akos);
            if (codec != 1 && codec != 5)
            {
                throw new ImageEncodeException("AKOS cel import is implemented for codec 1 (BYLE RLE) and codec 5 (BOMP); this costume uses codec " + codec + ".");
            }

            RawContainerBlock akof = GetSub(akos, "AKOF");
            RawContainerBlock akci = GetSub(akos, "AKCI");
            RawContainerBlock akcd = GetSub(akos, "AKCD");
            RawContainerBlock akpl = GetSub(akos, "AKPL");
            if (akof == null || akci == null || akcd == null || akpl == null)
            {
                throw new ImageEncodeException("AKOS is missing a required sub-block (AKOF/AKCI/AKCD/AKPL).");
            }

            int recordBase = celIndex * 6;
            if (recordBase + 6 > akof.Contents.Length)
            {
                throw new ImageEncodeException("Cel index " + celIndex + " is out of range.");
            }

            int celStart = (int)ReadU32(akof.Contents, recordBase);
            int akciOffset = ReadU16(akof.Contents, recordBase + 4);
            // Validate the offsets read from AKOF before using them (mirror the decoder's guards), so a
            // corrupt/edited AKOF gives a controlled ImageEncodeException instead of an IndexOutOfRange.
            if (celStart < 0 || celStart >= akcd.Contents.Length)
            {
                throw new ImageEncodeException("AKCD offset " + celStart + " is out of range for cel " + celIndex + ".");
            }
            if (akciOffset + 4 > akci.Contents.Length)
            {
                throw new ImageEncodeException("AKCI offset " + akciOffset + " is out of range for cel " + celIndex + ".");
            }
            int width = ReadU16(akci.Contents, akciOffset);
            int height = ReadU16(akci.Contents, akciOffset + 2);
            if (indices.GetLength(0) != width || indices.GetLength(1) != height)
            {
                throw new ImageEncodeException(string.Format(
                    "The image must be {0}x{1} (the cel's size); got {2}x{3}.",
                    width, height, indices.GetLength(0), indices.GetLength(1)));
            }

            // The encoders are validated separately (V7CostumeTests.AkosCelEncodeRoundTrips decodes the
            // re-encoded bytes and asserts the indices are identical), so no per-call round-trip is done
            // here. A valid (non-empty) cel always yields at least one byte.
            byte[] newData = codec == 1 ? EncodeByleRle(indices, akpl.Contents.Length) : EncodeBomp(indices);

            // The cel runs from celStart up to the next cel's start in AKCD (or the end of AKCD).
            int celEnd = akcd.Contents.Length;
            for (int i = 0; i + 6 <= akof.Contents.Length; i += 6)
            {
                int off = (int)ReadU32(akof.Contents, i);
                if (off > celStart && off < celEnd)
                {
                    celEnd = off;
                }
            }

            int delta = newData.Length - (celEnd - celStart);

            // Rebuild AKCD with the cel's slice replaced.
            byte[] oldAkcd = akcd.Contents;
            var newAkcd = new byte[oldAkcd.Length + delta];
            Array.Copy(oldAkcd, 0, newAkcd, 0, celStart);
            Array.Copy(newData, 0, newAkcd, celStart, newData.Length);
            Array.Copy(oldAkcd, celEnd, newAkcd, celStart + newData.Length, oldAkcd.Length - celEnd);

            // Shift every AKOF entry that points at data after the replaced cel.
            byte[] newAkof = (byte[])akof.Contents.Clone();
            for (int i = 0; i + 6 <= newAkof.Length; i += 6)
            {
                int off = (int)ReadU32(newAkof, i);
                if (off >= celEnd)
                {
                    WriteU32(newAkof, i, off + delta);
                }
            }

            akcd.Contents = newAkcd;
            akof.Contents = newAkof;
        }

        /// <summary>
        /// AKOS codec 1 (BYLE RLE) encoder: the inverse of AkosImageDecoder.DecodeByleRle. Column-major
        /// run-length; each byte holds the colour in the high bits and the run in the low bits, the split
        /// keyed on the colour count (16 → 4/4, 32 → 5/3, 64 → 6/2). A run longer than the low-bits maximum
        /// is written as an escape (low bits 0) followed by a length byte (1..255); runs &gt; 255 are split.
        /// </summary>
        public static byte[] EncodeByleRle(byte[,] indices, int paletteSize)
        {
            int width = indices.GetLength(0);
            int height = indices.GetLength(1);
            int runBits = paletteSize == 32 ? 3 : (paletteSize == 64 ? 2 : 4);
            int runMask = (1 << runBits) - 1;
            int maxColor = (1 << (8 - runBits)) - 1;

            var output = new List<byte>();
            int x = 0, y = 0;
            while (x < width)
            {
                int color = indices[x, y];
                if (color > maxColor)
                {
                    throw new ImageEncodeException(string.Format(
                        "Pixel palette index {0} exceeds the costume's {1}-colour palette; the image must use only the costume's colours.",
                        color, paletteSize));
                }

                int runLen = 0;
                while (x < width && indices[x, y] == color)
                {
                    runLen++;
                    y++;
                    if (y == height)
                    {
                        y = 0;
                        x++;
                    }
                }

                while (runLen > 0)
                {
                    int chunk = Math.Min(runLen, 255);
                    if (chunk <= runMask)
                    {
                        output.Add((byte)((color << runBits) | chunk));
                    }
                    else
                    {
                        output.Add((byte)(color << runBits)); // low bits 0 = "run is in the next byte"
                        output.Add((byte)chunk);
                    }
                    runLen -= chunk;
                }
            }
            return output.ToArray();
        }

        /// <summary>
        /// AKOS codec 5 (CDAT/BOMP) encoder: the inverse of AkosImageDecoder.DecodeBomp. Each row is a
        /// uint16 LE byte-length followed by run-length data: a control byte whose low bit is the "repeat"
        /// flag and whose upper 7 bits are (run-1); a repeat run stores one colour byte, a literal run stores
        /// <c>run</c> colour bytes. Runs are capped at 128 (the 7-bit limit). Colours are full bytes (0-255).
        /// </summary>
        public static byte[] EncodeBomp(byte[,] indices)
        {
            int width = indices.GetLength(0);
            int height = indices.GetLength(1);
            var result = new List<byte>();

            for (int y = 0; y < height; y++)
            {
                var line = new List<byte>();
                int x = 0;
                while (x < width)
                {
                    int color = indices[x, y];
                    int runLen = 1;
                    while (x + runLen < width && indices[x + runLen, y] == color)
                    {
                        runLen++;
                    }

                    if (runLen >= 2)
                    {
                        int remaining = runLen;
                        while (remaining > 0)
                        {
                            int chunk = Math.Min(remaining, 128);
                            line.Add((byte)(((chunk - 1) << 1) | 1)); // repeat
                            line.Add((byte)color);
                            remaining -= chunk;
                        }
                        x += runLen;
                    }
                    else
                    {
                        // Gather a literal run of single (non-repeating) pixels.
                        int litStart = x;
                        int litLen = 0;
                        while (x < width)
                        {
                            int c = indices[x, y];
                            int rl = 1;
                            while (x + rl < width && indices[x + rl, y] == c) rl++;
                            if (rl >= 2) break; // a repeat run starts here - end the literal
                            litLen++;
                            x++;
                        }

                        int pos = litStart;
                        int rem = litLen;
                        while (rem > 0)
                        {
                            int chunk = Math.Min(rem, 128);
                            line.Add((byte)(((chunk - 1) << 1) | 0)); // literal
                            for (int k = 0; k < chunk; k++) line.Add((byte)indices[pos + k, y]);
                            pos += chunk;
                            rem -= chunk;
                        }
                    }
                }

                result.Add((byte)(line.Count & 0xFF));
                result.Add((byte)((line.Count >> 8) & 0xFF));
                result.AddRange(line);
            }
            return result.ToArray();
        }

        private static RawContainerBlock GetSub(BlockBase akos, string tag)
        {
            return akos.Childrens.FirstOrDefault(c => c.BlockType == tag) as RawContainerBlock;
        }

        private static long ReadU32(byte[] b, int o)
        {
            return (long)b[o] | ((long)b[o + 1] << 8) | ((long)b[o + 2] << 16) | ((long)b[o + 3] << 24);
        }

        private static int ReadU16(byte[] b, int o)
        {
            return b[o] | (b[o + 1] << 8);
        }

        private static void WriteU32(byte[] b, int o, long value)
        {
            b[o] = (byte)(value & 0xFF);
            b[o + 1] = (byte)((value >> 8) & 0xFF);
            b[o + 2] = (byte)((value >> 16) & 0xFF);
            b[o + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
