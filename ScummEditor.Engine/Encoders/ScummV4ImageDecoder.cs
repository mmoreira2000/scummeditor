using System;
using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes SCUMM v4 room backgrounds and object images. A v4 image is a single flat block
    /// (BM for the room, OI for an object) whose body is a strip table; there is no RMIM/IM00/SMAP
    /// nesting and no TRNS/PALS, so this cannot use the v5/v6 ImageDecoder.Decode(RoomBlock) path.
    ///
    /// The body layout depends on the graphics edition:
    ///   VGA (256 colors): smapLen = LE32 @ base+0; strip offset n = LE32 @ base+4+n*4; at base+offset
    ///                     sits a 1-byte codec id then that strip's bitstream (the same codecs the
    ///                     v5/v6 decoder uses, so the per-strip work is shared with ImageDecoder).
    ///   EGA (16 colors):  smapLen = LE16 @ base+0; strip offset n = LE16 @ base+2+n*2; no codec byte
    ///                     - the strip is a byte-oriented RLE consumed by DecodeEgaStrip below.
    /// All offsets are relative to "base" (the position of the leading smapLen word), and the last
    /// strip ends at smapLen (NOT the block size, which also covers the trailing z-planes).
    /// </summary>
    public class ScummV4ImageDecoder
    {
        /// <summary>Decodes the room background (BM block). Returns null when the room has no image.</summary>
        public Bitmap DecodeBackground(ScummV4RoomBlock room)
        {
            RoomHeader header = room.GetHD();
            ScummV4ImageBlock background = room.GetBM();
            if (header == null || background == null)
            {
                return null;
            }

            return Decode(background, header.Width, header.Height, room.IsEga, room.GetPA());
        }

        /// <summary>Decodes one object image (OI block), sized by its paired object code (OC) block.</summary>
        public Bitmap DecodeObject(ScummV4RoomBlock room, ScummV4ImageBlock objectImage, ObjectCode objectCode)
        {
            if (objectImage == null || objectCode == null || objectCode.Width == 0 || objectCode.Height == 0)
            {
                return null;
            }

            return Decode(objectImage, objectCode.Width, objectCode.Height, room.IsEga, room.GetPA());
        }

        /// <summary>Number of z-planes (masks) embedded in the room background block.</summary>
        public int CountBackgroundZPlanes(ScummV4RoomBlock room)
        {
            RoomHeader header = room.GetHD();
            ScummV4ImageBlock background = room.GetBM();
            if (header == null || background == null || header.Width == 0) return 0;
            return background.GetZPlaneRegions(header.Width / 8, room.IsEga).Count;
        }

        /// <summary>Number of z-planes (masks) embedded in an object image block.</summary>
        public int CountObjectZPlanes(ScummV4RoomBlock room, ScummV4ImageBlock objectImage, ObjectCode objectCode)
        {
            if (objectImage == null || objectCode == null || objectCode.Width == 0) return 0;
            return objectImage.GetZPlaneRegions(objectCode.Width / 8, room.IsEga).Count;
        }

        /// <summary>Decodes a room-background z-plane mask to a black/white bitmap (black = masked).</summary>
        public Bitmap DecodeBackgroundZPlane(ScummV4RoomBlock room, int zPlaneIndex)
        {
            RoomHeader header = room.GetHD();
            ScummV4ImageBlock background = room.GetBM();
            if (header == null || background == null || header.Width == 0 || header.Height == 0) return null;
            return DecodeZPlane(background, header.Width, header.Height, room.IsEga, zPlaneIndex);
        }

        /// <summary>Decodes an object z-plane mask to a black/white bitmap (black = masked).</summary>
        public Bitmap DecodeObjectZPlane(ScummV4RoomBlock room, ScummV4ImageBlock objectImage, ObjectCode objectCode, int zPlaneIndex)
        {
            if (objectImage == null || objectCode == null || objectCode.Width == 0 || objectCode.Height == 0) return null;
            return DecodeZPlane(objectImage, objectCode.Width, objectCode.Height, room.IsEga, zPlaneIndex);
        }

        private Bitmap DecodeZPlane(ScummV4ImageBlock image, int width, int height, bool isEga, int zPlaneIndex)
        {
            int numStrips = width / 8;
            List<(int Start, int Length)> regions = image.GetZPlaneRegions(numStrips, isEga);
            if (zPlaneIndex < 0 || zPlaneIndex >= regions.Count)
            {
                return null;
            }

            List<ZPlaneStripData> strips = image.GetZPlaneStrips(regions[zPlaneIndex].Start, regions[zPlaneIndex].Length, numStrips);
            return new ZPlaneDecoder().Decode(strips, width, height);
        }

        private Bitmap Decode(ScummV4ImageBlock image, int width, int height, bool isEga, PaletteData palette)
        {
            if (width == 0 || height == 0)
            {
                return null;
            }

            Color[] paletteColors = isEga ? EgaColorTable.Colors256 : (palette != null ? palette.Colors : null);
            if (paletteColors == null)
            {
                return null; // a VGA room with no palette cannot be rendered
            }

            byte[] body = image.Contents;
            int baseIndex = image.StripTableStart;
            int numStrips = width / 8;

            // The strip table needs the leading length word plus one offset per strip. Some objects
            // declare a size but ship no pixels (their OI holds only the object id), and the
            // defensive container walk can surface stray blocks; in both cases there is no image.
            int entrySize = isEga ? 2 : 4;
            long tableEnd = (long)baseIndex + entrySize + (long)numStrips * entrySize;
            if (numStrips <= 0 || body.Length < tableEnd)
            {
                return null;
            }

            if (isEga)
            {
                return DecodeEga(body, baseIndex, numStrips, width, height, paletteColors);
            }

            List<StripData> strips = BuildVgaStrips(body, baseIndex, numStrips);
            if (strips == null)
            {
                return null;
            }
            return new ImageDecoder().Decode(strips, width, height, paletteColors, -1);
        }

        /// <summary>
        /// Builds the strip list of a VGA image so the existing per-strip codec machinery in
        /// ImageDecoder can render it. Each strip keeps its codec id and its bitstream bytes.
        /// Returns null when the offset table or any strip range falls outside the body (a stray
        /// or truncated block), so the caller can render nothing instead of crashing.
        /// </summary>
        private List<StripData> BuildVgaStrips(byte[] body, int baseIndex, int numStrips)
        {
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
                int dataLength = end - start - 1; // -1 removes the codec byte

                if (start < 0 || end > smapLen || dataLength < 0 || codecPosition + 1 + dataLength > body.Length)
                {
                    return null;
                }

                var data = new byte[dataLength];
                Array.Copy(body, codecPosition + 1, data, 0, dataLength);

                strips.Add(new StripData
                {
                    OffSet = (uint)start,
                    CodecId = body[codecPosition],
                    ImageData = data
                });
            }

            return strips;
        }

        private Bitmap DecodeEga(byte[] body, int baseIndex, int numStrips, int width, int height, Color[] paletteColors)
        {
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

            var indexMatrix = new byte[width, height];
            for (int n = 0; n < numStrips; n++)
            {
                int start = offsets[n];
                int end = (n < numStrips - 1) ? offsets[n + 1] : smapLen;
                if (start < 0 || end > smapLen || end < start)
                {
                    return null;
                }
                DecodeEgaStrip(body, baseIndex + start, baseIndex + end, indexMatrix, n * 8, height);
            }

            return IndexedImageHelper.FromIndexMatrix(indexMatrix, paletteColors, -1);
        }

        /// <summary>
        /// Decodes a list of raw EGA strips (no codec byte, as produced by ScummV4EgaStripEncoder)
        /// straight into an index matrix. Used to verify the EGA encoder round-trips losslessly.
        /// </summary>
        public byte[,] DecodeEgaStripsToMatrix(List<byte[]> rawStrips, int width, int height)
        {
            var indexMatrix = new byte[width, height];
            for (int n = 0; n < rawStrips.Count; n++)
            {
                byte[] strip = rawStrips[n];
                DecodeEgaStrip(strip, 0, strip.Length, indexMatrix, n * 8, height);
            }
            return indexMatrix;
        }

        /// <summary>
        /// Decodes one 8-pixel-wide EGA strip into the index matrix, column-major (y advances first,
        /// wrapping to the next column at the strip height). Port of ScummVM's Gdi::drawStripEGA: a
        /// byte-oriented RLE with three ops - literal run, vertical copy (repeat the pixel one column
        /// to the left), and dither (alternate two 4-bit colors). A run of 0 means the real run
        /// length is in the next byte (applies to all three ops).
        /// </summary>
        private void DecodeEgaStrip(byte[] body, int start, int end, byte[,] indexMatrix, int x0, int height)
        {
            int p = start;
            int x = 0;
            int y = 0;

            while (x < 8 && p < end)
            {
                byte color = body[p++];
                int run;

                if ((color & 0x80) != 0)
                {
                    run = color & 0x3F;

                    if ((color & 0x40) != 0)
                    {
                        // Dither: alternate two colors packed in the next byte (even -> high nibble).
                        byte colors = body[p++];
                        if (run == 0)
                        {
                            run = body[p++];
                        }
                        for (int z = 0; z < run; z++)
                        {
                            int nibble = ((z & 1) == 1) ? (colors & 0x0F) : (colors >> 4);
                            EmitEgaPixel(indexMatrix, x0, ref x, ref y, height, (byte)nibble);
                        }
                    }
                    else
                    {
                        // Vertical copy: repeat the pixel one column to the left, same row.
                        if (run == 0)
                        {
                            run = body[p++];
                        }
                        for (int z = 0; z < run; z++)
                        {
                            byte left = (x0 + x - 1 >= 0) ? indexMatrix[x0 + x - 1, y] : (byte)0;
                            EmitEgaPixel(indexMatrix, x0, ref x, ref y, height, left);
                        }
                    }
                }
                else
                {
                    // Literal run of a single color (the low nibble).
                    run = color >> 4;
                    if (run == 0)
                    {
                        run = body[p++];
                    }
                    byte index = (byte)(color & 0x0F);
                    for (int z = 0; z < run; z++)
                    {
                        EmitEgaPixel(indexMatrix, x0, ref x, ref y, height, index);
                    }
                }
            }
        }

        private void EmitEgaPixel(byte[,] indexMatrix, int x0, ref int x, ref int y, int height, byte value)
        {
            if (x < 8)
            {
                indexMatrix[x0 + x, y] = value;
            }

            y++;
            if (y >= height)
            {
                y = 0;
                x++;
            }
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
