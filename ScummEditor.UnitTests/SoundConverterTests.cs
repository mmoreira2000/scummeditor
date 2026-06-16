using System.Text;
using ScummEditor.Encoders;
using Xunit;

namespace ScummEditor.UnitTests
{
    public class SoundConverterTests
    {
        private static byte[] MinimalMidi()
        {
            // "MThd" + a 6-byte header chunk (format 0, 1 track, 96 ticks/quarter).
            return new byte[] { (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6, 0, 0, 0, 1, 0, 96 };
        }

        private static byte[] MinimalVoc()
        {
            // "Creative Voice File" signature padded out past the 26-byte minimum header.
            byte[] sig = Encoding.ASCII.GetBytes("Creative Voice File\x1A");
            var voc = new byte[32];
            System.Array.Copy(sig, voc, sig.Length);
            voc[20] = 26; // header size / first-block offset
            return voc;
        }

        [Fact]
        public void ClassifiesStandardMidi()
        {
            Assert.Equal(SoundConverter.SoundKind.StandardMidi, SoundConverter.Classify(MinimalMidi()));
        }

        [Fact]
        public void ClassifiesMidiEvenBehindAPrefix()
        {
            // SCUMM wraps MIDI behind a small header; Classify searches for "MThd".
            byte[] wrapped = new byte[] { 0xAA, 0xBB, 0xCC };
            byte[] midi = MinimalMidi();
            var combined = new byte[wrapped.Length + midi.Length];
            System.Array.Copy(wrapped, combined, wrapped.Length);
            System.Array.Copy(midi, 0, combined, wrapped.Length, midi.Length);

            Assert.Equal(SoundConverter.SoundKind.StandardMidi, SoundConverter.Classify(combined));
        }

        [Fact]
        public void ClassifiesVoc()
        {
            Assert.Equal(SoundConverter.SoundKind.Voc, SoundConverter.Classify(MinimalVoc()));
        }

        [Theory]
        [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
        [InlineData(new byte[0])]
        public void ClassifiesEverythingElseAsUnknown(byte[] data)
        {
            Assert.Equal(SoundConverter.SoundKind.Unknown, SoundConverter.Classify(data));
        }

        [Fact]
        public void ExtractMidiReturnsBytesFromTheSignature()
        {
            byte[] midi = MinimalMidi();
            byte[] wrapped = new byte[] { 0x01, 0x02 };
            var combined = new byte[wrapped.Length + midi.Length];
            System.Array.Copy(wrapped, combined, wrapped.Length);
            System.Array.Copy(midi, 0, combined, wrapped.Length, midi.Length);

            byte[] extracted = SoundConverter.ExtractMidi(combined);

            Assert.Equal(midi, extracted);
        }

        [Fact]
        public void ExtractMidiReturnsNullWhenAbsent()
        {
            Assert.Null(SoundConverter.ExtractMidi(new byte[] { 1, 2, 3, 4 }));
        }

        [Fact]
        public void SuggestExtensionFollowsClassification()
        {
            Assert.Equal(".mid", SoundConverter.SuggestExtension(MinimalMidi()));
            Assert.Equal(".voc", SoundConverter.SuggestExtension(MinimalVoc()));
            Assert.Equal(".bin", SoundConverter.SuggestExtension(new byte[] { 9, 9, 9 }));
        }
    }
}
