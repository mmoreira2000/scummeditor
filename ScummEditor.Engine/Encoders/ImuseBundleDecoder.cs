using System.IO;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Turns a raw iMUSE-bundle entry (a COMP-compressed chunk, or a plain iMUS resource) into its
    /// decompressed iMUS resource, and from there into a PCM WAV. The COMP chunk holds a table of blocks,
    /// each compressed with one of codecs 0-12 (ImuseBundleCodecs); concatenating the decompressed blocks
    /// rebuilds the iMUS resource, which ImuseAudioDecoder converts to WAV. Mirrors ScummVM
    /// dimuse_bndmgr.cpp loadCompTable/readFile (block offsets are relative to the entry start).
    /// </summary>
    public static class ImuseBundleDecoder
    {
        private const int BundleChunkSize = 0x2000; // DIMUSE_BUN_CHUNK_SIZE: each block decompresses to <= this

        /// <summary>
        /// Decompresses a bundle entry's COMP chunk to its full iMUS resource bytes, or returns the bytes
        /// unchanged when the entry is already a plain iMUS. Returns null when it is neither, or when a
        /// block uses an unsupported codec (VIMA 13/15).
        /// </summary>
        public static byte[] DecodeToImus(byte[] comp)
        {
            if (comp == null || comp.Length < 4) return null;

            string tag = Tag(comp, 0);
            if (tag == "iMUS")
            {
                return comp; // uncompressed entry
            }
            if (tag != "COMP" || comp.Length < 16)
            {
                return null;
            }

            int numBlocks = ReadBE(comp, 4);
            // comp[8..11] padding, comp[12..15] last-block decompressed size (unused for a full extraction).
            if (numBlocks <= 0) return null;

            int tableStart = 16;
            if (tableStart + numBlocks * 16 > comp.Length) return null;

            var imus = new MemoryStream();
            var output = new byte[BundleChunkSize + 16]; // a little slack over the chunk size
            var blockInput = new byte[2];

            try
            {
                for (int i = 0; i < numBlocks; i++)
                {
                    int rec = tableStart + i * 16;
                    int blockOffset = ReadBE(comp, rec);
                    int blockSize = ReadBE(comp, rec + 4);
                    int codec = ReadBE(comp, rec + 8);
                    if (!ImuseBundleCodecs.CanDecode(codec)) return null; // VIMA / unknown
                    if (blockOffset < 0 || blockSize < 0 || blockOffset + blockSize > comp.Length) return null;

                    // Copy the block to its own buffer with TWO trailing zero bytes: the LZ77 reader refills
                    // its 16-bit mask by reading two bytes, so a single guard byte (the ScummVM CMI trick)
                    // could still over-read by one at the very end.
                    if (blockInput.Length < blockSize + 2) blockInput = new byte[blockSize + 2];
                    System.Array.Copy(comp, blockOffset, blockInput, 0, blockSize);
                    blockInput[blockSize] = 0;
                    blockInput[blockSize + 1] = 0;

                    int outSize = ImuseBundleCodecs.DecompressCodec(codec, blockInput, blockSize, output);
                    if (outSize > 0) imus.Write(output, 0, outSize);
                }
            }
            catch (System.Exception)
            {
                // A malformed/truncated COMP block can still run a codec off the end; treat the whole entry
                // as undecodable rather than crashing the viewer (real Dig/FT bundles never hit this).
                return null;
            }

            return imus.ToArray();
        }

        /// <summary>Decompresses a bundle entry and decodes its iMUS resource to a PCM WAV (null if it cannot).</summary>
        public static byte[] ToWav(byte[] comp)
        {
            byte[] imus = DecodeToImus(comp);
            return imus == null ? null : ImuseAudioDecoder.ToWav(imus);
        }

        private static string Tag(byte[] b, int o)
        {
            if (o + 4 > b.Length) return string.Empty;
            return string.Concat((char)b[o], (char)b[o + 1], (char)b[o + 2], (char)b[o + 3]);
        }

        private static int ReadBE(byte[] b, int o)
        {
            return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        }
    }
}
