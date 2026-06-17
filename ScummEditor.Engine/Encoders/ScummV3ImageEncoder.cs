using System.Collections.Generic;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited bitmap back into a SCUMM v3 "GF_OLD256" image block (Indy3 VGA, Zak/Indy3
    /// FM-Towns). These rooms store a v4-style VGA strip table (smapLen:LE32, then numStrips x LE32
    /// offsets, then per-strip a codec byte + data), so the block rebuild and z-plane preservation are
    /// identical to v4 - the only difference is the per-strip codec set. v3 uses the FM-Towns codecs
    /// 1/2/3/4/7 (raw256 + unkDecode8..11), which the v4/v5 ImageEncoder does not produce, so every
    /// CHANGED strip is re-encoded as codec 1 (raw256): a column-major dump of the 8xheight pixels,
    /// the exact inverse of <see cref="FmTownsStripDecoder"/>.Raw256.
    ///
    /// Unchanged columns are still reused verbatim by the base class (keeping their original codec),
    /// so an untouched image round-trips byte-for-byte and only edited columns switch to raw256 - the
    /// engine accepts a per-strip mix of codecs, so this is lossless without having to invert
    /// unkDecode8..11.
    /// </summary>
    public class ScummV3ImageEncoder : ScummV4ImageEncoder
    {
        protected override List<StripData> EncodeVgaStrips(byte[,] indexMatrix, int width, int height, List<StripData> originalStrips)
        {
            int numStrips = width / 8;
            var strips = new List<StripData>(numStrips);
            for (int n = 0; n < numStrips; n++)
            {
                strips.Add(EncodeRaw256Strip(indexMatrix, n * 8, height));
            }
            return strips;
        }

        /// <summary>
        /// Encodes one 8-pixel-wide strip as codec 1 (raw256): the 8xheight palette indexes written
        /// column-major (top-to-bottom, then the next column), which FmTownsStripDecoder.Raw256 reads
        /// back one byte per pixel.
        /// </summary>
        private static StripData EncodeRaw256Strip(byte[,] indexMatrix, int x0, int height)
        {
            var data = new byte[8 * height];
            int p = 0;
            for (int col = 0; col < 8; col++)
            {
                for (int row = 0; row < height; row++)
                {
                    data[p++] = indexMatrix[x0 + col, row];
                }
            }
            return new StripData { CodecId = 1, ImageData = data };
        }
    }
}
