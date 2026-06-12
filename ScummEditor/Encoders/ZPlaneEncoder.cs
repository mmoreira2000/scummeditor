using System;
using System.Collections.Generic;
using System.Drawing;
using ScummEditor.Exceptions;
using ScummEditor.Structures.DataFile;
using System.Linq;

namespace ScummEditor.Encoders
{
    public class ZPlaneEncoder
    {
        private ushort _width;
        private ushort _height;
        private ZPlane _zPlane;
        private Bitmap _imageToEncode;

        //Used during encode.
        private int _currentOffset;
        private int _currentLine;

        public void Encode(RoomBlock blockToEncode, int objectIndex, int imageIndex, Bitmap imageToEncode, int zPlaneIndex)
        {
            var obj = blockToEncode.GetOBIMs()[objectIndex];

            _imageToEncode = imageToEncode;

            ObjectImageHeader IMHD = obj.GetIMHD();

            _width = IMHD.Width;
            _height = IMHD.Height;
            _zPlane = obj.GetIMxx()[imageIndex].GetZPlanes()[zPlaneIndex];

            Encode();
        }

        public void Encode(RoomBlock blockToEncode, Bitmap imageToEncode, int zPlaneIndex)
        {
            _imageToEncode = imageToEncode;

            var RMHD = blockToEncode.GetRMHD();
            
            _width = RMHD.Width;
            _height = RMHD.Height;
            _zPlane = blockToEncode.GetRMIM().GetIM00().GetZPlanes()[zPlaneIndex];

            Encode();
        }

        private void Encode()
        {
            if (_imageToEncode.Width != _width || _imageToEncode.Height != _height)
            {
                throw new ImageEncodeException("The image must have the same size as ROOM/OBJ");
            }

            _zPlane.Strips.Clear();
            int stripCount = _width / 8;
            for (int i = 0; i < stripCount; i++)
            {
                _currentOffset = i * 8;//each strip has 8 pixels width, so with multiply the current strip by 8 to get the proper offset where it should be rendered.
                _zPlane.Strips.Add(EncodeZPlaneStrip());
            }

            //calculaOffSets
            var cOffSet = (ushort)(BinaryHelper.ConvertUTF8StringToByteArray(_zPlane.BlockType).Length + 4 + (_zPlane.Strips.Count * 2));
            foreach (ZPlaneStripData stripData in _zPlane.Strips)
            {
                stripData.OffSet = cOffSet;
                cOffSet += (ushort)(stripData.ImageData.Length);
            }
        }

