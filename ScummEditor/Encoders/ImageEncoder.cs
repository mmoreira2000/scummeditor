using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using ScummEditor.Exceptions;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    public class ImageEncoder
    {
        public enum EncodeTypeSettings
        {
            AutoDetect = 0,
            Uncompressed = 1,
            Method1Vertical = 2,
            Method1Horizontal = 3,
            Method2 = 4,
            Method2AlternateCode = 5
        }

        private ushort _width;
        private ushort _height;
        private byte _transparency;
        private Bitmap _imageToEncode;
        private ImageStripTable _strips;

        // Raw palette indexes read from the (indexed) source bitmap. The codec stores these
        // directly, so the index is preserved losslessly regardless of duplicate palette colors.
        private byte[,] _indexMatrix;

        //Used during encode.
        private int _currentOffset;
        private Point _currentLocation;
        private RenderingDirections _renderdingDirection;

        public EncodeTypeSettings EncodeSettings { get; set; }

        public ImageEncoder()
        {
            EncodeSettings = EncodeTypeSettings.AutoDetect;
        }

        public void Encode(RoomBlock blockToEncode, int objectIndex, int imageIndex, Bitmap imageToEncode)
        {
            var obj = blockToEncode.GetOBIMs()[objectIndex];

            _imageToEncode = imageToEncode;

            ObjectImageHeader IMHD = obj.GetIMHD();

            _width = IMHD.Width;
            _height = IMHD.Height;
            _transparency = blockToEncode.GetTRNS().Value;
            _strips = obj.GetIMxx()[imageIndex].GetSMAP();
            Encode();
        }

        public void Encode(RoomBlock blockToEncode, Bitmap imageToEncode)
        {
            _imageToEncode = imageToEncode;
            var RMHD = blockToEncode.GetRMHD();

            _width = RMHD.Width;
            _height = RMHD.Height;
            _transparency = blockToEncode.GetTRNS().Value;
            _strips = blockToEncode.GetRMIM().GetIM00().GetSMAP();

            Encode();
        }

        private void Encode()
        {
            if (_imageToEncode.Width != _width || _imageToEncode.Height != _height)
            {
                throw new ImageEncodeException("The image must have the same size as ROOM/OBJ");
            }

            if (!IndexedImageHelper.IsIndexed(_imageToEncode))
            {
                throw new ImageEncodeException("The image must be an indexed (palette-based) image so the original palette indexes are preserved. Re-export it from ScummEditor and edit it without converting it to RGB/truecolor.");
            }
            _indexMatrix = IndexedImageHelper.GetIndexMatrix(_imageToEncode);

            _strips.Strips.Clear();
            int stripCount = _width / 8;
            for (int i = 0; i < stripCount; i++)
            {
                _currentOffset = i * 8;//each strip has 8 pixels width, so with multiply the current strip by 8 to get the proper offset where it should be rendered.

                _strips.Strips.Add(GetNextStrip());
            }

            //calculaOffSets
            var cOffSet = (uint)(BinaryHelper.ConvertUTF8StringToByteArray(_strips.BlockType).Length + 4 + (_strips.Strips.Count * 4));
            foreach (StripData stripData in _strips.Strips)
            {
                stripData.OffSet = cOffSet;
                cOffSet += (uint)(stripData.ImageData.Length + 1);
            }
        }

        private StripData GetNextStrip()
        {
            StripData strip = null;

            switch (EncodeSettings)
            {
                case EncodeTypeSettings.AutoDetect:
                    strip = EncodeUncompressed();

                    StripData alternateMethod = EncodeCompressedMethod1(true);
                    if (alternateMethod.ImageData.Length < strip.ImageData.Length)
                    {
                        strip = alternateMethod;
                    }

                    alternateMethod = EncodeCompressedMethod1(false);
                    if (alternateMethod.ImageData.Length < strip.ImageData.Length)
                    {
                        strip = alternateMethod;
                    }

                    alternateMethod = EncodeCompressedMethod2();
                    if (alternateMethod.ImageData.Length < strip.ImageData.Length)
                    {
                        strip = alternateMethod;
                    }
                    break;

                case EncodeTypeSettings.Uncompressed:
                    strip = EncodeUncompressed();
                    break;
                case EncodeTypeSettings.Method1Horizontal:
                    strip = EncodeCompressedMethod1(true);
                    break;
                case EncodeTypeSettings.Method1Vertical:
                    strip = EncodeCompressedMethod1(false);
                    break;
                case EncodeTypeSettings.Method2:
                    strip = EncodeCompressedMethod2();
                    break;
            }

            return strip;
        }

        private StripData EncodeUncompressed()
        {
            var strip = new StripData();
            var bitStream = new BitStreamManager();
            strip.CodecId = 0x01;

            _renderdingDirection = strip.RenderdingDirection;
            _currentLocation.X = 0;
            _currentLocation.Y = 0;

            int firstPaletteIndex = GetIndexAt(_currentOffset + 0, 0);
            bitStream.AddByte((byte)firstPaletteIndex);

            while (!(_currentLocation.X == 7 && _currentLocation.Y == (_height - 1)))
            {
                var paletteIndex = GetNextIndex();
                bitStream.AddByte((byte)paletteIndex);
            }

            strip.ImageData = bitStream.ToByteArray();
            return strip;
        }

        private StripData EncodeCompressedMethod1(bool horizontalDirection)
        {
            var strip = new StripData();
            var bitStream = new BitStreamManager();

            bool transparent;
            int paletteSize;
            VerifyStrip(out transparent, out paletteSize, true);

            strip.CodecId = (byte)(GetSubstractionCode(transparent, horizontalDirection, CompressionTypes.Method1) + paletteSize);

            _renderdingDirection = strip.RenderdingDirection;
            _currentLocation.X = 0;
            _currentLocation.Y = 0;

            int negationValue = 1;

            int currentPaletteIndex = GetIndexAt(_currentOffset + 0, 0);

            bitStream.AddByte((byte)currentPaletteIndex);


            while (!(_currentLocation.X == 7 && _currentLocation.Y == (_height - 1)))
            {
                var newPaletteIndex = GetNextIndex();

                if (newPaletteIndex == currentPaletteIndex)
                {
                    bitStream.AddBit(false); //continue Drawing the same color.
                }
                else
                {
                    bitStream.AddBit(true); //discover what to do.

                    if (newPaletteIndex == (currentPaletteIndex - negationValue))
                    {
                        //subtract the negation value from the palette and draw the next pixel;
                        bitStream.AddBit(true);
                        bitStream.AddBit(false);
                    }
                    else if (newPaletteIndex == (currentPaletteIndex - (negationValue * -1)))
                    {
                        //invert the negation value, subtract it from the palette
                        //and draw the next pixel;
                        negationValue *= -1;
                        bitStream.AddBit(true);
                        bitStream.AddBit(true);
                    }
                    else
                    {
                        //reset the negation value, read the next index from the palette,
                        //and draw the pixel.
                        negationValue = 1;
                        bitStream.AddBit(false);
                        bitStream.AddByte((byte)newPaletteIndex, paletteSize);
                    }

                    currentPaletteIndex = newPaletteIndex;
                }

            }

            strip.ImageData = bitStream.ToByteArray();
            return strip;
        }

        private StripData EncodeCompressedMethod2()
        {
            var strip = new StripData();
            var bitStream = new BitStreamManager();

            bool transparent;
            int paletteSize;
            VerifyStrip(out transparent, out paletteSize, true);

            bool alternateCode = EncodeSettings == EncodeTypeSettings.Method2AlternateCode;

            strip.CodecId = (byte)(GetSubstractionCode(transparent, true, CompressionTypes.Method2, alternateCode) + paletteSize);
            if (strip.CompressionType == CompressionTypes.Unknow)
            {
                strip.CodecId = (byte)(GetSubstractionCode(transparent, true, CompressionTypes.Method2, !alternateCode) + paletteSize);
            }

            _renderdingDirection = strip.RenderdingDirection;
            _currentLocation.X = 0;
            _currentLocation.Y = 0;

            int currentPaletteIndex = GetIndexAt(_currentOffset + 0, 0);

            bitStream.AddByte((byte)currentPaletteIndex);

            byte accumulatedSameColor = 0;

            while (!(_currentLocation.X == 7 && _currentLocation.Y == (_height - 1)))
            {
                var newPaletteIndex = GetNextIndex();

                if (newPaletteIndex == currentPaletteIndex)
                {
                    accumulatedSameColor++; //Add one to the count of pixels with the same color.
                    if (accumulatedSameColor == 255)
                    {
                        //If we have pixels with the previous color drawed before the color change
                        //We need to write that information to the data, before process the new color.
                        WriteAccumulatedColor(bitStream, accumulatedSameColor);
                        accumulatedSameColor = 0;
                    }
                }
                else
                {
                    if (accumulatedSameColor > 0)
                    {
                        //If we have pixels with the previous color drawed before the color change
                        //We need to write that information to the data, before process the new color.
                        WriteAccumulatedColor(bitStream, accumulatedSameColor);
                        accumulatedSameColor = 0;
                    }


                    /*
                    Then discory the new bit code.
                     
                    10: Read a new palette index from the bitstream (i.e., the number of bits specified by the parameter), and draw the next pixel.
                    11: Read the next 3 bit value, and perform an action, depending on the value:
                           000 (0): Decrease current palette index by 4.
                           001 (1): Decrease current palette index by 3.
                           010 (2): Decrease current palette index by 2.
                           011 (3): Decrease current palette index by 1.
                           100 (4): Read next 8 bits. Draw the number of pixels specified by these 8 bits with the current palette index (somewhat similar to RLE).
                           101 (5): Increase current palette index by 1.
                           110 (6): Increase current palette index by 2.
                           111 (7): Increase current palette index by 3. 
                    */

                    bitStream.AddBit(true); //Add the change action bit

                    if ((newPaletteIndex < (currentPaletteIndex - 4)) || (newPaletteIndex > (currentPaletteIndex + 3)))
                    {
                        //reset the negation value, read the next index from the palette,
                        //and draw the pixel.
                        bitStream.AddBit(false);
                        bitStream.AddByte((byte)newPaletteIndex, paletteSize);
                    }
                    else
                    {
                        //Add the read code bit
                        bitStream.AddBit(true);
                        if (newPaletteIndex == (currentPaletteIndex - 4)) //000 (0): Decrease current palette index by 4.
                        {
                            //Add the code 0
                            bitStream.AddByte(0, 3);
                        }
                        else if (newPaletteIndex == (currentPaletteIndex - 3)) //001 (1): Decrease current palette index by 3.
                        {
                            //Add the code 1
                            bitStream.AddByte(1, 3);
                        }
                        else if (newPaletteIndex == (currentPaletteIndex - 2)) //010 (2): Decrease current palette index by 2.
                        {
                            //Add the code 2
                            bitStream.AddByte(2, 3);
                        }
                        else if (newPaletteIndex == (currentPaletteIndex - 1)) //011 (3): Decrease current palette index by 1.
                        {
                            //Add the code 3
                            bitStream.AddByte(3, 3);
                        }
                        else if (newPaletteIndex == (currentPaletteIndex + 1)) //101 (5): Increase current palette index by 1.
                        {
                            //Add the code 5
                            bitStream.AddByte(5, 3);
                        }
                        else if (newPaletteIndex == (currentPaletteIndex + 2)) //110 (6): Increase current palette index by 2.
                        {
                            //Add the code 6
                            bitStream.AddByte(6, 3);
                        }
                        else if (newPaletteIndex == (currentPaletteIndex + 3)) //111 (7): Increase current palette index by 3.
                        {
                            //Add the code 7
                            bitStream.AddByte(7, 3);
                        }
                        else
                        {
                            throw new ImageEncodeException("nao podia ter caido aqui");
                        }
                    }

                    currentPaletteIndex = newPaletteIndex;
                }

            }

            //if the loop terminated with accumulated bits pending, add these to the stream.
            if (accumulatedSameColor > 0)
            {
                //If we have pixels with the previous color drawed before the color change
                //We need to write that information to the data, before process the new color.
                WriteAccumulatedColor(bitStream, accumulatedSameColor);
            }

            strip.ImageData = bitStream.ToByteArray();
            return strip;
        }

        private void WriteAccumulatedColor(BitStreamManager bitStream, byte accumulatedSameColor)
        {
            if (accumulatedSameColor > 12)
            {
                //Add the change action bits: 
                //   11: Read the next 3 bit value, and perform an action, depending on the value:
                bitStream.AddBit(true);
                bitStream.AddBit(true);
                //Add the count bit action code: 100 (4): Read next 8 bits. Draw the number of pixels specified by these 8 bits with the current palette index (somewhat similar to RLE). 
                bitStream.AddByte(4, 3);

                //Add the number of pixels that will be draw with this color.
                bitStream.AddByte(accumulatedSameColor);
            }
            else
            {
                for (int i = 0; i < accumulatedSameColor; i++)
                {
                    bitStream.AddBit(false);
                }
            }
        }

        // Returns the raw palette index stored at the given pixel of the indexed source bitmap.
        private int GetIndexAt(int x, int y)
        {
            return _indexMatrix[x, y];
        }

        // Advances the current location and returns the index there.
        private int GetNextIndex()
        {
            if (_renderdingDirection == RenderingDirections.Horizontal)
            {
                _currentLocation.X++;
                if (_currentLocation.X == 8)
                {
                    _currentLocation.X = 0;
                    _currentLocation.Y++;
                }
            }
            else
            {
                _currentLocation.Y++;
                if (_currentLocation.Y == _height)
                {
                    _currentLocation.Y = 0;
                    _currentLocation.X++;
                }
            }
            return GetIndexAt(_currentOffset + _currentLocation.X, _currentLocation.Y);
        }

        public void VerifyStrip(out bool hasTransparency, out int paletteBitSize, bool checkTransparentColorEntry)
        {
            hasTransparency = false;
            int highestPaletteIndex = -1;
            paletteBitSize = 0;
            _currentLocation.X = 0;
            _currentLocation.Y = 0;

            int pIndex = GetIndexAt(_currentOffset + 0, 0);
            if (checkTransparentColorEntry && pIndex == _transparency) hasTransparency = true;
            if (pIndex > highestPaletteIndex) highestPaletteIndex = pIndex;

            while (!(_currentLocation.X == 7 && _currentLocation.Y == (_height - 1)))
            {
                pIndex = GetNextIndex();
                if (checkTransparentColorEntry && pIndex == _transparency) hasTransparency = true;
                if (pIndex > highestPaletteIndex) highestPaletteIndex = pIndex;
            }

            if (highestPaletteIndex >= 0 && highestPaletteIndex <= 15)
            {
                paletteBitSize = 4;//max 15 values;
            }
            else if (highestPaletteIndex >= 16 && highestPaletteIndex <= 31)
            {
                paletteBitSize = 5;//max 31 values;
            }
            else if (highestPaletteIndex >= 32 && highestPaletteIndex <= 63)
            {
                paletteBitSize = 6;//max 63 values;
            }
            else if (highestPaletteIndex >= 64 && highestPaletteIndex <= 127)
            {
                paletteBitSize = 7;//max 127 values;
            }
            else if (highestPaletteIndex >= 128 && highestPaletteIndex <= 255)
            {
                paletteBitSize = 8;//max 255 values;
            }
            else
            {
                throw new ImageEncodeException("Indice da paletta invalido");
            }
        }

        private byte GetSubstractionCode(bool hasTransparency, bool horizontalDirection, CompressionTypes compressionType, bool method2AlternateCode = false)
        {
            /*
            IDs 	        Method      	Rendering Direction 	Transparent 	Param Subtraction 	Remarks
            0x01 	        Uncompressed 	Horizontal          	No 	            - 	-
            0x0E .. 0x12 	1st method   	Vertical            	No           	0x0A 	
            0x18 .. 0x1C 	1st method  	Horizontal          	No          	0x14 	
            0x22 .. 0x26 	1st method  	Vertical 	            Yes 	        0x1E 	
            0x2C .. 0x30 	1st method  	Horizontal           	Yes          	0x28 	
            
            0x40 .. 0x44 	2nd method  	Horizontal 	            No 	            0x3C 	            //Não sei se essas duas linhas estão certas, pois nenhuma imagem no DOTT ou SAM&MAX utiliza essas compressões
            0x54 .. 0x58 	2nd method  	Horizontal          	Yes         	0x50 	            //Não sei se essas duas linhas estão certas, pois nenhuma umagem no DOTT ou SAM&MAX utiliza essas compressões
            0x68 .. 0x6C 	2nd method  	Horizontal           	No  	        0x64 	            Same as 0x54 .. 0x58 //Eu inverti essas 2 transparencias, estava errado no site.
            0x7C .. 0x80 	2nd method  	Horizontal 	            Yes	            0x78 	            Same as 0x40 .. 0x44 //Eu inverti essas 2 transparencias, estava errado no site.
             */
            if (compressionType == CompressionTypes.Uncompressed || compressionType == CompressionTypes.Unknow)
            {
                return 0;
            }
            if (compressionType == CompressionTypes.Method1)
            {
                // ReSharper disable ConditionIsAlwaysTrueOrFalse
                //I know the redundancy below, but I left here because it easier to understand later.
                if (!hasTransparency && !horizontalDirection) return 0x0A;
                if (!hasTransparency && horizontalDirection) return 0x14;
                if (hasTransparency && !horizontalDirection) return 0x1E;
                if (hasTransparency && horizontalDirection) return 0x28;
                // ReSharper restore ConditionIsAlwaysTrueOrFalse
            }
            if (compressionType == CompressionTypes.Method2)
            {
                if (hasTransparency)
                {


                    return (byte)(method2AlternateCode ? 0x50 : 0x78);
                }
                return (byte)(method2AlternateCode ? 0x3C : 0x64);
            }

            throw new ImageEncodeException("Unexpected Exception");
        }

    }
}