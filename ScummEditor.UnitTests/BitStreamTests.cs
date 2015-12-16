using NUnit.Framework;
namespace ScummEditor.UnitTests
{
    [TestFixture]
    public class BitStreamTests
    {
        [Test]
        public void TestArrayRebuild()
        {
            var sourceBytes = new byte[2];
            sourceBytes[0] = 100; //0110 0100
            sourceBytes[1] = 54;  //0011 0110

            var bitStreamManager = new BitStreamManager(sourceBytes);

            Assert.AreEqual(16, bitStreamManager.Lenght);

            byte[] rebuildBytes = bitStreamManager.ToByteArray();

            Assert.AreEqual(sourceBytes[0], rebuildBytes[0]);
            Assert.AreEqual(sourceBytes[1], rebuildBytes[1]);
        }

        [Test]
        public void TestArrayBuildFromBitPopulated()
        {
            var bitStreamManager = new BitStreamManager();

            //100 = 0110 0100
            bitStreamManager.AddBit(false);
            bitStreamManager.AddBit(false);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(false);

            bitStreamManager.AddBit(false);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(false);

            //54 = 0011 0110
            bitStreamManager.AddBit(false);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(false);

            bitStreamManager.AddBit(true);
            bitStreamManager.AddBit(true);
            //Deixa esses dois em branco, e o bitStream tem que preencher com 0s...
            //bitStreamManager.AddBit(false);
            //bitStreamManager.AddBit(false);

            var sourceBytes = new byte[2];
            sourceBytes[0] = 100; //0110 0100
            sourceBytes[1] = 54;  //


            Assert.AreEqual(14, bitStreamManager.Lenght);

            byte[] rebuildBytes = bitStreamManager.ToByteArray();

            Assert.AreEqual(sourceBytes[0], rebuildBytes[0]);
            Assert.AreEqual(sourceBytes[1], rebuildBytes[1]);
        }

        [Test]
        public void TestReadBitAndAdvanceStream()
        {
            var sourceBytes = new byte[1];
            sourceBytes[0] = 130; //1000 0010

            var bitStreamManager = new BitStreamManager(sourceBytes);

            Assert.AreEqual(0, bitStreamManager.Position);

            //Le um bit e verifica se ele é off;
            //o Stream resultante ficou assim: 1000 001
            Assert.IsFalse(bitStreamManager.ReadBit());


            Assert.AreEqual(1, bitStreamManager.Position);

            //Verifica que o stream não terminou
            Assert.IsFalse(bitStreamManager.EndOfStream);

            //Le mais 7 bits
            byte result = bitStreamManager.ReadValue(7);

            //65 = 0100 0001 porque o bit mais significante foi completado em branco.
            Assert.AreEqual(65, result);
        }

        [Test]
        public void TestB()
        {
            //0x11 (17)  - 0000 1011
            //0x05 (5)   - 0000 0101
            //0x80 (128) - 1000 0000
            //0xFC (252) - 1111 1100

            var bs = new BitStreamManager(new byte[] { 0x11, 0x05, 0x80, 0xFC });
            //int pSize = 7;

            var compression = bs.ReadByte();
            Assert.AreEqual(0x11, compression);

            Assert.AreEqual(0x05, bs.ReadByte()); //le o numero da palheta;

            //0x80

            //Desenha tudo na mesma paletta.
            Assert.AreEqual(false, bs.ReadBit()); //0
            Assert.AreEqual(false, bs.ReadBit()); //0
            Assert.AreEqual(false, bs.ReadBit()); //0
            Assert.AreEqual(false, bs.ReadBit()); //0

            Assert.AreEqual(false, bs.ReadBit()); //0
            Assert.AreEqual(false, bs.ReadBit()); //0
            Assert.AreEqual(false, bs.ReadBit()); //0

            //Encontrou bit de controle.
            Assert.AreEqual(true, bs.ReadBit());  //1

            //0xFC

            //Bit 0 - le o próximo indice da palheta (7 bits)
            Assert.AreEqual(false, bs.ReadBit()); //0

            Assert.AreEqual(0x7E, bs.ReadValue(7));
            //Assert.AreEqual(false, bs.ReadBit()); //0
            //Assert.AreEqual(true, bs.ReadBit());  //1
            //Assert.AreEqual(true, bs.ReadBit());  //1

            //Assert.AreEqual(true, bs.ReadBit());  //1
            //Assert.AreEqual(true, bs.ReadBit());  //1
            //Assert.AreEqual(true, bs.ReadBit());  //1
            //Assert.AreEqual(true, bs.ReadBit());  //1
        }


        [Test]
        public void TestC()
        {
            //            else if (highestPaletteIndex >= 16 && highestPaletteIndex <= 31)
            //{
            //     paletteBitSize = 5;//max 31 values;

            BitStreamManager bs = new BitStreamManager();

            bs.AddBit(true);
            bs.AddBit(false);
            bs.AddByte(28, 5);

            byte[] values = bs.ToByteArray();
            bs = new BitStreamManager(values);

            Assert.IsTrue(bs.ReadBit());
            Assert.IsFalse(bs.ReadBit());
            Assert.AreEqual(28,bs.ReadValue(6));
            //Assert.IsFalse(bs.ReadBit());
        }
    }
}