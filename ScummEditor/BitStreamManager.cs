using System;
using System.Collections.Generic;
using System.IO;
using ScummEditor.Exceptions;

namespace ScummEditor
{
    public class BitStreamManager
    {
        private List<bool> _bitStream;

        public BitStreamManager()
        {
            _bitStream = new List<bool>();
        }
        public BitStreamManager(byte[] byteArray)
        {
            _bitStream = new List<bool>(byteArray.Length * 8);

            foreach (byte b in byteArray)
            {
                AddByte(b);
            }
        }

        private int _position;
        public int Position
        {
            get { return _position; }
            set
            {
                if (value > _bitStream.Count)
                {
                    throw new IndexOutOfRangeException("Position is after the end of stream");
                }
                _position = value;
            }
        }
        public bool EndOfStream
        {
            get { return _position == _bitStream.Count; }
        }

        public int Lenght { get { return _bitStream.Count; } }

        private void CheckIsEnoughSpace(int numBits)
        {
            if (_position + numBits > _bitStream.Count)
            {
                throw new EndOfStreamException("Position is after the end of stream");
            }
        }

        public bool ReadBit()
        {
            if (EndOfStream) throw new EndOfStreamException("Stream finished");

            return _bitStream[_position++];
        }

        public byte ReadByte()
        {
            return ReadValue(8);
        }

        public byte ReadValue(int numBits)
        {
            CheckIsEnoughSpace(numBits);

            var bitSet = new bool[numBits];

            for (int i = 0; i < numBits; i++)
            {
                bitSet[i] = _bitStream[Position + i];
            }
            Position += numBits;

            return BitArrayToByte(bitSet);
        }

        public void AddBit(bool bit)
        {
            _bitStream.Add(bit);
        }

        public void AddByte(byte newByte)
        {
            _bitStream.Add((newByte & 1) != 0); //01  = 0000 0001
            _bitStream.Add((newByte & 2) != 0); //02  = 0000 0010
            _bitStream.Add((newByte & 4) != 0); //04  = 0000 0100
            _bitStream.Add((newByte & 8) != 0); //08  = 0000 1000
            _bitStream.Add((newByte & 16) != 0); //16  = 0001 0000
            _bitStream.Add((newByte & 32) != 0); //32  = 0010 0000
            _bitStream.Add((newByte & 64) != 0); //64  = 0100 0000
            _bitStream.Add((newByte & 128) != 0); //128 = 1000 0000
        }
        public void AddByte(byte newByte, int numBits)
        {
            for (int i = 0; i < numBits; i++)
            {
                _bitStream.Add((newByte & ((int)Math.Pow(2, i))) != 0);
            }
        }

        public byte[] ToByteArray()
        {
            return BitArrayToByteArray(_bitStream.ToArray());
        }

        private byte[] BitArrayToByteArray(bool[] bitArray)
        {
            int numBytes = bitArray.Length / 8;

            if (bitArray.Length % 8 != 0) numBytes++;

            byte[] bytes = new byte[numBytes];

            int byteIndex = 0, bitIndex = 0;

            for (int i = 0; i < bitArray.Length; i++)
            {
                if (bitArray[i])
                {
                    //Como os bit são armazenados em ordem reversa, ou seja, do menos significativo para o mais significativo
                    //Então aqui ele precisa ser reconstruido em ordem reversa também.
                    //set os bits estivessem na ordem certa, então a linha abaixo seria:
                    //bytes[byteIndex] |= (byte)(1 << (7 - bitIndex));
                    bytes[byteIndex] |= (byte)(1 << bitIndex);
                }

                bitIndex++;
                if (bitIndex == 8)
                {
                    bitIndex = 0;
                    byteIndex++;
                }
            }

            return bytes;
        }

        private byte BitArrayToByte(bool[] bitArray)
        {
            if (bitArray.Length > 8) throw new ConversionException("Maximum allowed is 8 bits");

            var resultByte = (byte)0;

            for (int i = 0; i < bitArray.Length; i++)
            {
                if (bitArray[i])
                {
                    //Como os bit são armazenados em ordem reversa, ou seja, do menos significativo para o mais significativo
                    //Então aqui ele precisa ser reconstruido em ordem reversa também.
                    //set os bits estivessem na ordem certa, então a linha abaixo seria:
                    //bytes[byteIndex] |= (byte)(1 << (7 - bitIndex));
                    resultByte |= (byte)(1 << i);
                }
            }

            return resultByte;
        }

        public uint ReadUInt16()
        {
            var bytes = new byte[2];
            bytes[0] = ReadByte();
            bytes[1] = ReadByte();

            return BinaryHelper.ByteToUInt16(bytes);
        }
    }
}