        private ZPlaneStripData EncodeZPlaneStrip()
        {
            var strip = new ZPlaneStripData();
            var bitStream = new BitStreamManager();

            var linesInformation = new List<LineInformation>();

            _currentLine = 0;
            var currentLineInformation = new LineInformation();

            byte lastLine = GetNextLine();
            if (lastLine == PeekNextLine())
            {
                currentLineInformation.RepeatSameLine = true;
            }

            currentLineInformation.Lines.Add(lastLine);

            //Primeira passagem - Cadastra tudo alternado
            while (_currentLine < (_height))
            {
                byte currentLine = GetNextLine();

                if (currentLine != lastLine)
                {
                    if (currentLineInformation.RepeatSameLine)
                    {
                        //Switched from REPEAT to NO-REPEAT
                        linesInformation.Add(currentLineInformation);

                        currentLineInformation = new LineInformation();
                        currentLineInformation.RepeatSameLine = false;
                    }
                }
                else
                {
                    if (!currentLineInformation.RepeatSameLine)
                    {
                        //Switched from NO-REPEAT to REPEAT
                        linesInformation.Add(currentLineInformation);

                        currentLineInformation = new LineInformation();
                        currentLineInformation.RepeatSameLine = true;
                    }
                }

                currentLineInformation.Lines.Add(currentLine);

                lastLine = currentLine;
            }
            linesInformation.Add(currentLineInformation);

            //Second pass - checks the non-repeated list: when its last element repeats the first element of the next repeated list, it must move there.
            for (int i = 1; i < linesInformation.Count; i++)
            {
                var previous = linesInformation[i - 1];
                var previousLastIndex = previous.Lines.Count - 1;
                var current = linesInformation[i - 1];

                if (!previous.RepeatSameLine && current.RepeatSameLine && previous.Lines[previousLastIndex] == current.Lines[0])
                {
                    //moves the item from the previous list to the current one
                    current.Lines.Insert(0, previous.Lines[previousLastIndex]);

                    previous.Lines.RemoveAt(previousLastIndex);
                }
            }

            //Terceira passagem - procura por listas que eventualmente ficaram vazias, e as remove.
            //The list must be walked backwards, otherwise it would be read wrong and throw IndexOutOfBounds at the end.
            for (int i = linesInformation.Count - 1; i >= 0; i--)
            {
                if (linesInformation[i].Lines.Count == 0)
                {
                    linesInformation.RemoveAt(i);
                }
            }

            //Fourth pass: splits lists with more than 127 items, since 127 is the encoding limit per chunk.
            var finalLinesInformation = new List<LineInformation>();
            foreach (LineInformation lineInformation in linesInformation)
            {
                if (lineInformation.Lines.Count > 127)
                {
                    while (lineInformation.Lines.Count > 127)
                    {
                        var dividedLineInformation = new LineInformation();
                        dividedLineInformation.RepeatSameLine = lineInformation.RepeatSameLine;
                        dividedLineInformation.Lines.AddRange(lineInformation.Lines.Take(127).ToArray());

                        finalLinesInformation.Add(dividedLineInformation);

                        lineInformation.Lines = lineInformation.Lines.Skip(127).ToList();
                    }
                }

                finalLinesInformation.Add(lineInformation);
            }

            //Quinta passagem, grava o BitStream.
            foreach (LineInformation lineInformation in finalLinesInformation)
            {
                if (lineInformation.RepeatSameLine)
                {
                    byte repeatByte = BinaryHelper.Compose2Bytes(1, (byte)lineInformation.Lines.Count, 7);
                    bitStream.AddByte(repeatByte);
                    bitStream.AddByte(lineInformation.Lines[0]);
                }
                else
                {
                    //Checks whether the different-lines run had only 1 line before writing it; this can happen
                    //when lines 1 and 2 are A and lines 3 and 4 are B, for example.
                    byte repeatByte = BinaryHelper.Compose2Bytes(0, (byte)lineInformation.Lines.Count, 7);
                    bitStream.AddByte(repeatByte);
                    foreach (byte differentLine in lineInformation.Lines)
                    {
                        bitStream.AddByte(differentLine);
                    }
                }
            }

            strip.ImageData = bitStream.ToByteArray();
            return strip;
        }

        private byte PeekNextLine()
        {
            byte result = 0;
            for (int i = 0; i < 8; i++)
            {
                Color pixel = _imageToEncode.GetPixel(_currentOffset + i, _currentLine);
                if (ColorCompare(pixel, Color.Black))
                {
                    result = BinaryHelper.SetBitState(result, 7 - i, true);
                }
                else
                {
                    result = BinaryHelper.SetBitState(result, 7 - i, false);
                }
            }
            return result;
        }

        private byte GetNextLine()
        {
            byte result = PeekNextLine();
            _currentLine++;

            return result;
        }

        private bool ColorCompare(Color colorA, Color colorB, bool compareAlpha = false)
        {
            if (compareAlpha)
            {
                return colorA.A == colorB.A &&
                       colorA.R == colorB.R &&
                       colorA.G == colorB.G &&
                       colorA.B == colorB.B;
            }
            return colorA.R == colorB.R &&
                   colorA.G == colorB.G &&
                   colorA.B == colorB.B;
        }
    }
}