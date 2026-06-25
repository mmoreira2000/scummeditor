using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes one edited glyph of a SCUMM v7 .NUT SMUSH font and splices it back into the file. The
    /// edit is index-based (an edited matrix of palette indices), so it is palette-independent and exactly
    /// reverses <see cref="NutImageDecoder"/>: codec 1/3 are written as BOMP run-length, codec 21/44 as the
    /// skip-copy run-length (the codec-44 transparent index is 2, the others 0). NUT frames are walked
    /// sequentially with no offset table, so splicing only rewrites the one FRME and fixes the outer ANIM
    /// size - every other frame is preserved byte-for-byte, and an unedited font never changes at all.
    /// </summary>
    public static class NutImageEncoder
    {
        public static bool CanEncode(int codec)
        {
            return NutImageDecoder.IsSupportedCodec(codec);
        }

        /// <summary>
        /// Replaces glyph <paramref name="index"/>'s pixels with <paramref name="indices"/> (which must be
        /// exactly the glyph's width x height), re-encoding with the glyph's own codec and rebuilding the
        /// font's RawContent. Throws <see cref="ImageEncodeException"/> on a bad index, an unsupported codec
        /// or a size mismatch.
        /// </summary>
        public static void ReplaceGlyph(NutFont font, int index, byte[,] indices)
        {
            if (font == null || font.RawContent == null || index < 0 || index >= font.Glyphs.Count)
            {
                throw new ImageEncodeException("no NUT glyph #" + index);
            }

            NutGlyph glyph = font.Glyphs[index];
            if (!glyph.HasFobj)
            {
                throw new ImageEncodeException("NUT glyph #" + index + " has no editable frame object");
            }
            if (!CanEncode(glyph.Codec))
            {
                throw new ImageEncodeException("NUT glyph #" + index + " uses unsupported codec " + glyph.Codec);
            }

            int width = indices.GetLength(0);
            int height = indices.GetLength(1);
            if (width != glyph.Width || height != glyph.Height)
            {
                throw new ImageEncodeException(string.Format(
                    "NUT glyph #{0} is {1}x{2}; the imported image is {3}x{4} (glyph size must match)",
                    index, glyph.Width, glyph.Height, width, height));
            }

            int transparency = NutImageDecoder.TransparencyIndex(glyph.Codec);
            byte[] payload = (glyph.Codec == 1 || glyph.Codec == 3)
                ? EncodeBomp(indices, width, height)
                : EncodeSkipCopy(indices, width, height, (byte)transparency);

            font.RawContent = SpliceFrame(font, glyph, payload);
            font.Reparse();
        }

        /// <summary>
        /// Rebuilds RawContent with the edited frame's new FOBJ payload spliced in. Everything before and
        /// after the frame is copied verbatim (NUT frames carry no absolute offsets, so the trailing frames
        /// just shift); the four reserved FOBJ bytes and any trailing FRME data are preserved, and the
        /// outer ANIM size is recomputed.
        /// </summary>
        private static byte[] SpliceFrame(NutFont font, NutGlyph glyph, byte[] payload)
        {
            byte[] src = font.RawContent;

            int fobjSize = 14 + payload.Length;             // codec/x/y/w/h(10) + 4 reserved + payload
            int fobjChunkLen = 8 + fobjSize;                // FOBJ tag+size + body
            int trailingFrmeLen = glyph.FrameSize - (8 + glyph.FobjSize); // FRME data after the FOBJ chunk
            if (trailingFrmeLen < 0) trailingFrmeLen = 0;
            int frameSize = fobjChunkLen + trailingFrmeLen; // FRME body size

            var frame = new MemoryStream();
            WriteTag(frame, "FRME");
            WriteUInt32BE(frame, (uint)frameSize);
            WriteTag(frame, "FOBJ");
            WriteUInt32BE(frame, (uint)fobjSize);
            WriteUInt16LE(frame, glyph.Codec);
            WriteUInt16LE(frame, glyph.XOffset);
            WriteUInt16LE(frame, glyph.YOffset);
            WriteUInt16LE(frame, glyph.Width);
            WriteUInt16LE(frame, glyph.Height);
            // The four reserved bytes (FOBJ +18..+21) are preserved from the original frame.
            frame.Write(src, glyph.FobjOffset + 18, 4);
            frame.Write(payload, 0, payload.Length);
            if (trailingFrmeLen > 0)
            {
                frame.Write(src, glyph.FobjOffset + 8 + glyph.FobjSize, trailingFrmeLen);
            }
            byte[] frameBytes = frame.ToArray();

            // Where this frame's block (including its even-alignment pad) ends in the original file.
            int oldNext = glyph.FrameOffset + 8 + glyph.FrameSize;
            if ((glyph.FrameSize & 1) != 0) oldNext++;

            var output = new MemoryStream();
            output.Write(src, 0, glyph.FrameOffset);   // everything before the frame, verbatim
            output.Write(frameBytes, 0, frameBytes.Length);
            if ((frameSize & 1) != 0) output.WriteByte(0); // re-pad to an even boundary like the original
            if (oldNext < src.Length)
            {
                output.Write(src, oldNext, src.Length - oldNext); // following frames + trailer, verbatim
            }

            byte[] result = output.ToArray();
            // Fix the outer ANIM chunk size (big-endian uint32 at offset 4).
            uint animSize = (uint)(result.Length - 8);
            result[4] = (byte)(animSize >> 24);
            result[5] = (byte)(animSize >> 16);
            result[6] = (byte)(animSize >> 8);
            result[7] = (byte)animSize;
            return result;
        }

        /// <summary>
        /// BOMP run-length encode (codec 1/3), row by row: each row is [size:uint16 LE] then control bytes
        /// (low bit = repeat, upper 7 = run-1). Runs of >= 2 equal bytes are emitted as repeats (max 128),
        /// the rest as literals (max 128). Reverses <see cref="NutImageDecoder"/>'s BOMP decode.
        /// </summary>
        private static byte[] EncodeBomp(byte[,] indices, int width, int height)
        {
            var output = new MemoryStream();
            var line = new List<byte>();
            var literals = new List<byte>();

            for (int y = 0; y < height; y++)
            {
                line.Clear();
                literals.Clear();

                int x = 0;
                while (x < width)
                {
                    byte value = indices[x, y];
                    int run = 1;
                    while (x + run < width && indices[x + run, y] == value && run < 128) run++;

                    if (run >= 2)
                    {
                        FlushLiterals(line, literals);
                        line.Add((byte)(((run - 1) << 1) | 1)); // repeat
                        line.Add(value);
                        x += run;
                    }
                    else
                    {
                        literals.Add(value);
                        if (literals.Count == 128) FlushLiterals(line, literals);
                        x++;
                    }
                }
                FlushLiterals(line, literals);

                output.WriteByte((byte)(line.Count & 0xFF));
                output.WriteByte((byte)((line.Count >> 8) & 0xFF));
                output.Write(line.ToArray(), 0, line.Count);
            }
            return output.ToArray();
        }

        private static void FlushLiterals(List<byte> line, List<byte> literals)
        {
            if (literals.Count == 0) return;
            line.Add((byte)((literals.Count - 1) << 1)); // literal (low bit 0)
            line.AddRange(literals);
            literals.Clear();
        }

        /// <summary>
        /// Skip-copy run-length encode (codec 21/44), row by row: each row is [size:uint16 LE] then records
        /// of [skip:uint16 LE][run-1:uint16 LE][run bytes], where a "skip" is a span of the transparent
        /// index. Reverses <see cref="NutImageDecoder"/>'s skip-copy decode.
        /// </summary>
        private static byte[] EncodeSkipCopy(byte[,] indices, int width, int height, byte transparency)
        {
            var output = new MemoryStream();
            var line = new List<byte>();

            for (int y = 0; y < height; y++)
            {
                line.Clear();
                int x = 0;
                while (x < width)
                {
                    int gapStart = x;
                    while (x < width && indices[x, y] == transparency) x++;
                    int skip = x - gapStart;

                    if (x >= width)
                    {
                        // Trailing transparent run: emit just the skip (the decoder stops after it).
                        if (skip > 0) AddUInt16LE(line, skip);
                        break;
                    }

                    int runStart = x;
                    while (x < width && indices[x, y] != transparency) x++;
                    int run = x - runStart;

                    AddUInt16LE(line, skip);
                    AddUInt16LE(line, run - 1);
                    for (int i = runStart; i < x; i++) line.Add(indices[i, y]);
                }

                output.WriteByte((byte)(line.Count & 0xFF));
                output.WriteByte((byte)((line.Count >> 8) & 0xFF));
                output.Write(line.ToArray(), 0, line.Count);
            }
            return output.ToArray();
        }

        private static void AddUInt16LE(List<byte> list, int value)
        {
            list.Add((byte)(value & 0xFF));
            list.Add((byte)((value >> 8) & 0xFF));
        }

        private static void WriteTag(Stream s, string tag)
        {
            for (int i = 0; i < 4; i++) s.WriteByte((byte)tag[i]);
        }

        private static void WriteUInt32BE(Stream s, uint value)
        {
            s.WriteByte((byte)(value >> 24));
            s.WriteByte((byte)(value >> 16));
            s.WriteByte((byte)(value >> 8));
            s.WriteByte((byte)value);
        }

        private static void WriteUInt16LE(Stream s, int value)
        {
            s.WriteByte((byte)(value & 0xFF));
            s.WriteByte((byte)((value >> 8) & 0xFF));
        }
    }
}
