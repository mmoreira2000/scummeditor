using System.Collections.Generic;
using System.Linq;
using ScummEditor.Encoders;
using ScummEditor.Structures;
using ScummEditor.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// v4 sound handling: SoundBlockV4.GetPayload (the sub-block slice extracted to the engine in
    /// Stage 2c) must return exactly the bytes after the 6-byte header, and the AdLib-music payloads
    /// must convert to a structurally valid Standard MIDI file (the melody-preview path).
    /// </summary>
    public class V4SoundTests
    {
        private static IEnumerable<SoundSubBlockV4> Flatten(List<SoundSubBlockV4> subs)
        {
            if (subs == null) yield break;
            foreach (SoundSubBlockV4 sub in subs)
            {
                yield return sub;
                if (sub.Children != null)
                {
                    foreach (SoundSubBlockV4 child in Flatten(sub.Children)) yield return child;
                }
            }
        }

        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1FloppyVga)]
        [InlineData(GameLibrary.Loom)]
        public void GetPayloadSlicesCorrectlyAndAdLibMusicConvertsToValidMidi(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.NotNull(game);

            List<SoundBlockV4> sounds = GameLibrary.AllBlocks(game).OfType<SoundBlockV4>().ToList();
            Assert.NotEmpty(sounds);

            int adMusic = 0, validMidi = 0;
            foreach (SoundBlockV4 so in sounds)
            {
                foreach (SoundSubBlockV4 sub in Flatten(so.SubBlocks))
                {
                    int start = sub.Offset + 6;
                    int length = sub.Size - 6;
                    if (start < 0 || length <= 0 || start + length > so.RawContent.Length) continue;

                    byte[] payload = so.GetPayload(sub);

                    // The payload is exactly the post-header slice of the parent's RawContent.
                    Assert.Equal(length, payload.Length);
                    Assert.Equal(so.RawContent[start], payload[0]);
                    Assert.Equal(so.RawContent[start + length - 1], payload[length - 1]);

                    // AdLib music (type marker 0x80) must yield a valid SMF.
                    if (sub.Tag == "AD" && start + 2 < so.RawContent.Length && so.RawContent[start + 2] == 0x80)
                    {
                        adMusic++;
                        byte[] midi = ScummV4AdLibMidi.ToStandardMidi(payload);
                        Assert.NotNull(midi);
                        Assert.True(midi.Length > 14, "MIDI too short");
                        Assert.Equal((byte)'M', midi[0]);
                        Assert.Equal((byte)'T', midi[1]);
                        Assert.Equal((byte)'h', midi[2]);
                        Assert.Equal((byte)'d', midi[3]);
                        validMidi++;
                    }
                }
            }

            Assert.True(adMusic > 0, "expected at least one AdLib music block in " + relativePath);
            Assert.Equal(adMusic, validMidi);
        }
    }
}
