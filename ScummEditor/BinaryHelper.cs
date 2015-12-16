using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using ScummEditor.Exceptions;

namespace ScummEditor
{
    public static class BinaryHelper
    {
        //signed word (2 bytes)
        private static short ByteToInt16(byte[] bytes, bool bigEndian = false)
        {
            if (bigEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToInt16(bytes, 0);
        }

        //word (2 bytes)
        public static ushort ByteToUInt16(byte[] bytes, bool bigEndian = false)
        {
            if (bigEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt16(bytes, 0);
        }

        //dword (4 bytes)
        private static uint ByteToUInt32(byte[] bytes, bool bigEndian = false)
        {
            if (bigEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt32(bytes, 0);
        }

        //qword (8 bytes)
        private static ulong ByteToUInt64(byte[] bytes, bool bigEndian = false)
        {
            if (bigEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToUInt64(bytes, 0);
        }

        public static byte GetBitsFromByte(byte b, int count, int offset = 0)
        {
            {
                //information about how this works on file: bitwise_description.txt
                return (byte)((b >> offset) & ((1 << count) - 1));
            }
        }


        //Information about this method on file: bitwise_operators.html
        public static bool CheckBitState(byte b, int bitnumber)
        {
            return (b & 1 << bitnumber) != 0;
        }

        public static bool CheckBitState(ushort b, int bitnumber)
        {
            return (b & 1 << bitnumber) != 0;
        }

        //Information about this method on file: bitwise_operators.html
        public static byte SetBitState(byte b, int bitNumber, bool bitState)
        {
            if (bitState)
            {
                return (byte)(b | 1 << bitNumber);
            }
            else
            {
                return (byte)(b & ~(1 << bitNumber));
            }
        }

        //Information about this method on file: bitwise_operators.html
        private static byte ToggleBitState(byte b, int bitNumber)
        {
            return (byte)(b ^ 1 << bitNumber);
        }


        //signed word (2 bytes)
        private static byte[] Int16ToBytes(short value, bool bigEndian = false)
        {

            byte[] bytes = BitConverter.GetBytes(value);

            if (bytes.Length != 2) throw new ConversionException(string.Format("Erro! A conversão deveria gerar 2 bytes, porem gerou {0}", bytes.Length));

            if (bigEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        //word (2 bytes)
        public static byte[] UInt16ToBytes(ushort value, bool bigEndian = false)
        {

            byte[] bytes = BitConverter.GetBytes(value);

            if (bytes.Length != 2) throw new ConversionException(string.Format("Erro! A conversão deveria gerar 2 bytes, porem gerou {0}", bytes.Length));

            if (bigEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        //dword (4 bytes)
        private static byte[] UInt32ToBytes(uint value, bool bigEndian = false)
        {

            byte[] bytes = BitConverter.GetBytes(value);

            if (bytes.Length != 4) throw new ConversionException(string.Format("Erro! A conversão deveria gerar 4 bytes, porem gerou {0}", bytes.Length));

            if (bigEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }

        //qword (8 bytes)
        private static byte[] UInt64ToBytes(ulong value, bool bigEndian = false)
        {

            byte[] bytes = BitConverter.GetBytes(value);

            if (bytes.Length != 8) throw new ConversionException(string.Format("Erro! A conversão deveria gerar 8 bytes, porem gerou {0}", bytes.Length));

            if (bigEndian)
            {
                Array.Reverse(bytes);
            }
            return bytes;
        }


        public static byte Compose2Bytes(byte byte1, byte byte2, int positionByte1, int positionByte2 = 0)
        {
            var finalByte1 = (byte)((byte1 << positionByte1));
            var finalByte2 = (byte)((byte2 << positionByte2));

            var composite = (byte)(finalByte1 | finalByte2);

            return composite;
        }

        public static byte[] ConvertUTF8StringToByteArray(string text)
        {
            var utf8 = new UTF8Encoding();
            //var ascii  = new ASCIIEncoding();
            Byte[] bytesMessage = utf8.GetBytes(text);

            return bytesMessage;
        }

        public static string ConvertByteArrayToUTF8String(byte[] bytes)
        {
            var utf8 = new UTF8Encoding();
            //var ascii = new ASCIIEncoding();
            var text = utf8.GetString(bytes);

            return text;
        }


        //
        //
        //  STREAM EXTENSIONS
        //
        //
        public static string ReadUTF8Sring(this Stream stream, int numBytes)
        {
            byte[] bytes = ReadBytes(stream, numBytes);
            return BinaryHelper.ConvertByteArrayToUTF8String(bytes);
        }

        public static short ReadInt16(this Stream stream, bool bigEndian = false)
        {
            return BinaryHelper.ByteToInt16(ReadBytes(stream, 2), bigEndian);
        }


        public static ushort ReadUint16(this Stream stream, bool bigEndian = false)
        {
            return BinaryHelper.ByteToUInt16(ReadBytes(stream, 2), bigEndian);
        }
        public static uint ReadUint32(this Stream stream, bool bigEndian = false)
        {
            return BinaryHelper.ByteToUInt32(ReadBytes(stream, 4), bigEndian);
        }

        public static byte ReadByte1(this Stream stream)
        {
            return InternalReadByte(stream, false);
        }

        public static byte[] ReadBytes(this Stream stream, int count)
        {
            return InternalReadBytes(stream, count, false);
        }

        public static byte PeekByte(this Stream stream)
        {
            return InternalReadByte(stream, true);
        }

        public static byte[] PeekBytes(this Stream stream, int count)
        {
            return InternalReadBytes(stream, count, true);
        }

        public static ushort PeekUint16(this Stream stream, bool bigEndian = false)
        {
            return BinaryHelper.ByteToUInt16(PeekBytes(stream, 2), bigEndian);
        }

        private static byte InternalReadByte(Stream stream, bool peekOnly)
        {
            byte[] result = InternalReadBytes(stream, 1, peekOnly);

            if (result.Length == 0)
            {
                return 0;
            }
            return result[0];
        }

        private static byte[] InternalReadBytes(Stream stream, int count, bool peekOnly)
        {
            if (!stream.CanRead)
            {
                throw new InvalidOperationException("Base stream is not readable!");
            }
            if (peekOnly && !stream.CanSeek)
            {
                throw new InvalidOperationException("Base stream is not seekable!");
            }

            long originalPosition = stream.Position;

            var result = new byte[count];
            int numRead = 0;

            do
            {
                int bytesRead = stream.Read(result, numRead, count);
                if (bytesRead == 0)
                {
                    break;
                }
                numRead += bytesRead;
                count -= bytesRead;
            } while (count > 0);

            if (numRead != result.Length)
            {
                // Trim array.  This should happen on EOF & possibly net streams.
                var copy = new byte[numRead];
                Array.Copy(result, copy, numRead);
                result = copy;
            }

            if (peekOnly)
            {
                stream.Position = originalPosition;
            }
            return result;
        }



        public static void Write(this Stream stream, uint value, bool bigEndian = false)
        {
            WriteBytes(stream, BinaryHelper.UInt32ToBytes(value, bigEndian));
        }

        public static void Write(this Stream stream, ushort value, bool bigEndian = false)
        {
            WriteBytes(stream, BinaryHelper.UInt16ToBytes(value, bigEndian));
        }

        public static void Write(this Stream stream, short value, bool bigEndian = false)
        {
            WriteBytes(stream, BinaryHelper.Int16ToBytes(value, bigEndian));
        }

        public static void Write(this Stream stream, byte saveByte)
        {
            stream.WriteByte(saveByte);
        }
        public static void Write(this Stream stream, byte[] buffer)
        {
            WriteBytes(stream, buffer);
        }

        public static void WriteBytes(this Stream stream, byte[] buffer)
        {
            if (!stream.CanWrite)
            {
                throw new InvalidOperationException("Base stream is not writable!");
            }
            stream.Write(buffer, 0, buffer.Length);
        }


    }
}
