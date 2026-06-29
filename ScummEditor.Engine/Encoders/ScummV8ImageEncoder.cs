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
            int unused = FindUnusedIndex(matrix, width, height); // an index no pixel uses -> non-transparent codecs

            // Pick the transparency sentinel so the per-strip codecs come out NON-transparent and every
            // palette index is reproduced exactly. If all 256 indexes are present there is no free value,
            // so fall back to the uncompressed codec (0x01), which has no transparent variant - lossless at
            // the cost of size (a rare full-256-colour strip). Otherwise a transparent codec would make the
            // engine skip the pixels equal to the sentinel, leaving holes.
            byte transparency;
            ImageEncoder.EncodeTypeSettings settings;
            if (unused < 0)
            {
                transparency = 0; // unused by the uncompressed codec
                settings = ImageEncoder.EncodeTypeSettings.Uncompressed;
            }
            else
            {
                transparency = (byte)unused;
                settings = ImageEncoder.EncodeTypeSettings.AutoDetect;
            }

            List<StripData> strips = new ImageEncoder().EncodeStrips(matrix, width, height, transparency, settings);
            leafBlock.Contents = BuildStripLeaf(strips);

            // A size-changing re-encode of this SMAP shifts every later SMAP/BOMP state in the same
            // IMAG->WRAP, so the WRAP's OFFS state table (which the engine reads to locate each state's
            // image - see ScummVM getObjectImage) must be rebuilt from the new child sizes.
            RebuildOuterOffsTable(imag);
        }

        /// <summary>
        /// Rebuilds the IMAG->WRAP OFFS state table from the (recomputed) sizes of its SMAP/BOMP children.
        /// Each table entry is a state's image cumulative byte offset from the WRAP body start (= OFFS chunk
        /// start), in file order; entry[0] = the OFFS chunk size, entry[i] = entry[i-1] + child[i-1] size.
        /// ScummVM reads state k from this table to find its SMAP (object.cpp getObjectImage), so without
        /// this a size-changing edit of one state would corrupt every later state of a multi-state object.
        /// A single-state image rebuilds to the same (correct) bytes, so this is always safe to run.
        /// </summary>
        private static void RebuildOuterOffsTable(BlockBase imag)
        {
            BlockBase wrap = ScummV8ImageDecoder.FindChild(imag, "WRAP");
            if (wrap == null) return;
            var offs = ScummV8ImageDecoder.FindChild(wrap, "OFFS") as RawContainerBlock;
            if (offs == null || offs.Contents == null) return;

            // Refresh every child's size from the just-edited subtree (the save pipeline recomputes sizes
            // later too, but the offsets here need the up-to-date sizes now).
            wrap.CalculateBlockSize();

            int stateCount = wrap.Childrens.Count(c => c.BlockType != "OFFS");
            if (stateCount * 4 != offs.Contents.Length) return; // unexpected layout - leave it untouched

            var table = new byte[stateCount * 4];
            long cumulative = 0;
            int entry = 0;
            foreach (BlockBase child in wrap.Childrens)
            {
                if (child.BlockType == "OFFS") { cumulative += child.BlockSize; continue; }
                uint off = (uint)cumulative;
                table[entry * 4] = (byte)(off & 0xFF);
                table[entry * 4 + 1] = (byte)((off >> 8) & 0xFF);
                table[entry * 4 + 2] = (byte)((off >> 16) & 0xFF);
                table[entry * 4 + 3] = (byte)((off >> 24) & 0xFF);
                entry++;
                cumulative += child.BlockSize;
            }
            offs.Contents = table;
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
        /// index matrix is preserved exactly); -1 when all 256 indexes are present (no free value).</summary>
        private static int FindUnusedIndex(byte[,] matrix, int width, int height)
        {
            var used = new bool[256];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    used[matrix[x, y]] = true;
            for (int i = 255; i >= 0; i--)
                if (!used[i]) return i;
            return -1;
        }
    }
}
