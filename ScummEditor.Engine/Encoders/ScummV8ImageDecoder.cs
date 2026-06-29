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

            // SMAP-coded object (the common case).
            if (FindStripLeaf(imag) != null) return DecodeImag(imag, width, height, ReadRoomPalette(room));

            // BOMP-coded object (verb GUI, cursors, overlays): IMAG->WRAP->{OFFS, BOMP[state]...}, the BOMP
            // is a direct child of WRAP (not wrapped in BSTR) and carries its own [w:4 LE][h:4 LE] header.
            RawContainerBlock bomp = FindBompLeaf(imag, 0);
            if (bomp != null) return DecodeBomp(bomp.Contents, ReadRoomPalette(room));

            return null;
        }

        /// <summary>Decodes a v8 BOMP chunk body ([w:4 LE][h:4 LE][RLE]) to a bitmap via the shared BOMP codec.</summary>
        private static Bitmap DecodeBomp(byte[] bomp, Color[] palette)
        {
            if (bomp == null || bomp.Length < 8) return null;
            int w = (int)ReadUInt32LE(bomp, 0);
            int h = (int)ReadUInt32LE(bomp, 4);
            if (w <= 0 || h <= 0 || w > 8192 || h > 8192) return null;
            var data = new byte[bomp.Length - 8];
            Array.Copy(bomp, 8, data, 0, data.Length);
            byte[,] matrix = BompImageDecoder.DecodeIndexMatrix(data, w, h);
            return IndexedImageHelper.FromIndexMatrix(matrix, palette, -1);
        }

        /// <summary>How many object images (OBIM blocks) the room has.</summary>
        public static int ObjectCount(RoomBlock room)
        {
            return room.Childrens.Count(c => c.BlockType == "OBIM");
        }

        // -------------------------------------------------------------------------
        // Z-planes (occlusion masks). v8 nests them ROOM/OBIM -> IMAG -> WRAP -> SMAP -> ZPLN -> WRAP ->
        // {OFFS(one entry per ZSTR), ZSTR x numZBuffer}; each ZSTR -> WRAP -> leaf[inner OFFS + mask strips],
        // the leaf laid out exactly like the SMAP strip leaf but the strips are 1-bit mask RLE (no codec
        // byte) decoded by the shared ZPlaneDecoder. numZBuffer (RMHD body+16) == the ZSTR count.
        // -------------------------------------------------------------------------

        /// <summary>Number of z-planes on the room background (0 when none / an empty placeholder).</summary>
        public int CountBackgroundZPlanes(RoomBlock room)
        {
            return CountZPlanes(FindChild(room, "IMAG"));
        }

        /// <summary>Decodes background z-plane <paramref name="z"/> as a 1-bit mask (black=masked), or null.</summary>
        public Bitmap DecodeBackgroundZPlane(RoomBlock room, int z)
        {
            byte[] rmhd = LeafBytes(FindChild(room, "RMHD"));
            if (rmhd == null || rmhd.Length < 12) return null;
            int width = (int)ReadUInt32LE(rmhd, 4);
            int height = (int)ReadUInt32LE(rmhd, 8);
            return DecodeZPlane(FindChild(room, "IMAG"), z, width, height);
        }

        /// <summary>Number of z-planes on object image <paramref name="objectIndex"/>.</summary>
        public int CountObjectZPlanes(RoomBlock room, int objectIndex)
        {
            List<BlockBase> obims = room.Childrens.Where(c => c.BlockType == "OBIM").ToList();
            if (objectIndex < 0 || objectIndex >= obims.Count) return 0;
            return CountZPlanes(FindChild(obims[objectIndex], "IMAG"));
        }

        /// <summary>Decodes object z-plane <paramref name="z"/> as a 1-bit mask (black=masked), or null.</summary>
        public Bitmap DecodeObjectZPlane(RoomBlock room, int objectIndex, int z)
        {
            List<BlockBase> obims = room.Childrens.Where(c => c.BlockType == "OBIM").ToList();
            if (objectIndex < 0 || objectIndex >= obims.Count) return null;
            byte[] imhd = LeafBytes(FindChild(obims[objectIndex], "IMHD"));
            if (imhd == null || imhd.Length < 64) return null;
            int width = (int)ReadUInt32LE(imhd, 56);
            int height = (int)ReadUInt32LE(imhd, 60);
            return DecodeZPlane(FindChild(obims[objectIndex], "IMAG"), z, width, height);
        }

        private static Bitmap DecodeZPlane(BlockBase imag, int z, int width, int height)
        {
            if (imag == null || width <= 0 || height <= 0) return null;
            RawContainerBlock leaf = FindZStrLeaf(imag, z);
            if (leaf == null || leaf.Contents == null) return null;
            List<ZPlaneStripData> strips = BuildZPlaneStrips(leaf.Contents, width / 8);
            if (strips == null) return null;
            return new ZPlaneDecoder().Decode(strips, width, height);
        }

        /// <summary>Builds z-plane strips from a ZSTR leaf ([inner OFFS][mask strips]); the strips are pure
        /// mask RLE (no codec byte), unlike the SMAP strip leaf.</summary>
        private static List<ZPlaneStripData> BuildZPlaneStrips(byte[] leaf, int expectedStrips)
        {
            if (leaf.Length < 8 || leaf[0] != 'O' || leaf[1] != 'F' || leaf[2] != 'F' || leaf[3] != 'S') return null;
            int offsSize = (int)ReadUInt32BE(leaf, 4);
            int numStrips = (offsSize - 8) / 4;
            if (numStrips <= 0) numStrips = expectedStrips;
            if (numStrips <= 0 || 8 + numStrips * 4 > leaf.Length) return null;

            var offsets = new int[numStrips];
            for (int i = 0; i < numStrips; i++) offsets[i] = (int)ReadUInt32LE(leaf, 8 + i * 4);

            var strips = new List<ZPlaneStripData>(numStrips);
            for (int i = 0; i < numStrips; i++)
            {
                int start = offsets[i];
                int end = (i + 1 < numStrips) ? offsets[i + 1] : leaf.Length;
                if (start < 0 || start > leaf.Length || end > leaf.Length || end < start) return null;
                var data = new byte[end - start];
                Array.Copy(leaf, start, data, 0, data.Length);
                strips.Add(new ZPlaneStripData { ImageData = data });
            }
            return strips;
        }

        // -------------------------------------------------------------------------

        private static Bitmap DecodeImag(BlockBase imag, int width, int height, Color[] palette)
        {
            if (width <= 0 || height <= 0) return null;

            RawContainerBlock innerWrap = FindStripLeaf(imag);
            byte[] leaf = innerWrap != null ? innerWrap.Contents : null;
            if (leaf == null) return null; // no SMAP bitmap (e.g. BOMP image or an empty/z-plane-only IMAG)

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

        internal static BlockBase FindChild(BlockBase parent, string tag)
        {
            if (parent == null) return null;
            return parent.Childrens.FirstOrDefault(c => c.BlockType == tag);
        }

        /// <summary>Navigates ROOM/OBIM IMAG to the BSTR/WRAP leaf that holds the OFFS strip table + strips
        /// (or null when the IMAG carries no SMAP bitmap). Shared with the encoder.</summary>
        internal static RawContainerBlock FindStripLeaf(BlockBase imag)
        {
            BlockBase wrap = FindChild(imag, "WRAP");
            BlockBase smap = FindChild(wrap, "SMAP");
            if (smap == null) return null;
            BlockBase bstr = FindChild(smap, "BSTR");
            return FindChild(bstr ?? smap, "WRAP") as RawContainerBlock;
        }

        /// <summary>The <paramref name="state"/>-th BOMP chunk directly under IMAG/WRAP (objects use one BOMP
        /// per image state, siblings of the OFFS table), or null when the IMAG carries no BOMP. Shared with
        /// the encoder.</summary>
        internal static RawContainerBlock FindBompLeaf(BlockBase imag, int state)
        {
            BlockBase wrap = FindChild(imag, "WRAP");
            if (wrap == null) return null;
            int idx = 0;
            foreach (BlockBase c in wrap.Childrens)
            {
                if (c.BlockType != "BOMP") continue;
                if (idx == state) return c as RawContainerBlock;
                idx++;
            }
            return null;
        }

        /// <summary>The ZPLN's inner WRAP (holding the OFFS table + one ZSTR per z-plane) for the first SMAP
        /// under IMAG, or null when there is no z-plane. Shared with the encoder.</summary>
        internal static BlockBase FindZPlaneWrap(BlockBase imag)
        {
            BlockBase wrap = FindChild(imag, "WRAP");
            BlockBase smap = FindChild(wrap, "SMAP");
            BlockBase zpln = FindChild(smap, "ZPLN");
            return FindChild(zpln, "WRAP");
        }

        /// <summary>The number of z-planes (ZSTR blocks) under the first SMAP's ZPLN, or 0.</summary>
        internal static int CountZPlanes(BlockBase imag)
        {
            BlockBase zwrap = FindZPlaneWrap(imag);
            return zwrap == null ? 0 : zwrap.Childrens.Count(c => c.BlockType == "ZSTR");
        }

        /// <summary>The leaf (ZSTR->WRAP content) of z-plane <paramref name="z"/>, or null. Shared with the encoder.</summary>
        internal static RawContainerBlock FindZStrLeaf(BlockBase imag, int z)
        {
            BlockBase zwrap = FindZPlaneWrap(imag);
            if (zwrap == null) return null;
            int idx = 0;
            foreach (BlockBase c in zwrap.Childrens)
            {
                if (c.BlockType != "ZSTR") continue;
                if (idx == z) return FindChild(c, "WRAP") as RawContainerBlock;
                idx++;
            }
            return null;
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

        internal static byte[] LeafBytes(BlockBase block)
        {
            var raw = block as RawContainerBlock;
            return raw != null ? raw.Contents : null;
        }

        internal static uint ReadUInt32LE(byte[] b, int o)
        {
            return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
        }

        private static uint ReadUInt32BE(byte[] b, int o)
        {
            return (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);
        }
    }
}
