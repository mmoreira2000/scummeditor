using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Decodes SCUMM v8 (The Curse of Monkey Island) room background and object images. v8 keeps the
    /// SAME per-strip SMAP bitstream codec as v5/v6/v7 - only the container nesting and the headers are
    /// new - so this class just navigates the v8 tree to locate the strip data and then reuses
    /// <see cref="ImageDecoder"/> (the strip-list overload). The v8 layout (verified from real COMI bytes):
    ///   ROOM/OBIM -> IMAG -> WRAP -> {OFFS(table), SMAP -> BSTR -> WRAP(leaf)}, where the leaf is
    ///   [OFFS tag][size:BE][stripOffset:uint32 LE x numStrips][strip codec+data...], stripOffset[n]
    ///   relative to the leaf start. Palette = PALS -> WRAP -> APAL (256x3 RGB). RMHD/IMHD give the size.
    /// </summary>
    public class ScummV8ImageDecoder
    {
        /// <summary>Decodes the room background, or null when the room has no IMAG / SMAP image.</summary>
        public Bitmap DecodeBackground(RoomBlock room)
        {
            byte[] rmhd = LeafBytes(FindChild(room, "RMHD"));
            if (rmhd == null || rmhd.Length < 12) return null;
            int width = (int)ReadUInt32LE(rmhd, 4);
            int height = (int)ReadUInt32LE(rmhd, 8);

            BlockBase imag = FindChild(room, "IMAG");
            if (imag == null) return null;

            return DecodeImag(imag, width, height, ReadRoomPalette(room));
        }

        /// <summary>Decodes object image <paramref name="objectIndex"/> (in OBIM order), or null when it
        /// has no image (a hotspot-only object is just an IMHD with no IMAG).</summary>
        public Bitmap DecodeObject(RoomBlock room, int objectIndex)
        {
            List<BlockBase> obims = room.Childrens.Where(c => c.BlockType == "OBIM").ToList();
            if (objectIndex < 0 || objectIndex >= obims.Count) return null;
            BlockBase obim = obims[objectIndex];

            byte[] imhd = LeafBytes(FindChild(obim, "IMHD"));
            if (imhd == null || imhd.Length < 64) return null;
            // v8 IMHD body (ScummVM ImageHeader::v8, object.h): name[32]@0, unk_1[2]@32, version@40,
            // image_count@44, x_pos@48, y_pos@52, width@56, height@60, actordir@64, flags@68, hotspots.
            int width = (int)ReadUInt32LE(imhd, 56);
            int height = (int)ReadUInt32LE(imhd, 60);

            BlockBase imag = FindChild(obim, "IMAG");
            if (imag == null) return null; // imageless object

            return DecodeImag(imag, width, height, ReadRoomPalette(room));
        }

        /// <summary>How many object images (OBIM blocks) the room has.</summary>
        public static int ObjectCount(RoomBlock room)
        {
            return room.Childrens.Count(c => c.BlockType == "OBIM");
        }

        // -------------------------------------------------------------------------

        private static Bitmap DecodeImag(BlockBase imag, int width, int height, Color[] palette)
        {
            if (width <= 0 || height <= 0) return null;

            BlockBase wrap = FindChild(imag, "WRAP");
            BlockBase smap = FindChild(wrap, "SMAP");
            if (smap == null) return null; // BOMP-coded object images are handled separately

            // SMAP -> BSTR -> WRAP(leaf), or SMAP -> WRAP(leaf) if there is no BSTR layer.
            BlockBase bstr = FindChild(smap, "BSTR");
            BlockBase innerWrap = FindChild(bstr ?? smap, "WRAP");
            byte[] leaf = LeafBytes(innerWrap);
            if (leaf == null) return null;

            List<StripData> strips = BuildStrips(leaf, width / 8);
            if (strips == null) return null;

            return new ImageDecoder().Decode(strips, width, height, palette, -1);
        }

        /// <summary>
        /// Builds the strip list from the BSTR/WRAP leaf: an inner OFFS block ([tag][size:BE][offsets:
        /// uint32 LE]) followed by the strip codec+data. Each offset is relative to the leaf start and
        /// points at a strip's codec byte; the strip's data runs to the next offset (or the leaf end).
        /// </summary>
        private static List<StripData> BuildStrips(byte[] leaf, int expectedStrips)
        {
            if (leaf.Length < 8) return null;
            if (leaf[0] != 'O' || leaf[1] != 'F' || leaf[2] != 'F' || leaf[3] != 'S') return null;

            int offsSize = (int)ReadUInt32BE(leaf, 4);
            int numStrips = (offsSize - 8) / 4;
            if (numStrips <= 0) numStrips = expectedStrips;
            if (numStrips <= 0 || 8 + numStrips * 4 > leaf.Length) return null;

            var offsets = new int[numStrips];
            for (int i = 0; i < numStrips; i++) offsets[i] = (int)ReadUInt32LE(leaf, 8 + i * 4);

            var strips = new List<StripData>(numStrips);
            for (int i = 0; i < numStrips; i++)
            {
                int start = offsets[i];
                int end = (i + 1 < numStrips) ? offsets[i + 1] : leaf.Length;
                if (start < 0 || start >= leaf.Length || end > leaf.Length || end <= start) return null;

                var data = new byte[end - start - 1];
                Array.Copy(leaf, start + 1, data, 0, data.Length);
                strips.Add(new StripData { CodecId = leaf[start], ImageData = data });
            }
            return strips;
        }

        private static Color[] ReadRoomPalette(RoomBlock room)
        {
            var palette = new Color[256];
            byte[] apal = FindDescendantLeaf(FindChild(room, "PALS"), "APAL");
            if (apal != null && apal.Length >= 768)
            {
                for (int i = 0; i < 256; i++)
                {
                    palette[i] = Color.FromArgb(apal[i * 3], apal[i * 3 + 1], apal[i * 3 + 2]);
                }
            }
            return palette;
        }

        // -------------------------------------------------------------------------
        // v8 tree navigation (the room/object sub-blocks are RawContainerBlocks)
        // -------------------------------------------------------------------------

        private static BlockBase FindChild(BlockBase parent, string tag)
        {
            if (parent == null) return null;
            return parent.Childrens.FirstOrDefault(c => c.BlockType == tag);
        }

        private static byte[] FindDescendantLeaf(BlockBase block, string tag)
        {
            if (block == null) return null;
            if (block.BlockType == tag)
            {
                byte[] leaf = LeafBytes(block);
                if (leaf != null) return leaf;
            }
            foreach (BlockBase child in block.Childrens)
            {
                byte[] found = FindDescendantLeaf(child, tag);
                if (found != null) return found;
            }
            return null;
        }

        private static byte[] LeafBytes(BlockBase block)
        {
            var raw = block as RawContainerBlock;
            return raw != null ? raw.Contents : null;
        }

        private static uint ReadUInt32LE(byte[] b, int o)
        {
            return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
        }

        private static uint ReadUInt32BE(byte[] b, int o)
        {
            return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
        }
    }
}
