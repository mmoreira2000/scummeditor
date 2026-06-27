using System.Collections.Generic;
using System.IO;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v7 in-container sound (SOUN). v7 SOUN blocks are typed as SoundBlockV7 (a byte-exact
    /// RawContainerBlock marker) so the GUI can decode them: The Dig wraps an iMUS digital-audio resource
    /// (MAP/FRMT/DATA, 8/12-bit PCM) decoded by ImuseAudioDecoder; Full Throttle stores a Creative Voice
    /// File (VOC) decoded by the existing SoundConverter.VocToWav. These tests run on the real games.
    /// </summary>
    public class V7SoundTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.TheDig)]
        [InlineData(GameLibrary.FullThrottle)]
        public void SounBlocksAreTypedAsSoundBlockV7(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            List<SoundBlockV7> sounds = CollectSounds(game);
            Assert.True(sounds.Count > 0, "no SOUN blocks found (SOUN not typed as SoundBlockV7?)");
        }

        [SkippableFact]
        public void TheDigImusSoundsDecodeToWav()
        {
            Skip.If(GameLibrary.Folder(GameLibrary.TheDig) == null, "The Dig not present");

            ScummGameData game = GameLibrary.Load(GameLibrary.TheDig);
            int decoded = 0, checkedCount = 0;
            foreach (SoundBlockV7 s in CollectSounds(game))
            {
                byte[] body = Serialize(s);
                if (!ImuseAudioDecoder.IsImus(body)) continue;

                ImuseAudioDecoder.ImuseInfo info = ImuseAudioDecoder.GetInfo(body);
                Assert.NotNull(info);
                Assert.Contains(info.WordSize, new[] { 8, 12, 16 });
                Assert.InRange(info.Channels, 1, 2);
                Assert.True(info.SampleRate == 11025 || info.SampleRate == 22050, "unexpected rate " + info.SampleRate);

                byte[] wav = ImuseAudioDecoder.ToWav(body);
                Assert.NotNull(wav);
                AssertValidWav(wav, info.Channels, info.SampleRate, info.WordSize == 8 ? 8 : 16);
                decoded++;

                if (++checkedCount >= 40) break; // a representative sample keeps the test fast
            }
            Assert.True(decoded > 0, "no iMUS sounds decoded");
        }

        [SkippableFact]
        public void FullThrottleVocSoundsDecodeToWav()
        {
            Skip.If(GameLibrary.Folder(GameLibrary.FullThrottle) == null, "Full Throttle not present");

            ScummGameData game = GameLibrary.Load(GameLibrary.FullThrottle);
            int decoded = 0, checkedCount = 0;
            foreach (SoundBlockV7 s in CollectSounds(game))
            {
                byte[] body = Serialize(s);
                int voc = IndexOf(body, "Creative Voice File");
                if (voc < 0) continue;

                byte[] slice = new byte[body.Length - voc];
                System.Array.Copy(body, voc, slice, 0, slice.Length);
                byte[] wav = SoundConverter.VocToWav(slice);
                // VocToWav returns null only for an unsupported codec (e.g. ADPCM); FT speech is PCM.
                if (wav != null)
                {
                    Assert.True(wav.Length > 44 && wav[0] == 'R' && wav[1] == 'I' && wav[2] == 'F' && wav[3] == 'F',
                        "VOC did not decode to a RIFF/WAVE");
                    decoded++;
                }

                if (++checkedCount >= 40) break;
            }
            Assert.True(decoded > 0, "no VOC sounds decoded");
        }

        [SkippableFact]
        public void FullThrottleExposesMonsterSouSpeech()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.FullThrottle);
            Skip.If(info == null, "Full Throttle not present");

            // FT's recorded speech is an external MONSTER.SOU (Creative VOC); the existing speech viewer
            // shows/plays/exports it once detection points SpeechFilePath at it.
            Assert.NotNull(info.SpeechFilePath);
            Assert.EndsWith("MONSTER.SOU", info.SpeechFilePath, System.StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(info.SpeechFilePath), "MONSTER.SOU not found");
        }

        [SkippableTheory]
        [InlineData(GameLibrary.FullThrottle)]        // v7: each entry is VCTL + VTLK-wrapped VOC
        [InlineData(GameLibrary.DayOfTheTentacleCd)]  // v6: VCTL + bare VOC (no VTLK wrapper)
        [InlineData(GameLibrary.SamAndMaxCd)]         // v6: VCTL + bare VOC
        public void MonsterSouParsesEverySpeechEntry(string relativePath)
        {
            string folder = GameLibrary.Folder(relativePath);
            Skip.If(folder == null, "GameData folder not present: " + relativePath);
            string sou = Path.Combine(folder, "MONSTER.SOU");
            Skip.If(!File.Exists(sou), "MONSTER.SOU not present: " + relativePath);

            var speech = new SpeechSouFile(sou);
            speech.EnsureParsed();

            // The walk must reach the end of the file (Full Throttle wraps the VOC in a VTLK block that the
            // parser must skip; the older talkie editions do not) and expose every speech entry.
            Assert.Null(speech.ParseError);
            Assert.True(speech.Entries.Count > 100, "too few speech entries parsed: " + speech.Entries.Count);

            // Spot-check the first entry: it points at a real Creative VOC and yielded a sample rate.
            SpeechSouEntry first = speech.Entries[0];
            byte[] voc = speech.ReadVocBytes(first);
            Assert.True(voc.Length > 26, "VOC too short");
            Assert.Equal("Creative Voice File", System.Text.Encoding.ASCII.GetString(voc, 0, 19));
            Assert.True(first.SampleRate > 0, "no sample rate decoded");
        }

        [SkippableFact]
        public void TheDigHasNoMonsterSou()
        {
            GameInfo info = GameLibrary.Detect(GameLibrary.TheDig);
            Skip.If(info == null, "The Dig not present");

            // The Dig keeps its voice in DIGVOICE.BUN, not a MONSTER.SOU, so no speech file is exposed.
            Assert.Null(info.SpeechFilePath);
        }

        private static List<SoundBlockV7> CollectSounds(ScummGameData game)
        {
            var list = new List<SoundBlockV7>();
            Walk((BlockBase)game.DataFile, list);
            return list;
        }

        private static void Walk(BlockBase node, List<SoundBlockV7> outList)
        {
            if (node is SoundBlockV7 s) outList.Add(s);
            foreach (BlockBase c in node.Childrens) Walk(c, outList);
        }

        private static byte[] Serialize(BlockBase block)
        {
            using (var ms = new MemoryStream())
            {
                block.SaveToBinaryWriter(ms);
                return ms.ToArray();
            }
        }

        private static void AssertValidWav(byte[] wav, int channels, int sampleRate, int bits)
        {
            Assert.True(wav.Length > 44, "WAV too short");
            Assert.True(wav[0] == 'R' && wav[1] == 'I' && wav[2] == 'F' && wav[3] == 'F', "no RIFF");
            Assert.True(wav[8] == 'W' && wav[9] == 'A' && wav[10] == 'V' && wav[11] == 'E', "no WAVE");
            Assert.Equal(channels, wav[22] | (wav[23] << 8));            // fmt channels
            Assert.Equal(sampleRate, wav[24] | (wav[25] << 8) | (wav[26] << 16) | (wav[27] << 24));
            Assert.Equal(bits, wav[34] | (wav[35] << 8));                // bits per sample
        }

        private static int IndexOf(byte[] data, string text)
        {
            for (int i = 0; i + text.Length <= data.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < text.Length; j++)
                {
                    if (data[i + j] != text[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
