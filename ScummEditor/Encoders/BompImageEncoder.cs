using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ScummEditor.Exceptions;
using ScummEditor.Structures.DataFile;

namespace ScummEditor.Encoders
{
    public class BompImageEncoder
    {
        private ushort _width;
        private ushort _height;
        private Bitmap _imageToEncode;
        private ImageBomp _imageBomp;

        // Raw palette indexes read from the (indexed) source bitmap. The codec stores these
        // directly, so the index is preserved losslessly regardless of duplicate palette colors.
        private byte[,] _indexMatrix;

        //Used during encode.
        private int _currentLine;

        public void Encode(RoomBlock blockToEncode, int objectIndex, int imageIndex, Bitmap imageToEncode)
        {
            ObjectImage obj = blockToEncode.GetOBIMs()[objectIndex];

            _imageToEncode = imageToEncode;
            var IMHD = obj.GetIMHD();
            _width = IMHD.Width;
            _height = IMHD.Height;
            _imageBomp = obj.GetIMxx()[imageIndex].GetBOMP();

            Encode();
        }

        public void Encode(RoomBlock blockToEncode, Bitmap imageToEncode)
        {
            _imageToEncode = imageToEncode;
            RoomHeader RMHD = blockToEncode.GetRMHD();

            _width = RMHD.Width;
            _height = RMHD.Height;
            _imageBomp = blockToEncode.GetRMIM().GetIM00().GetBOMP();

            Encode();
        }

        //  lines
        //    encoded size : 16le
        //    line data    : size bytes
        private void Encode()
        {
            var result = new List<byte>();

            if (!IndexedImageHelper.IsIndexed(_imageToEncode))
            {
                throw new ImageEncodeException("The image must be an indexed (palette-based) image so the original palette indexes are preserved. Re-export it from ScummEditor and edit it without converting it to RGB/truecolor.");
            }
            _indexMatrix = IndexedImageHelper.GetIndexMatrix(_imageToEncode);

            _currentLine = 0;

            //Primeira passagem - Cadastra tudo alternado
            while (_currentLine < (_height))
            {
                var bitStream = new BitStreamManager();
                var lineInformation = new List<SegmentInformation>();

                var currentSegmentInformation = new SegmentInformation();
                byte lastColorIndex = (byte)GetIndexAt(0, _currentLine);
                if (lastColorIndex == (byte)GetIndexAt(1, _currentLine))
                {
                    currentSegmentInformation.RepeatSameColor = true;
                }
                currentSegmentInformation.Colors.Add(lastColorIndex);

                for (int x = 1; x < _width; x++)
                {
                    byte currentColorIndex = (byte)GetIndexAt(x, _currentLine);

                    if (currentColorIndex != lastColorIndex)
                    {
                        if (currentSegmentInformation.RepeatSameColor)
                        {
                            //Mudou de REPETIR para NÃO REPETIR
                            lineInformation.Add(currentSegmentInformation);

                            currentSegmentInformation = new SegmentInformation();
                            currentSegmentInformation.RepeatSameColor = false;
                        }
                    }
                    else
                    {
                        if (!currentSegmentInformation.RepeatSameColor)
                        {
                            //Mudou de NÃO REPETIR para REPETIR
                            lineInformation.Add(currentSegmentInformation);

                            currentSegmentInformation = new SegmentInformation();
                            currentSegmentInformation.RepeatSameColor = true;
                        }
                    }

                    currentSegmentInformation.Colors.Add(currentColorIndex);

                    lastColorIndex = currentColorIndex;
                }

                lineInformation.Add(currentSegmentInformation);

                //Segunda passagem - verifica a lista de itens não repetidos para ver se o ultimo elemento é um elemento da lista repetida seguinte.
                for (int i = 1; i < lineInformation.Count; i++)
                {
                    var previous = lineInformation[i - 1];
                    var previousLastIndex = previous.Colors.Count - 1;
                    var current = lineInformation[i - 1];

                    if (!previous.RepeatSameColor && current.RepeatSameColor && previous.Colors[previousLastIndex] == current.Colors[0])
                    {
                        //move o item da lista anterior para a lista atual
                        current.Colors.Insert(0, previous.Colors[previousLastIndex]);

                        previous.Colors.RemoveAt(previousLastIndex);
                    }
                }

                //Terceira passagem - procura por listas que eventualmente ficaram vazias, e as remove.
                //É importante percorrer a lista ao contrários, caso contrário a lista sera lida errada, além de dar IndexOutOfBounds no final.
                for (int i = lineInformation.Count - 1; i >= 0; i--)
                {
                    if (lineInformation[i].Colors.Count == 0)
                    {
                        lineInformation.RemoveAt(i);
                    }
                }

                //Quarta passagem, divide as listas com mais de 128 itens em varias listas, pois 128 é o limite máximo de 7 bits + 1.
                var finalLineInformation = new List<SegmentInformation>();
                foreach (SegmentInformation segmentInformation in lineInformation)
                {
                    if (segmentInformation.Colors.Count > 128)
                    {
                        while (segmentInformation.Colors.Count > 128)
                        {
                            var dividedLineInformation = new SegmentInformation();
                            dividedLineInformation.RepeatSameColor = segmentInformation.RepeatSameColor;
                            dividedLineInformation.Colors.AddRange(segmentInformation.Colors.Take(128).ToArray());

                            finalLineInformation.Add(dividedLineInformation);

                            segmentInformation.Colors = segmentInformation.Colors.Skip(128).ToList();
                        }
                    }
                    finalLineInformation.Add(segmentInformation);
                }

                //Quinta passagem, grava o BitStream.
                foreach (SegmentInformation segmentInformation in finalLineInformation)
                {
                    bitStream.AddBit(segmentInformation.RepeatSameColor);
                    if (segmentInformation.RepeatSameColor)
                    {
                        bitStream.AddByte((byte)(segmentInformation.Colors.Count - 1), 7);
                        bitStream.AddByte(segmentInformation.Colors[0]);
                    }
                    else
                    {
                        bitStream.AddByte((byte)(segmentInformation.Colors.Count - 1), 7);
                        foreach (byte differentLine in segmentInformation.Colors)
                        {
                            bitStream.AddByte(differentLine);
                        }
                    }
                }

                _currentLine++;

                byte[] encodedLine = bitStream.ToByteArray();
                result.AddRange(BinaryHelper.UInt16ToBytes((ushort)encodedLine.Length));
                result.AddRange(encodedLine);
            }

            _imageBomp.Data = result.ToArray();
        }

        // Returns the raw palette index stored at the given pixel of the indexed source bitmap.
        private int GetIndexAt(int x, int y)
        {
            return _indexMatrix[x, y];
        }

    }
}