using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    A SCUMM v4 image block: the room background ("BM") or an object image ("OI").

    Unlike v5/v6 - which nest the image inside RMIM/IM00/SMAP (or OBIM/IMnn/SMAP) sub-blocks -
    a v4 image is a single flat block. After the 6-byte small header the body is the strip table:

        BM body:  [strip table]
        OI body:  [obj id:2 LE][strip table]

    The strip table itself is read by ScummV4ImageDecoder (its VGA/EGA layout depends on the
    graphics edition, which is not known per-block). Here we only keep the raw body verbatim so
    the file always round-trips byte-for-byte, and expose the object id and where the strip table
    starts within the body.
    */
    public class ScummV4ImageBlock : BlockBase
    {
        private readonly string _blockType;

        public ScummV4ImageBlock(BlockBase blockBase, string blockType) : base(blockBase)
        {
            _blockType = blockType;
        }

        /// <summary>The body bytes after the 6-byte small header (kept verbatim for byte-exact save).</summary>
        public byte[] Contents { get; set; }

        /// <summary>Object id (OI only); matches the id in the paired OC block. Zero for a room "BM".</summary>
        public ushort ObjectId { get; private set; }

        /// <summary>The body length as originally loaded; lets the save-time index fix-up tell which images were edited.</summary>
        public int OriginalContentLength { get; private set; }

        public override string BlockType
        {
            get { return _blockType; }
        }

        /// <summary>Index within <see cref="Contents"/> where the strip table begins (after the OI object id).</summary>
        public int StripTableStart
        {
            get { return _blockType == "OI" ? 2 : 0; }
        }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)Contents.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            Contents = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));
            OriginalContentLength = Contents.Length;

            if (_blockType == "OI" && Contents.Length >= 2)
            {
                ObjectId = (ushort)(Contents[0] | (Contents[1] << 8));
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(Contents);
        }

        /// <summary>
        /// Replaces the image strips of a VGA (256-color) block with newly encoded ones, keeping the
        /// trailing z-planes verbatim. The body becomes:
        ///   [OI obj id (2 bytes, OI only)] [smapLen:LE32] [numStrips x offset:LE32] [codec+data per strip] [z-planes].
        /// Offsets are relative to the strip-table base (the smapLen position); the last strip ends at smapLen.
        /// </summary>
        public void RebuildVgaContents(List<StripData> strips)
        {
            int baseIndex = StripTableStart;
            byte[] zPlaneTail = ExtractZPlaneTail(ReadOriginalSmapLen(baseIndex, fourByte: true), baseIndex);

            int offsetTableSize = 4 + strips.Count * 4;
            int stripDataSize = 0;
            foreach (StripData strip in strips)
            {
                stripDataSize += 1 + strip.ImageData.Length; // codec byte + data
            }
            uint smapLen = (uint)(offsetTableSize + stripDataSize);

            using (var stream = new MemoryStream())
            {
                WritePrefix(stream, baseIndex);
                WriteUInt32(stream, smapLen);

                uint offset = (uint)offsetTableSize;
                foreach (StripData strip in strips)
                {
                    WriteUInt32(stream, offset);
                    offset += (uint)(1 + strip.ImageData.Length);
                }

                foreach (StripData strip in strips)
                {
                    stream.WriteByte(strip.CodecId);
                    stream.Write(strip.ImageData, 0, strip.ImageData.Length);
                }

                stream.Write(zPlaneTail, 0, zPlaneTail.Length);
                Contents = stream.ToArray();
            }
        }

        /// <summary>
        /// Replaces the image strips of an EGA (16-color) block with newly encoded raw RLE strips
        /// (no codec byte), keeping the trailing z-planes verbatim. The body becomes:
        ///   [OI obj id (OI only)] [smapLen:LE16] [numStrips x offset:LE16] [raw strip bytes] [z-planes].
        /// </summary>
        public void RebuildEgaContents(List<byte[]> rawStrips)
        {
            int baseIndex = StripTableStart;
            byte[] zPlaneTail = ExtractZPlaneTail(ReadOriginalSmapLen(baseIndex, fourByte: false), baseIndex);

            int offsetTableSize = 2 + rawStrips.Count * 2;
            int stripDataSize = 0;
            foreach (byte[] strip in rawStrips)
            {
                stripDataSize += strip.Length;
            }
            ushort smapLen = (ushort)(offsetTableSize + stripDataSize);

            using (var stream = new MemoryStream())
            {
                WritePrefix(stream, baseIndex);
                WriteUInt16(stream, smapLen);

                ushort offset = (ushort)offsetTableSize;
                foreach (byte[] strip in rawStrips)
                {
                    WriteUInt16(stream, offset);
                    offset += (ushort)strip.Length;
                }

                foreach (byte[] strip in rawStrips)
                {
                    stream.Write(strip, 0, strip.Length);
                }

                stream.Write(zPlaneTail, 0, zPlaneTail.Length);
                Contents = stream.ToArray();
            }
        }

        /// <summary>
        /// Locates the z-plane (mask) regions embedded after the image strips. v4 has no ZPnn
        /// sub-blocks: the z-planes follow the strip region (at base+smapLen). v4 GF_SMALL_HEADER
        /// chains them by a leading LE16 "size to next z-plane" word, ending at a zero word; v3
        /// GF_OLD256 reserves a single plane prefixed by a LE32 size word (0 == no plane). Returns
        /// each z-plane's (start, length) within <see cref="Contents"/>.
        /// </summary>
        public List<(int Start, int Length)> GetZPlaneRegions(int numStrips, bool isEga)
        {
            var regions = new List<(int, int)>();
            if (numStrips <= 0)
            {
                return regions;
            }

            int baseIndex = StripTableStart;
            int smapLen = (int)ReadOriginalSmapLen(baseIndex, fourByte: !isEga);
            int zp = baseIndex + smapLen;

            if (IsOld256ZPlane)
            {
                // GF_OLD256 (v3 "small") reserves exactly one walk-behind plane - ScummVM fixes
                // _numZBuffer at 2 for all v3 (gfx.cpp:1039) and never walks a chain. The plane is
                // prefixed by a LE32 size word that doubles as the presence flag: 0 means the image
                // genuinely has no z-plane (gfx.cpp:2286).
                int headerSize = 4 + numStrips * 2;
                if (zp + headerSize > Contents.Length)
                {
                    return regions;
                }
                int size = (int)ReadUInt32At(zp);
                if (size < headerSize || zp + size > Contents.Length)
                {
                    return regions; // size 0 (no plane) or a value that cannot be a real region
                }
                regions.Add((zp, size));
                return regions;
            }

            // v4 GF_SMALL_HEADER: z-planes are chained by a leading LE16 "size to next" word.
            int offsetTableSize = 2 + numStrips * 2;
            while (zp + offsetTableSize <= Contents.Length)
            {
                int delta = ReadUInt16At(zp);
                if (delta < offsetTableSize || zp + delta > Contents.Length)
                {
                    break; // a zero word terminates the chain; anything too small/large is not a z-plane
                }
                regions.Add((zp, delta));
                zp += delta;
            }
            return regions;
        }

        /// <summary>
        /// Parses one z-plane into its mask strips. The strip-offset table sits at zpStart + header size
        /// (2 for v4, 4 for v3 GF_OLD256), with width/8 LE16 offsets relative to zpStart; an offset of 0
        /// marks an empty (fully unmasked) strip.
        /// </summary>
        public List<ZPlaneStripData> GetZPlaneStrips(int zpStart, int delta, int numStrips)
        {
            int headerSize = ZPlaneHeaderSize;
            var strips = new List<ZPlaneStripData>(numStrips);
            for (int n = 0; n < numStrips; n++)
            {
                int start = ReadUInt16At(zpStart + headerSize + n * 2);
                if (start == 0)
                {
                    strips.Add(new ZPlaneStripData { OffSet = 0, ImageData = new byte[0] });
                    continue;
                }

                // The mask decoder self-terminates at the strip height, so a strip is given every byte
                // from its offset to the end of the z-plane. Strips are NOT bounded by the next offset:
                // ScummVM's decodeMask reads from zplane+offset until "height" rows, and some strips
                // legitimately read past the following offset (shared / overlapping run data).
                int length = delta - start;
                if (length < 0 || zpStart + start + length > Contents.Length) length = 0;

                var data = new byte[length];
                if (length > 0) Array.Copy(Contents, zpStart + start, data, 0, length);
                strips.Add(new ZPlaneStripData { OffSet = (ushort)start, ImageData = data });
            }
            return strips;
        }

        /// <summary>
        /// Replaces the z-plane at [zpStart, zpStart+oldLength) with newly encoded mask strips, keeping
        /// the image and any other z-planes unchanged. The rebuilt z-plane is
        ///   [size:LE16 (v4) or LE32 (v3 GF_OLD256)][numStrips x offset:LE16][strip mask data...]
        /// with offset 0 for empty strips.
        /// </summary>
        public void RebuildZPlane(int zpStart, int oldLength, List<ZPlaneStripData> strips)
        {
            int headerSize = ZPlaneHeaderSize;
            int offsetTableSize = headerSize + strips.Count * 2;
            var offsets = new int[strips.Count];
            int running = offsetTableSize;
            for (int n = 0; n < strips.Count; n++)
            {
                byte[] data = strips[n].ImageData;
                if (data == null || data.Length == 0)
                {
                    offsets[n] = 0;
                }
                else
                {
                    offsets[n] = running;
                    running += data.Length;
                }
            }
            int newLength = running; // size-to-next == total z-plane size

            byte[] zPlaneBytes;
            using (var stream = new MemoryStream())
            {
                // The header carries the total z-plane size: a LE32 word for v3 GF_OLD256, a LE16
                // "size to next" word for v4 GF_SMALL_HEADER.
                if (headerSize == 4)
                {
                    WriteUInt32(stream, (uint)newLength);
                }
                else
                {
                    WriteUInt16(stream, (ushort)newLength);
                }
                for (int n = 0; n < strips.Count; n++)
                {
                    WriteUInt16(stream, (ushort)offsets[n]);
                }
                foreach (ZPlaneStripData strip in strips)
                {
                    if (strip.ImageData != null && strip.ImageData.Length > 0)
                    {
                        stream.Write(strip.ImageData, 0, strip.ImageData.Length);
                    }
                }
                zPlaneBytes = stream.ToArray();
            }

            int tailStart = zpStart + oldLength;
            var newContents = new byte[zpStart + zPlaneBytes.Length + (Contents.Length - tailStart)];
            Array.Copy(Contents, 0, newContents, 0, zpStart);
            Array.Copy(zPlaneBytes, 0, newContents, zpStart, zPlaneBytes.Length);
            Array.Copy(Contents, tailStart, newContents, zpStart + zPlaneBytes.Length, Contents.Length - tailStart);
            Contents = newContents;
        }

        private int ReadUInt16At(int index)
        {
            return Contents[index] | (Contents[index + 1] << 8);
        }

        private uint ReadUInt32At(int index)
        {
            return (uint)(Contents[index] | (Contents[index + 1] << 8)
                         | (Contents[index + 2] << 16) | (Contents[index + 3] << 24));
        }

        /// <summary>
        /// True for the v3 "small" GF_OLD256 games (Indy3 VGA, Zak/Loom FM-Towns), whose z-plane layout
        /// differs from v4. Decided by the loaded ScummVersion, exactly like the FM-Towns codec branch in
        /// <see cref="ScummEditor.Engine.Encoders.ScummV4ImageDecoder"/>.
        /// </summary>
        private bool IsOld256ZPlane
        {
            get { return GameInfo != null && GameInfo.ScummVersion == 3; }
        }

        /// <summary>
        /// Bytes a z-plane reserves before its per-strip offset table. v4 GF_SMALL_HEADER prefixes each
        /// z-plane with a LE16 "size to next" word, so the table starts 2 bytes in (ScummVM gfx.cpp:2617).
        /// v3 GF_OLD256 widens that to a LE32 size word, so the table starts 4 bytes in (gfx.cpp:2615).
        /// </summary>
        private int ZPlaneHeaderSize
        {
            get { return IsOld256ZPlane ? 4 : 2; }
        }

        private uint ReadOriginalSmapLen(int baseIndex, bool fourByte)
        {
            if (fourByte)
            {
                if (Contents.Length < baseIndex + 4) return 0;
                return (uint)(Contents[baseIndex] | (Contents[baseIndex + 1] << 8)
                             | (Contents[baseIndex + 2] << 16) | (Contents[baseIndex + 3] << 24));
            }
            if (Contents.Length < baseIndex + 2) return 0;
            return (ushort)(Contents[baseIndex] | (Contents[baseIndex + 1] << 8));
        }

        /// <summary>The bytes after the strip region (z-planes), to be re-appended unchanged.</summary>
        private byte[] ExtractZPlaneTail(uint originalSmapLen, int baseIndex)
        {
            int zPlaneStart = baseIndex + (int)originalSmapLen;
            if (originalSmapLen == 0 || zPlaneStart >= Contents.Length)
            {
                return new byte[0];
            }
            var tail = new byte[Contents.Length - zPlaneStart];
            Array.Copy(Contents, zPlaneStart, tail, 0, tail.Length);
            return tail;
        }

        private void WritePrefix(Stream stream, int baseIndex)
        {
            // OI keeps its 2-byte object id ahead of the strip table; BM has no prefix.
            if (baseIndex == 2)
            {
                stream.WriteByte(Contents[0]);
                stream.WriteByte(Contents[1]);
            }
        }

        private static void WriteUInt32(Stream stream, uint value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private static void WriteUInt16(Stream stream, ushort value)
        {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
        }
    }
}
