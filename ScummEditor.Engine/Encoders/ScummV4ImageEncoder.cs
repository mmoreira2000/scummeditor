using System;
using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited bitmap back into a SCUMM v4 image block (BM room background or OI object
    /// image), the inverse of ScummV4ImageDecoder. The trailing z-planes are preserved unchanged.
    ///
    /// To keep the result as close to the original as possible, it works STRIP BY STRIP: each
    /// 8-pixel-wide column whose pixels are unchanged keeps its original bytes verbatim, so an
    /// untouched image round-trips byte-for-byte and a partially translated one differs only where
    /// it was actually edited. Only changed columns are re-encoded (VGA via the shared ImageEncoder
    /// codecs; EGA via ScummV4EgaStripEncoder).
    ///
    /// The imported bitmap must be palette-indexed and exactly the original size, so the stored
    /// palette indexes survive losslessly (the same rule as the v5/v6 importer).
    /// </summary>
    public class ScummV4ImageEncoder
    {
        // Transparent codec ids sit a fixed 0x14 above their non-transparent counterparts for every
        // compression method, so an originally-transparent strip can keep its transparency after a
        // (non-transparent) re-encode just by shifting the codec id - the bitstream is identical.
        private const int TransparentCodecShift = 0x14;

        public void EncodeBackground(ScummV4RoomBlock room, Bitmap bitmap)
        {
            RoomHeader header = room.GetHD();
            ScummV4ImageBlock background = room.GetBM();
            if (header == null || background == null)
            {
                throw new ImageEncodeException("This room has no background image to import into.");
            }

            byte[,] original = DecodeToMatrix(new ScummV4ImageDecoder().DecodeBackground(room));
            Encode(background, header.Width, header.Height, room.IsEga, bitmap, original);
        }

        public void EncodeObject(ScummV4RoomBlock room, ScummV4ImageBlock objectImage, ObjectCode objectCode, Bitmap bitmap)
        {
            if (objectImage == null || objectCode == null || objectCode.Width == 0 || objectCode.Height == 0)
            {
                throw new ImageEncodeException("This object has no image to import into.");
            }

            byte[,] original = DecodeToMatrix(new ScummV4ImageDecoder().DecodeObject(room, objectImage, objectCode));
            Encode(objectImage, objectCode.Width, objectCode.Height, room.IsEga, bitmap, original);
        }

        /// <summary>Re-encodes an edited room-background z-plane mask back into the BM block.</summary>
        public void EncodeBackgroundZPlane(ScummV4RoomBlock room, int zPlaneIndex, Bitmap bitmap)
        {
            RoomHeader header = room.GetHD();
            ScummV4ImageBlock background = room.GetBM();
            if (header == null || background == null)
            {
                throw new ImageEncodeException("This room has no background to import a z-plane into.");
            }
            EncodeZPlane(background, header.Width, header.Height, room.IsEga, zPlaneIndex, bitmap);
        }

        /// <summary>Re-encodes an edited object z-plane mask back into the OI block.</summary>
        public void EncodeObjectZPlane(ScummV4RoomBlock room, ScummV4ImageBlock objectImage, ObjectCode objectCode, int zPlaneIndex, Bitmap bitmap)
        {
            if (objectImage == null || objectCode == null || objectCode.Width == 0 || objectCode.Height == 0)
            {
                throw new ImageEncodeException("This object has no image to import a z-plane into.");
            }
            EncodeZPlane(objectImage, objectCode.Width, objectCode.Height, room.IsEga, zPlaneIndex, bitmap);
        }

        private void EncodeZPlane(ScummV4ImageBlock block, int width, int height, bool isEga, int zPlaneIndex, Bitmap bitmap)
        {
            if (bitmap.Width != width || bitmap.Height != height)
            {
                throw new ImageEncodeException(string.Format(
                    "The z-plane image must be {0}x{1} (the original size), but it is {2}x{3}.",
                    width, height, bitmap.Width, bitmap.Height));
            }

            int numStrips = width / 8;
            List<(int Start, int Length)> regions = block.GetZPlaneRegions(numStrips, isEga);
            if (zPlaneIndex < 0 || zPlaneIndex >= regions.Count)
            {
                throw new ImageEncodeException("This image has no z-plane at that position to import into.");
            }

            // The RLE mask scheme is not 1:1 with the game's original encoding (run boundaries can
            // differ while decoding to the same pixels), so a straight re-encode of an unchanged mask
            // would still shift bytes. Detect that case - the incoming mask matches what the original
            // bytes already decode to - and leave the block untouched, so a no-op import (e.g. the
            // batch round-trip) stays byte-for-byte identical.
            if (MaskUnchanged(block, regions[zPlaneIndex], numStrips, width, height, bitmap))
            {
                return;
            }

            // A z-plane mask is black/white (black = masked); the strips are encoded with a simple
            // run-length scheme that is the exact inverse of ZPlaneDecoder, so an unchanged mask
            // re-encodes to the same pixels.
            int numStripsForMask = width / 8;
            var strips = new List<ZPlaneStripData>(numStripsForMask);
            for (int n = 0; n < numStripsForMask; n++)
            {
                strips.Add(new ZPlaneStripData { ImageData = EncodeMaskStrip(bitmap, n * 8, height) });
            }
            block.RebuildZPlane(regions[zPlaneIndex].Start, regions[zPlaneIndex].Length, strips);
        }

        /// <summary>
        /// True when <paramref name="bitmap"/> is the same mask the existing z-plane bytes decode to
        /// (so re-encoding it would be a no-op). A pixel is "masked" when it is opaque black; every
        /// other pixel (white or transparent) is unmasked, matching <see cref="EncodeMaskStrip"/>.
        /// </summary>
        private static bool MaskUnchanged(ScummV4ImageBlock block, (int Start, int Length) region, int numStrips, int width, int height, Bitmap bitmap)
        {
            List<ZPlaneStripData> originalStrips = block.GetZPlaneStrips(region.Start, region.Length, numStrips);
            using (Bitmap originalMask = new ZPlaneDecoder().Decode(originalStrips, width, height))
            {
                if (originalMask == null)
                {
                    return false;
                }
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (IsMasked(bitmap.GetPixel(x, y)) != IsMasked(originalMask.GetPixel(x, y)))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static bool IsMasked(Color pixel)
        {
            return pixel.A != 0 && pixel.R == 0 && pixel.G == 0 && pixel.B == 0;
        }

        /// <summary>
        /// Encodes one 8-pixel-wide z-plane mask strip as run-length rows (the inverse of
        /// ZPlaneDecoder): a repeat run is 0x80|count + the row byte; a copy run is count + that many
        /// distinct row bytes. Each row byte holds 8 pixels (bit set = masked/black). Runs are capped
        /// at 127 so the decoder reads them back unchanged.
        /// </summary>
        private static byte[] EncodeMaskStrip(Bitmap bitmap, int x0, int height)
        {
            var rows = new byte[height];
            for (int y = 0; y < height; y++)
            {
                byte row = 0;
                for (int i = 0; i < 8; i++)
                {
                    Color pixel = bitmap.GetPixel(x0 + i, y);
                    // Masked = opaque black. Transparent pixels (alpha 0) count as unmasked, so a mask
                    // with undrawn/erased areas (or an editor's transparency) re-encodes the way the
                    // game reads it - white and transparent both mean "not masked here".
                    if (pixel.A != 0 && pixel.R == 0 && pixel.G == 0 && pixel.B == 0)
                    {
                        row |= (byte)(1 << (7 - i));
                    }
                }
                rows[y] = row;
            }

            var output = new List<byte>();
            int line = 0;
            while (line < height)
            {
                int repeat = 1;
                while (line + repeat < height && rows[line + repeat] == rows[line] && repeat < 127)
                {
                    repeat++;
                }

                if (repeat >= 2)
                {
                    output.Add((byte)(0x80 | repeat));
                    output.Add(rows[line]);
                    line += repeat;
                }
                else
                {
                    // Collect distinct rows until a repeat run starts (or the strip/cap ends).
                    var literals = new List<byte>();
                    while (line < height && literals.Count < 127)
                    {
                        if (line + 1 < height && rows[line + 1] == rows[line])
                        {
                            break; // a repeat run starts here; leave it for the repeat branch
                        }
                        literals.Add(rows[line]);
                        line++;
                    }
                    output.Add((byte)literals.Count);
                    output.AddRange(literals);
                }
            }
            return output.ToArray();
        }

        private void Encode(ScummV4ImageBlock block, int width, int height, bool isEga, Bitmap bitmap, byte[,] original)
        {
            if (bitmap.Width != width || bitmap.Height != height)
            {
                throw new ImageEncodeException(string.Format(
                    "The image must be {0}x{1} (the original size), but it is {2}x{3}.",
                    width, height, bitmap.Width, bitmap.Height));
            }

            if (!IndexedImageHelper.IsIndexed(bitmap))
            {
                throw new ImageEncodeException("The image must be an indexed (palette-based) PNG so the original palette indexes are preserved. Re-export it from ScummEditor and edit it without converting it to RGB/truecolor.");
            }

            byte[,] newMatrix = IndexedImageHelper.GetIndexMatrix(bitmap);
            int numStrips = width / 8;

            if (isEga)
            {
                List<byte[]> newStrips = ScummV4EgaStripEncoder.EncodeImage(newMatrix, width, height);
                List<byte[]> originalStrips = ExtractEgaStrips(block, numStrips);

                var finalStrips = new List<byte[]>(numStrips);
                for (int i = 0; i < numStrips; i++)
                {
                    bool reuse = originalStrips != null && ColumnUnchanged(newMatrix, original, i, height);
                    finalStrips.Add(reuse ? originalStrips[i] : newStrips[i]);
                }
                block.RebuildEgaContents(finalStrips);
            }
            else
            {
                List<StripData> originalStrips = ExtractVgaStrips(block, numStrips);
                List<StripData> newStrips = EncodeVgaStrips(newMatrix, width, height, originalStrips);

                var finalStrips = new List<StripData>(numStrips);
                for (int i = 0; i < numStrips; i++)
                {
                    bool reuse = originalStrips != null && ColumnUnchanged(newMatrix, original, i, height);
                    finalStrips.Add(reuse ? originalStrips[i] : newStrips[i]);
                }
                block.RebuildVgaContents(finalStrips);
            }
        }

        /// <summary>True when the 8-pixel column <paramref name="strip"/> is identical in both matrices.</summary>
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
            {
                for (int y = 0; y < height; y++)
                {
                    if (newMatrix[x, y] != original[x, y])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        protected virtual List<StripData> EncodeVgaStrips(byte[,] indexMatrix, int width, int height, List<StripData> originalStrips)
        {
            // Encode with a transparency value that no pixel uses, so the codec picker yields
            // non-transparent codecs; then restore transparency per strip from the original block.
            byte sentinel = FindUnusedIndex(indexMatrix, width, height);
            List<StripData> strips = new ImageEncoder()
                .EncodeStrips(indexMatrix, width, height, sentinel, ImageEncoder.EncodeTypeSettings.AutoDetect);

            if (originalStrips != null)
            {
                for (int i = 0; i < strips.Count && i < originalStrips.Count; i++)
                {
                    // A transparent strip keeps its masking only if it stays a compressed codec;
                    // uncompressed (0x01) has no transparent variant, so it is left as-is (rare).
                    if (originalStrips[i].Transparent && strips[i].CompressionType != CompressionTypes.Uncompressed)
                    {
                        strips[i].CodecId = (byte)(strips[i].CodecId + TransparentCodecShift);
                    }
                }
            }

            return strips;
        }

        /// <summary>Parses the original VGA strips (codec + bitstream) for reuse; null if malformed.</summary>
        private List<StripData> ExtractVgaStrips(ScummV4ImageBlock block, int numStrips)
        {
            byte[] body = block.Contents;
            int baseIndex = block.StripTableStart;
            if (numStrips <= 0 || body.Length < baseIndex + 4 + numStrips * 4)
            {
                return null;
            }

            int smapLen = (int)ReadUInt32(body, baseIndex);
            if (smapLen <= 0 || baseIndex + smapLen > body.Length)
            {
                return null;
            }

            var offsets = new int[numStrips];
            for (int n = 0; n < numStrips; n++)
            {
                offsets[n] = (int)ReadUInt32(body, baseIndex + 4 + n * 4);
            }

            var strips = new List<StripData>(numStrips);
            for (int n = 0; n < numStrips; n++)
            {
                int start = offsets[n];
                int end = (n < numStrips - 1) ? offsets[n + 1] : smapLen;
                int codecPosition = baseIndex + start;
                int dataLength = end - start - 1;
                if (start < 0 || end > smapLen || dataLength < 0 || codecPosition + 1 + dataLength > body.Length)
                {
                    return null;
                }

                var data = new byte[dataLength];
                Array.Copy(body, codecPosition + 1, data, 0, dataLength);
                strips.Add(new StripData { CodecId = body[codecPosition], ImageData = data });
            }
            return strips;
        }

        /// <summary>Parses the original EGA strips (raw RLE bytes) for reuse; null if malformed.</summary>
        private List<byte[]> ExtractEgaStrips(ScummV4ImageBlock block, int numStrips)
        {
            byte[] body = block.Contents;
            int baseIndex = block.StripTableStart;
            if (numStrips <= 0 || body.Length < baseIndex + 2 + numStrips * 2)
            {
                return null;
            }

            int smapLen = ReadUInt16(body, baseIndex);
            if (smapLen <= 0 || baseIndex + smapLen > body.Length)
            {
                return null;
            }

            var offsets = new int[numStrips];
            for (int n = 0; n < numStrips; n++)
            {
                offsets[n] = ReadUInt16(body, baseIndex + 2 + n * 2);
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
                var data = new byte[end - start];
                Array.Copy(body, baseIndex + start, data, 0, data.Length);
                strips.Add(data);
            }
            return strips;
        }

        private static byte[,] DecodeToMatrix(Bitmap bitmap)
        {
            return bitmap == null ? null : IndexedImageHelper.GetIndexMatrix(bitmap);
        }

        private static byte FindUnusedIndex(byte[,] indexMatrix, int width, int height)
        {
            var used = new bool[256];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    used[indexMatrix[x, y]] = true;
                }
            }
            for (int i = 255; i >= 0; i--)
            {
                if (!used[i])
                {
                    return (byte)i;
                }
            }
            return 255; // every index is used (extremely unlikely for a single image)
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }
    }
}
