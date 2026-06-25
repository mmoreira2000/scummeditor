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
