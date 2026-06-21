using System;
using ScummEditor.Engine.Encoders;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// The SCUMM v2 accent charmap (GameTextCodecV12): a translation maps accented letters onto reused
    /// punctuation slots (redrawn in the EXE font), so the codec must decode those slot bytes as accents
    /// and encode the accents back to the same bytes. The map is serialized to the export "; charmap:" line.
    /// </summary>
    public class V2AccentCharmapTests
    {
        [Fact]
        public void PortugueseAccentsRoundTripThroughTheSlotBytes()
        {
            GameTextCodecV12 codec = GameTextCodecV12.Portuguese();
            // 'H'(0x48) + á-slot(0x7E) + ç-slot(0x5C) + 'o'(0x6F)
            byte[] bytes = { 0x48, 0x7E, 0x5C, 0x6F };

            string decoded = codec.Decode(bytes, 0, bytes.Length);
            Assert.Equal("Háço", decoded);

            string error;
            byte[] reencoded = codec.Encode(decoded, out error);
            Assert.Null(error);
            Assert.Equal(bytes, reencoded);
        }

        [Fact]
        public void DefaultCodecHasNoAccentMap()
        {
            GameTextCodecV12 codec = GameTextCodecV12.Default();
            // No remapping: 0x7E decodes to the literal '~', and plain ASCII still encodes.
            Assert.Equal("~", codec.Decode(new byte[] { 0x7E }, 0, 1));
            string error;
            Assert.Equal(new byte[] { (byte)'H', (byte)'i' }, codec.Encode("Hi", out error));
            Assert.Null(error);
        }

        [Fact]
        public void DefaultCodecCannotEncodeAnAccent()
        {
            GameTextCodecV12 codec = GameTextCodecV12.Default();
            string error;
            byte[] result = codec.Encode("ação", out error);
            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public void AccentSpecSerializationRoundTrips()
        {
            string spec = GameTextCodecV12.Portuguese().ToAccentSpec();
            Assert.Contains("á=0x7E", spec);
            Assert.Contains("ç=0x5C", spec);

            // Rebuilding from the serialized spec gives the same behavior.
            GameTextCodecV12 rebuilt = GameTextCodecV12.FromAccentSpec(spec);
            string error;
            Assert.Equal(new byte[] { 0x7E, 0x5C }, rebuilt.Encode("áç", out error));
            Assert.Null(error);
        }

        [Theory]
        [InlineData("a=0x41")]   // maps an ASCII character (not an accent)
        [InlineData("á=0x41")]   // slot 0x41 = 'A', a real letter
        [InlineData("á=0x39")]   // slot 0x39 = '9', a digit
        [InlineData("á=0x10")]   // slot below the printable range
        [InlineData("á=0x7E ç=0x7E")] // duplicated slot
        [InlineData("á=0x7E á=0x5C")] // duplicated character
        public void FromAccentSpecRejectsInvalidMaps(string spec)
        {
            Assert.Throws<FormatException>(() => GameTextCodecV12.FromAccentSpec(spec));
        }

        [Fact]
        public void BlankAccentSpecGivesAPlainCodec()
        {
            GameTextCodecV12 codec = GameTextCodecV12.FromAccentSpec("");
            Assert.Equal("~", codec.Decode(new byte[] { 0x7E }, 0, 1)); // no remapping
        }
    }
}
