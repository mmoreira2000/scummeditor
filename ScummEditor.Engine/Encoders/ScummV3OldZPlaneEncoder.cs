using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited walk-behind (z-plane) mask of a SCUMM v3 "old bundle" room (Loom EGA,
    /// Indy3 EGA) into the GF_OLD_BUNDLE z-plane layout, the inverse of
    /// ScummV3OldImageDecoder.DecodeBackgroundZPlane / DecodeObjectZPlane.
    ///
    /// The region is [numStrips x offset:u16][strip mask data...], with each offset relative to the
    /// region start (base +0 - no leading length word, unlike v4 / v3small) and 0 marking an empty
    /// (fully unmasked) strip. The per-strip mask RLE is identical to v4, so the strip bytes are
    /// produced by the shared ScummV4ImageEncoder.EncodeMaskStrip. The caller splices the returned
    /// bytes over the old z-plane region [zbase, regionEnd) and runs ScummV3OldWriter.ApplyEdit to
    /// re-point every offset the size change shifts.
    /// </summary>
    public static class ScummV3OldZPlaneEncoder
    {
        /// <summary>
        /// Builds the new z-plane region bytes for an image of the given size, given the edited mask
        /// (black = masked). The mask must be exactly the image size.
        /// </summary>
        public static byte[] Encode(int width, int height, Bitmap mask)
        {
            int numStrips = width / 8;
            var strips = new List<byte[]>(numStrips);
            for (int n = 0; n < numStrips; n++)
            {
                strips.Add(EncodeStrip(mask, n * 8, height));
            }
            return BuildRegion(strips);
        }

        /// <summary>
        /// Encodes one 8-pixel-wide strip, or returns an empty array when the strip has no masked pixel
        /// (so it is written as an offset-0 / empty strip - matching how the games store unmasked strips).
        /// </summary>
        private static byte[] EncodeStrip(Bitmap mask, int x0, int height)
        {
            bool anyMasked = false;
            for (int y = 0; y < height && !anyMasked; y++)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (ScummV4ImageEncoder.IsMasked(mask.GetPixel(x0 + i, y)))
                    {
                        anyMasked = true;
                        break;
                    }
                }
            }
            return anyMasked ? ScummV4ImageEncoder.EncodeMaskStrip(mask, x0, height) : new byte[0];
        }

        /// <summary>Assembles [numStrips x offset:u16][strips]; offset 0 for an empty strip, else relative to the region start.</summary>
        private static byte[] BuildRegion(List<byte[]> strips)
        {
            int offsetTableSize = strips.Count * 2;
            int dataSize = 0;
            foreach (byte[] s in strips) dataSize += s.Length;

            var result = new byte[offsetTableSize + dataSize];
            int running = offsetTableSize;
            for (int n = 0; n < strips.Count; n++)
            {
                if (strips[n].Length == 0)
                {
                    WriteU16(result, n * 2, 0); // empty strip
                    continue;
                }
                WriteU16(result, n * 2, running);
                System.Array.Copy(strips[n], 0, result, running, strips[n].Length);
                running += strips[n].Length;
            }
            return result;
        }

        private static void WriteU16(byte[] data, int p, int value)
        {
            data[p] = (byte)(value & 0xFF);
            data[p + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
