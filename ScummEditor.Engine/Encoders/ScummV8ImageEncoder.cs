using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ScummEditor.Engine.Exceptions;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited SCUMM v8 room background or object image back into the container. v8 keeps the
    /// same per-strip SMAP codec as v5/v6/v7, so this reuses <see cref="ImageEncoder.EncodeStrips"/> and
    /// only rebuilds the v8-specific BSTR/WRAP leaf: an inner OFFS table ([tag][size:BE][stripOffset:u32 LE
    /// x numStrips]) followed by the strip codec+data, offsets relative to the leaf start. Replacing that
    /// leaf's bytes is enough - the save pipeline (CalculateBlockSize/Offsets) recomputes every enclosing
    /// IMAG/WRAP/SMAP/BSTR size and the v8 index relocation fixes the directory offsets, so an edit of any
    /// size produces a loadable game. The edit is index-based (the imported PNG must be indexed), so the
    /// palette indexes are preserved losslessly.
    /// </summary>
    public class ScummV8ImageEncoder
    {
        public void EncodeBackground(RoomBlock room, Bitmap bitmap)
        {
            byte[] rmhd = ScummV8ImageDecoder.LeafBytes(ScummV8ImageDecoder.FindChild(room, "RMHD"));
            if (rmhd == null || rmhd.Length < 12) throw new ImageEncodeException("v8 room has no RMHD");
            int width = (int)ScummV8ImageDecoder.ReadUInt32LE(rmhd, 4);
            int height = (int)ScummV8ImageDecoder.ReadUInt32LE(rmhd, 8);

            ReplaceImag(ScummV8ImageDecoder.FindChild(room, "IMAG"), width, height, bitmap, "background");
        }

        public void EncodeObject(RoomBlock room, int objectIndex, Bitmap bitmap)
        {
            List<BlockBase> obims = room.Childrens.Where(c => c.BlockType == "OBIM").ToList();
            if (objectIndex < 0 || objectIndex >= obims.Count) throw new ImageEncodeException("v8 object index out of range");
            BlockBase obim = obims[objectIndex];

            byte[] imhd = ScummV8ImageDecoder.LeafBytes(ScummV8ImageDecoder.FindChild(obim, "IMHD"));
            if (imhd == null || imhd.Length < 64) throw new ImageEncodeException("v8 object has no IMHD");
            int width = (int)ScummV8ImageDecoder.ReadUInt32LE(imhd, 56);
            int height = (int)ScummV8ImageDecoder.ReadUInt32LE(imhd, 60);

            ReplaceImag(ScummV8ImageDecoder.FindChild(obim, "IMAG"), width, height, bitmap, "object");
        }

        private static void ReplaceImag(BlockBase imag, int width, int height, Bitmap bitmap, string what)
        {
            if (imag == null) throw new ImageEncodeException("the v8 " + what + " has no IMAG image to replace");
            RawContainerBlock leafBlock = ScummV8ImageDecoder.FindStripLeaf(imag);
            if (leafBlock == null) throw new ImageEncodeException("the v8 " + what + " has no SMAP strip data (it may be a BOMP or imageless object)");

            if (!IndexedImageHelper.IsIndexed(bitmap))
            {
                throw new ImageEncodeException("The image must be an indexed (palette-based) PNG so the original palette indexes are preserved. Re-export it and edit it without converting to RGB.");
            }
            if (bitmap.Width != width || bitmap.Height != height)
            {
                throw new ImageEncodeException(string.Format("The image must be {0}x{1} (the {2} size); got {3}x{4}.", width, height, what, bitmap.Width, bitmap.Height));
            }

            byte[,] matrix = IndexedImageHelper.GetIndexMatrix(bitmap);
            byte transparency = FindUnusedIndex(matrix, width, height); // force non-transparent codecs (index-preserving)

            List<StripData> strips = new ImageEncoder().EncodeStrips(matrix, width, height, transparency, ImageEncoder.EncodeTypeSettings.AutoDetect);
            leafBlock.Contents = BuildStripLeaf(strips);
        }

        /// <summary>
        /// Builds the BSTR/WRAP leaf: [OFFS tag][size:BE = 8 + numStrips*4][stripOffset:u32 LE x numStrips]
        /// then each strip's [codec byte][bitstream]. Offsets are relative to the leaf start.
        /// </summary>
        private static byte[] BuildStripLeaf(List<StripData> strips)
        {
            int tableLength = 8 + strips.Count * 4; // OFFS header (tag + BE size) + the offset table
            var output = new List<byte>(tableLength + strips.Sum(s => s.ImageData.Length + 1));

            output.Add((byte)'O'); output.Add((byte)'F'); output.Add((byte)'F'); output.Add((byte)'S');
            output.Add((byte)(tableLength >> 24)); output.Add((byte)(tableLength >> 16));
            output.Add((byte)(tableLength >> 8)); output.Add((byte)tableLength);

            int cursor = tableLength;
            foreach (StripData strip in strips)
            {
                output.Add((byte)(cursor & 0xFF)); output.Add((byte)((cursor >> 8) & 0xFF));
                output.Add((byte)((cursor >> 16) & 0xFF)); output.Add((byte)((cursor >> 24) & 0xFF));
                cursor += 1 + strip.ImageData.Length;
            }

            foreach (StripData strip in strips)
            {
                output.Add(strip.CodecId);
                output.AddRange(strip.ImageData);
            }

            return output.ToArray();
        }

        /// <summary>A palette index no pixel uses (so the strip codecs come out non-transparent and the
        /// index matrix is preserved exactly); falls back to 0 if all 256 indexes are present.</summary>
        private static byte FindUnusedIndex(byte[,] matrix, int width, int height)
        {
            var used = new bool[256];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    used[matrix[x, y]] = true;
            for (int i = 255; i >= 0; i--)
                if (!used[i]) return (byte)i;
            return 0;
        }
    }
}
