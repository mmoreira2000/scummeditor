using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 external iMUSE sound bundles (The Dig's DIGMUSIC.BUN / DIGVOICE.BUN). The bundles are
    /// "LB83" directories of COMP-compressed iMUS resources (codecs 0-12, no VIMA). ImuseBundleFile parses
    /// the directory; ImuseBundleDecoder decompresses an entry (ImuseBundleCodecs) into its iMUS resource,
    /// which ImuseAudioDecoder turns into WAV. These tests run on The Dig's real bundles.
    /// </summary>
    public class V7BundleTests
    {
        [SkippableFact]
        public void TheDigDetectsTwoBundlesFullThrottleNone()
        {
            GameInfo dig = GameLibrary.Detect(GameLibrary.TheDig);
            Skip.If(dig == null, "The Dig not present");
            Assert.NotNull(dig.BundleFiles);
            Assert.Equal(2, dig.BundleFiles.Count); // DIGMUSIC.BUN + DIGVOICE.BUN

            GameInfo ft = GameLibrary.Detect(GameLibrary.FullThrottle);
            if (ft != null)
            {
                Assert.True(ft.BundleFiles == null || ft.BundleFiles.Count == 0, "Full Throttle should have no .BUN");
            }
        }

        [SkippableFact]
        public void DigVoiceBundleDecodesToWav()
        {
            DecodeSample("DIGVOICE.BUN", 30); // codecs 0-3 (raw / LZ77 / delta)
        }

        [SkippableFact]
        public void DigMusicBundleDecodesToWav()
        {
            DecodeSample("DIGMUSIC.BUN", 3); // codecs 4-12 (delta + 12-bit repack); entries are large, so a few
        }

        private static void DecodeSample(string bundleName, int count)
        {
            string folder = GameLibrary.Folder(GameLibrary.TheDig);
            Skip.If(folder == null, "The Dig not present");
            string path = Path.Combine(folder, bundleName);
            Skip.If(!File.Exists(path), bundleName + " not present");

            var bundle = new ImuseBundleFile(path);
            bundle.EnsureParsed();
            Assert.True(bundle.IsValid, "bundle did not parse");
            Assert.True(bundle.Entries.Count > 0, "bundle has no entries");

            int decoded = 0;
            int n = System.Math.Min(count, bundle.Entries.Count);
            for (int i = 0; i < n; i++)
            {
                byte[] raw = bundle.ReadEntryRaw(i);
                Assert.NotNull(raw);

                byte[] imus = ImuseBundleDecoder.DecodeToImus(raw);
                Assert.NotNull(imus);
                // the decompressed entry must be a well-formed iMUS resource
                Assert.True(Find(imus, "iMUS") == 0, "entry " + i + " did not start with iMUS");
                Assert.True(Find(imus, "FRMT") > 0, "entry " + i + " has no FRMT");
                Assert.True(Find(imus, "DATA") > 0, "entry " + i + " has no DATA");

                byte[] wav = ImuseBundleDecoder.ToWav(raw);
                Assert.NotNull(wav);
                Assert.True(wav.Length > 44 && wav[0] == 'R' && wav[1] == 'I' && wav[2] == 'F' && wav[3] == 'F',
                    "entry " + i + " did not decode to a RIFF/WAVE");
                decoded++;
            }
            Assert.True(decoded > 0, "no entries decoded from " + bundleName);
        }

        [Fact]
        public void MalformedInputDecodesGracefully()
        {
            // None of these may throw; clearly-bad input must return null (entry shown as undecodable).
            Assert.Null(ImuseBundleDecoder.DecodeToImus(null));
            Assert.Null(ImuseBundleDecoder.DecodeToImus(new byte[] { 1, 2, 3 }));               // too short
            Assert.Null(ImuseBundleDecoder.DecodeToImus(System.Text.Encoding.ASCII.GetBytes("XXXXjunkjunkjunk"))); // unknown tag

            // A COMP whose single block declares an unsupported codec (14 is not in 0-12/13/15) -> null, no
            // throw. (Codecs 13/15 = VIMA are now supported for COMI, so they decode rather than return null.)
            Assert.Null(ImuseBundleDecoder.DecodeToImus(BuildComp(1, 0, 16, 4, 14)));
            // A COMP whose block points past the buffer -> null.
            Assert.Null(ImuseBundleDecoder.DecodeToImus(BuildComp(1, 0, 16, 100000, 1)));
            // A COMP claiming more blocks than the table holds -> null.
            Assert.Null(ImuseBundleDecoder.DecodeToImus(BuildComp(50, 0, 16, 4, 1)));

            // A COMP with a tiny codec-4 block (degenerate repack): must not throw (may return non-iMUS bytes,
            // which ToWav then rejects as null).
            byte[] tiny = BuildComp(1, 0, 16, 4, 4);
            byte[] imus = ImuseBundleDecoder.DecodeToImus(tiny); // no exception
            Assert.Null(ImuseBundleDecoder.ToWav(tiny));          // garbage -> not a valid iMUS/WAV
        }

        /// <summary>Builds a minimal COMP chunk with one block record for the robustness tests.</summary>
        private static byte[] BuildComp(int numBlocks, int lastBlockSize, int blockOffset, int blockSize, int codec)
        {
            var ms = new MemoryStream();
            ms.Write(System.Text.Encoding.ASCII.GetBytes("COMP"), 0, 4);
            WriteBE(ms, numBlocks);
            WriteBE(ms, 0);              // padding
            WriteBE(ms, lastBlockSize);
            WriteBE(ms, blockOffset);    // one block record: offset / size / codec / pad
            WriteBE(ms, blockSize);
            WriteBE(ms, codec);
            WriteBE(ms, 0);
            ms.Write(new byte[8], 0, 8); // a little payload so a small in-bounds block has bytes to read
            return ms.ToArray();
        }

        private static void WriteBE(Stream s, int v)
        {
            s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v);
        }

        private static int Find(byte[] data, string tag)
        {
            for (int i = 0; i + 4 <= data.Length; i++)
            {
                if (data[i] == tag[0] && data[i + 1] == tag[1] && data[i + 2] == tag[2] && data[i + 3] == tag[3])
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
