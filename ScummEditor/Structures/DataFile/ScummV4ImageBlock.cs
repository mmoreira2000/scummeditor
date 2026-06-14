using System;
using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.DataFile
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
