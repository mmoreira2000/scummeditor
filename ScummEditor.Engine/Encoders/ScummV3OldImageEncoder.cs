using System;
using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Engine.Exceptions;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited EGA image (a SCUMM v3 "old bundle" room background or object image) into a
    /// fresh strip table, the inverse of ScummV3OldImageDecoder. The table format is the v4 EGA one -
    /// [smapLen:u16][numStrips x offset:u16][raw strips] with offsets relative to the table start and
    /// no codec byte - so the strip RLE itself is produced by ScummV4EgaStripEncoder.
    ///
    /// As in the v4 encoder it works strip by strip: an 8-pixel column whose pixels are unchanged keeps
    /// its original bytes verbatim, so an untouched image re-encodes byte-for-byte and a partially
    /// translated one differs only where it was painted. The bitmap must be palette-indexed and exactly
    /// the original size. The caller splices the returned bytes over the old table and runs
    /// ScummV3OldWriter.ApplyEdit to re-point the offsets the size change shifts.
    /// </summary>
    public static class ScummV3OldImageEncoder
    {
        /// <summary>
        /// Builds the new EGA strip table for an image at <paramref name="imageOffset"/> of the room
        /// bytes, given the edited bitmap. Throws ImageEncodeException on a wrong size / non-indexed
        /// bitmap or a malformed original table.
        /// </summary>
        public static byte[] Encode(byte[] roomData, int imageOffset, int width, int height, Bitmap bitmap)
        {
            if (bitmap.Width != width || bitmap.Height != height)
            {
                throw new ImageEncodeException(string.Format(
                    "The image must be {0}x{1} (the original size), but it is {2}x{3}.",
                    width, height, bitmap.Width, bitmap.Height));
            }
            if (!IndexedImageHelper.IsIndexed(bitmap))
            {
                throw new ImageEncodeException("The image must be an indexed (palette-based) PNG so the original colour indexes are preserved. Re-export it from ScummEditor and edit it without converting it to RGB.");
            }

            int numStrips = width / 8;
            List<byte[]> originalStrips = ParseStrips(roomData, imageOffset, numStrips);
            if (originalStrips == null)
            {
                throw new ImageEncodeException("The original image strip table is malformed; cannot import.");
            }

            byte[,] newMatrix = IndexedImageHelper.GetIndexMatrix(bitmap);
            byte[,] original = new ScummV4ImageDecoder().DecodeEgaStripsToMatrix(originalStrips, width, height);
            List<byte[]> encoded = ScummV4EgaStripEncoder.EncodeImage(newMatrix, width, height);

            var finalStrips = new List<byte[]>(numStrips);
            for (int i = 0; i < numStrips; i++)
            {
                bool reuse = ColumnUnchanged(newMatrix, original, i, height);
                finalStrips.Add(reuse ? originalStrips[i] : encoded[i]);
            }

            byte[] table = BuildTable(finalStrips);

            // Keep the edit SIZE-NEUTRAL when the re-encoded table is smaller than the original: pad it
            // back to the original strip-table length so the room file (and therefore the whole 00.LFL
            // index and its detection MD5) stays byte-identical. Otherwise a size-changing edit moves the
            // room's sub-resource offsets in 00.LFL, the index MD5 changes, and ScummVM can no longer
            // match the game in its database - for some editions (e.g. Loom Floppy EGA v1.1, which its
            // fuzzy fallback rejects) that makes the edited game refuse to load even though the data is
            // valid. ScummVM and our decoder both read the strips through the offset table and ignore the
            // trailing padding; the engine self-locates the z-plane mask at imageOffset + smapLen, so
            // restoring smapLen to the original keeps the mask exactly where it was.
            int originalSmapLen = ReadU16(roomData, imageOffset);
            if (table.Length < originalSmapLen)
            {
                table = PadToLength(table, originalSmapLen);
            }
            return table;
        }

        /// <summary>Parses the original EGA strips (raw RLE bytes per strip); null if the table is malformed.</summary>
        private static List<byte[]> ParseStrips(byte[] data, int baseIndex, int numStrips)
        {
            if (numStrips <= 0 || baseIndex < 0 || baseIndex + 2 + numStrips * 2 > data.Length)
            {
                return null;
            }
            int smapLen = ReadU16(data, baseIndex);
            if (smapLen < 2 + numStrips * 2 || baseIndex + smapLen > data.Length)
            {
                return null;
            }

            var offsets = new int[numStrips];
            for (int n = 0; n < numStrips; n++)
            {
                offsets[n] = ReadU16(data, baseIndex + 2 + n * 2);
            }

            var strips = new List<byte[]>(numStrips);
            for (int n = 0; n < numStrips; n++)
            {
                int start = offsets[n];
                int end = (n < numStrips - 1) ? offsets[n + 1] : smapLen;
                if (start < 0 || end > smapLen || end < start)
                {
                    return null;
                }
                var bytes = new byte[end - start];
                Array.Copy(data, baseIndex + start, bytes, 0, bytes.Length);
                strips.Add(bytes);
            }
            return strips;
        }

        /// <summary>Builds [smapLen:u16][numStrips x offset:u16][strips]; offsets relative to the table start.</summary>
        private static byte[] BuildTable(List<byte[]> strips)
        {
            int offsetTableSize = 2 + strips.Count * 2;
            int dataSize = 0;
            foreach (byte[] s in strips) dataSize += s.Length;
            int smapLen = offsetTableSize + dataSize;

            var result = new byte[smapLen];
            WriteU16(result, 0, smapLen);
            int running = offsetTableSize;
            for (int n = 0; n < strips.Count; n++)
            {
                WriteU16(result, 2 + n * 2, running);
                Array.Copy(strips[n], 0, result, running, strips[n].Length);
                running += strips[n].Length;
            }
            return result;
        }

        private static bool ColumnUnchanged(byte[,] newMatrix, byte[,] original, int strip, int height)
        {
            if (original == null
                || original.GetLength(0) != newMatrix.GetLength(0)
                || original.GetLength(1) != newMatrix.GetLength(1))
            {
                return false;
            }
            int x0 = strip * 8;
            for (int x = x0; x < x0 + 8; x++)
                for (int y = 0; y < height; y++)
                    if ((newMatrix[x, y] & 0x0F) != (original[x, y] & 0x0F)) return false;
            return true;
        }

        /// <summary>
        /// Returns a copy of <paramref name="table"/> grown to <paramref name="targetLength"/> with
        /// trailing zero bytes, and its leading smapLen word set to the new length so the strip data
        /// and the z-plane that follows stay byte-aligned with the original (the decoders read strips by
        /// the offset table and never touch the padding).
        /// </summary>
        private static byte[] PadToLength(byte[] table, int targetLength)
        {
            var padded = new byte[targetLength];
            Array.Copy(table, 0, padded, 0, table.Length);
            WriteU16(padded, 0, targetLength);
            return padded;
        }

        private static int ReadU16(byte[] data, int p) { return data[p] | (data[p + 1] << 8); }

        private static void WriteU16(byte[] data, int p, int value)
        {
            data[p] = (byte)(value & 0xFF);
            data[p + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
