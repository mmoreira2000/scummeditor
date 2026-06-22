using ScummEditor.Engine.Encoders;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Regression for the 0xFE-escape bug: 0xFE is a string escape ONLY for v3 old-bundle games. For
    /// v4/v5/v6 it is ordinary content (a legal SJIS trail byte / CJK newline glyph in Japanese releases),
    /// so the codec must NOT consume it as an escape there - doing so desynced from the disassembler and
    /// corrupted Japanese text. GameTextCodec.FeEscape controls this.
    /// </summary>
    public class GameTextCodecFeEscapeTests
    {
        /// <summary>v4-v6 (FeEscape=false): a string with 0xFE content bytes round-trips byte-exact, no fe- tokens.</summary>
        [Fact]
        public void FeEscapeFalse_TreatsFEAsContent_RoundTripsExact()
        {
            var codec = GameTextCodec.Default();
            codec.FeEscape = false;

            // 'A' 0xFE 0x01 0xFE 0xFE 'B' - 0xFE here is SJIS-like content, NOT an escape.
            byte[] original = { (byte)'A', 0xFE, 0x01, 0xFE, 0xFE, (byte)'B' };
            string display = codec.Decode(original, 0, original.Length);

            Assert.DoesNotContain("fe-", display); // 0xFE must NOT become an escape token

            string err;
            byte[] back = codec.Encode(display, out err);
            Assert.Null(err);
            Assert.Equal(original, back); // byte-exact round-trip (the bug consumed the byte after 0xFE)
        }

        /// <summary>v3 old-bundle (FeEscape=true): 0xFE introduces an escape, tokenised as {fe-...}, and round-trips.</summary>
        [Fact]
        public void FeEscapeTrue_TreatsFEAsEscape_RoundTrips()
        {
            var codec = GameTextCodec.Default();
            codec.FeEscape = true;

            byte[] original = { (byte)'A', 0xFE, 0x01, (byte)'B' }; // 0xFE 0x01 = the no-arg "br" escape
            string display = codec.Decode(original, 0, original.Length);

            Assert.Contains("{fe-br}", display);

            string err;
            byte[] back = codec.Encode(display, out err);
            Assert.Null(err);
            Assert.Equal(original, back);
        }

        /// <summary>0xFF is always an escape regardless of FeEscape (both modes agree on it).</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FFIsAlwaysAnEscape(bool feEscape)
        {
            var codec = GameTextCodec.Default();
            codec.FeEscape = feEscape;

            byte[] original = { (byte)'H', 0xFF, 0x01, (byte)'i' }; // 0xFF 0x01 = {br}
            string display = codec.Decode(original, 0, original.Length);
            Assert.Contains("{br}", display);

            string err;
            byte[] back = codec.Encode(display, out err);
            Assert.Null(err);
            Assert.Equal(original, back);
        }
    }
}
