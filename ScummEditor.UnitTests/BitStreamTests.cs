using ScummEditor.Engine;
using Xunit;

namespace ScummEditor.UnitTests
{
    public class BitStreamTests
    {
        [Fact]
        public void RebuildsTheSourceBytes()
        {
            var sourceBytes = new byte[2];
            sourceBytes[0] = 100; // 0110 0100
            sourceBytes[1] = 54;  // 0011 0110

            var bitStreamManager = new BitStreamManager(sourceBytes);

            Assert.Equal(16, bitStreamManager.Lenght);

            byte[] rebuildBytes = bitStreamManager.ToByteArray();

            Assert.Equal(sourceBytes[0], rebuildBytes[0]);
            Assert.Equal(sourceBytes[1], rebuildBytes[1]);
        }

        [Fact]
        public void BuildsBytesFromIndividuallyAddedBits()
        {
            var bitStreamManager = new BitStreamManager();

            // 100 = 0110 0100
            bitStreamManager.AddBit(false);
            bitStreamManager.AddBit(false);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(false);

            bitStreamManager.AddBit(false);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(false);

            // 54 = 0011 0110
            bitStreamManager.AddBit(false);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(false);

            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(true);
            // The last two bits are left out; the stream pads the final byte with zeros.

            var sourceBytes = new byte[2];
            sourceBytes[0] = 100; // 0110 0100
            sourceBytes[1] = 54;  // 0011 0110

            Assert.Equal(14, bitStreamManager.Lenght);

            byte[] rebuildBytes = bitStreamManager.ToByteArray();

            Assert.Equal(sourceBytes[0], rebuildBytes[0]);
            Assert.Equal(sourceBytes[1], rebuildBytes[1]);
        }

        [Fact]
        public void ReadBitAdvancesThePosition()
        {
            var sourceBytes = new byte[1];
            sourceBytes[0] = 130; // 1000 0010

            var bitStreamManager = new BitStreamManager(sourceBytes);

            Assert.Equal(0, bitStreamManager.Position);

            // Read one bit and confirm it is off; the remaining stream is 1000 001.
            Assert.False(bitStreamManager.ReadBit());

            Assert.Equal(1, bitStreamManager.Position);

            // The stream has not ended yet.
            Assert.False(bitStreamManager.EndOfStream);

            // Read the next 7 bits.
            byte result = bitStreamManager.ReadValue(7);

            // 65 = 0100 0001 because the most significant bit was padded with zero.
            Assert.Equal(65, result);
        }

        [Fact]
        public void ReadsBytesThenBitsThenAValue()
        {
            // 0x11 (17)  - 0001 0001
            // 0x05 (5)   - 0000 0101
            // 0x80 (128) - 1000 0000
            // 0xFC (252) - 1111 1100

            var bs = new BitStreamManager(new byte[] { 0x11, 0x05, 0x80, 0xFC });

            byte compression = bs.ReadByte();
            Assert.Equal(0x11, compression);

            Assert.Equal(0x05, bs.ReadByte()); // palette number

            // 0x80: draw everything in the same palette (seven zero bits, then a control bit).
            Assert.False(bs.ReadBit()); // 0
            Assert.False(bs.ReadBit()); // 0
            Assert.False(bs.ReadBit()); // 0
            Assert.False(bs.ReadBit()); // 0

            Assert.False(bs.ReadBit()); // 0
            Assert.False(bs.ReadBit()); // 0
            Assert.False(bs.ReadBit()); // 0

            // Control bit found.
            Assert.True(bs.ReadBit()); // 1

            // 0xFC: bit 0 means read the next palette index (7 bits).
            Assert.False(bs.ReadBit()); // 0

            Assert.Equal(0x7E, bs.ReadValue(7));
        }

        [Fact]
        public void RoundTripsAFiveBitValueThroughAByteArray()
        {
            // 5-bit palette indexes are used when the highest palette index is in 16..31.
            var bs = new BitStreamManager();

            bs.AddBit(true);
            bs.AddBit(false);
            bs.AddByte(28, 5);

            byte[] values = bs.ToByteArray();
            bs = new BitStreamManager(values);

            Assert.True(bs.ReadBit());
            Assert.False(bs.ReadBit());
            Assert.Equal(28, bs.ReadValue(6));
        }
    }
}
