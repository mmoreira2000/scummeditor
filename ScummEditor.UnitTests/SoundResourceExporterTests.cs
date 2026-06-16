using System.Text;
using ScummEditor.Engine.Encoders;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SoundResourceExporter.GetExportBytes picks the most useful representation for export. This is
    /// the logic that moved out of SoundBlockControl in Stage 2c; it must match the old GUI choice.
    /// </summary>
    public class SoundResourceExporterTests
    {
        [Fact]
        public void MidiExportsAsExtractedMid()
        {
            byte[] data = new byte[] { (byte)'M', (byte)'T', (byte)'h', (byte)'d', 0, 0, 0, 6, 0, 0, 0, 1, 0, 96 };

            byte[] outBytes;
            string ext;
            SoundResourceExporter.GetExportBytes(data, out outBytes, out ext);

            Assert.Equal(".mid", ext);
            Assert.Equal(SoundConverter.ExtractMidi(data), outBytes);
        }

        [Fact]
        public void RawDataExportsAsBinUnchanged()
        {
            byte[] data = new byte[] { 0x10, 0x20, 0x30, 0x40 };

            byte[] outBytes;
            string ext;
            SoundResourceExporter.GetExportBytes(data, out outBytes, out ext);

            Assert.Equal(".bin", ext);
            Assert.Same(data, outBytes); // raw export returns the same array, no copy
        }

        [Fact]
        public void VocExportsAsWavOrRawVoc()
        {
            byte[] sig = Encoding.ASCII.GetBytes("Creative Voice File\x1A");
            var data = new byte[32];
            System.Array.Copy(sig, data, sig.Length);
            data[20] = 26;

            byte[] outBytes;
            string ext;
            SoundResourceExporter.GetExportBytes(data, out outBytes, out ext);

            // A decodable VOC becomes .wav; an unsupported codec falls back to the raw .voc.
            bool decodable = SoundConverter.VocToWav(data) != null;
            Assert.Equal(decodable ? ".wav" : ".voc", ext);
            Assert.NotNull(outBytes);
        }
    }
}
