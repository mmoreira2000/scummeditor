using System.Collections.Generic;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) sound. The external MUSDISK/VOXDISK .BUN bundles use the same
    /// LB83 + COMP structure as The Dig, but their blocks are mostly VIMA (IMA-ADPCM codecs 13 mono / 15
    /// stereo) - which the v7 port skipped. This verifies the COMI bundles parse, that VIMA is actually
    /// present, and that the bundle entries (including the VIMA ones) decode to valid PCM WAV.
    /// </summary>
    public class V8SoundTests
    {
        private readonly ITestOutputHelper _out;
        public V8SoundTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void BundleEntriesDecodeIncludingVima()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(info == null, "COMI (v8) not present");
            Skip.If(info.BundleFiles == null || info.BundleFiles.Count == 0, "no COMI .BUN bundles");

            int totalDecoded = 0, vimaSeen = 0;
            foreach (string path in info.BundleFiles)
            {
                var bundle = new ImuseBundleFile(path);
                bundle.EnsureParsed();
                if (!bundle.IsValid || bundle.Entries.Count == 0) continue;

                _out.WriteLine("{0}: {1} entries", System.IO.Path.GetFileName(path), bundle.Entries.Count);

                int sampled = 0;
                for (int i = 0; i < bundle.Entries.Count && sampled < 6; i++)
                {
                    byte[] raw = bundle.ReadEntryRaw(i);
                    if (raw == null) continue;
                    if (UsesVima(raw)) vimaSeen++;

                    byte[] wav = ImuseBundleDecoder.ToWav(raw);
                    Assert.NotNull(wav);
                    Assert.True(wav.Length > 44 && wav[0] == 'R' && wav[1] == 'I' && wav[2] == 'F' && wav[3] == 'F',
                        "bundle entry did not decode to a valid WAV: " + bundle.Entries[i].Name);
                    totalDecoded++;
                    sampled++;
                }
            }

            _out.WriteLine("v8 bundle entries decoded: {0} ({1} used VIMA)", totalDecoded, vimaSeen);
            Assert.True(totalDecoded > 0, "no v8 bundle entries decoded");
            Assert.True(vimaSeen > 0, "no VIMA (codec 13/15) block seen - the COMI VIMA path was not exercised");
        }

        /// <summary>True if the entry's COMP table contains a VIMA block (codec 13 or 15).</summary>
        private static bool UsesVima(byte[] comp)
        {
            if (comp == null || comp.Length < 16) return false;
            if (!(comp[0] == 'C' && comp[1] == 'O' && comp[2] == 'M' && comp[3] == 'P')) return false;
            int numBlocks = ReadBE(comp, 4);
            int tableStart = 16;
            for (int i = 0; i < numBlocks; i++)
            {
                int rec = tableStart + i * 16;
                if (rec + 12 > comp.Length) break;
                int codec = ReadBE(comp, rec + 8);
                if (codec == 13 || codec == 15) return true;
            }
            return false;
        }

        private static int ReadBE(byte[] b, int o)
        {
            return (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];
        }
    }
}
