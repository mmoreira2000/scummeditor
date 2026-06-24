using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// A generic IFF block read recursively. After its header ([tag:4][size:4 BE]) the body is
    /// inspected: if it is a sequence of well-formed sub-blocks that tile it exactly, each sub-block
    /// is parsed (and may itself be a container); otherwise the body is kept verbatim as raw bytes.
    /// Either way the block round-trips byte-for-byte, because a container writes its header plus its
    /// children and a leaf writes its header plus its raw bytes.
    ///
    /// This is the SCUMM v7 work-horse: it gives a navigable tree for the blocks the editor does not
    /// yet model (AKOS, SOUN/iMUS, SMAP, ZPxx, the v7 object headers, ...) while guaranteeing an
    /// identical rebuild, so v7 games load, navigate and save before any block gets typed support.
    /// </summary>
    public class RawContainerBlock : BlockBase, IRawContentBlock
    {
        // Deep enough for the real v7 nesting (LECF>LFLF>ROOM>OBIM>IMnn>SMAP and AKOS>...), with a
        // margin; the cap only stops a runaway recursion on pathological/misdetected data.
        private const int MaxDepth = 10;

        private readonly string _blockType;
        private readonly int _depth;

        /// <summary>The raw body bytes when this block is a leaf; null when it is a container (its
        /// bytes live in <see cref="BlockBase.Childrens"/>).</summary>
        public byte[] Contents { get; set; }

        public RawContainerBlock(BlockBase parent, string blockType) : this(parent, blockType, 0) { }

        private RawContainerBlock(BlockBase parent, string blockType, int depth) : base(parent)
        {
            _blockType = blockType;
            _depth = depth;
        }

        public override string BlockType
        {
            get { return _blockType; }
        }

        /// <summary>True when this block is stored as raw bytes (no parsed children).</summary>
        public bool IsLeaf
        {
            get { return Contents != null; }
        }

        public override void CalculateBlockSize()
        {
            if (Contents != null)
            {
                BlockSize = (uint)(HeaderLength + Contents.Length);
                return;
            }

            base.CalculateBlockSize(); // header + recursive sum of children
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader); // reads & validates the [tag][size] header

            int bodyLength = (int)BlockSize - HeaderLength;
            if (bodyLength < 0)
            {
                bodyLength = 0;
            }

            byte[] body = binaryReader.ReadBytes(bodyLength);

            if (_depth < MaxDepth && BodyTilesIntoBlocks(body))
            {
                ParseChildrenFromBody(body);
                Contents = null;
            }
            else
            {
                Contents = body;
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter); // writes the [tag][size] header

            if (Contents != null)
            {
                binaryWriter.WriteBytes(Contents);
                return;
            }

            foreach (BlockBase child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
        }

        /// <summary>
        /// Parses the body as a sequence of child blocks. The children get body-relative offsets,
        /// which is harmless: CalculateOffsets recomputes every offset from the tree on save, and only
        /// the LFLF-level blocks (parsed from the real stream) take part in index linking.
        /// </summary>
        private void ParseChildrenFromBody(byte[] body)
        {
            using (var stream = new MemoryStream(body, false))
            {
                while (stream.Position < stream.Length)
                {
                    string tag = BinaryHelper.ConvertByteArrayToUTF8String(stream.PeekBytes(4));
                    var child = new RawContainerBlock(this, tag, _depth + 1);
                    child.LoadFromBinaryReader(stream);
                    Childrens.Add(child);
                }
            }
        }

        /// <summary>
        /// True when the body is one or more well-formed sub-blocks ([tag:4][size:4 BE], size &gt;= 8)
        /// that tile it exactly. The tag must be printable text starting with a letter, so compressed
        /// pixel/codec data (SMAP, AKCD, ...) is never mistaken for a container.
        /// </summary>
        private static bool BodyTilesIntoBlocks(byte[] body)
        {
            int position = 0;
            int blockCount = 0;

            while (position + 8 <= body.Length)
            {
                if (!IsLikelyTag(body, position))
                {
                    return false;
                }

                uint size = ReadBigEndianUInt32(body, position + 4);
                if (size < 8 || position + size > body.Length)
                {
                    return false;
                }

                position += (int)size;
                blockCount++;
            }

            return blockCount >= 1 && position == body.Length;
        }

        /// <summary>A SCUMM tag is 4 bytes of printable text (letters, digits, space, underscore)
        /// whose first byte is a letter - covers tags like "RMIM", "iMUS" and "MAP ".</summary>
        private static bool IsLikelyTag(byte[] body, int offset)
        {
            for (int i = 0; i < 4; i++)
            {
                byte c = body[offset + i];
                bool isLetter = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                bool isAllowed = isLetter || (c >= '0' && c <= '9') || c == ' ' || c == '_';
                if (!isAllowed)
                {
                    return false;
                }
                if (i == 0 && !isLetter)
                {
                    return false;
                }
            }
            return true;
        }

        private static uint ReadBigEndianUInt32(byte[] data, int offset)
        {
            return (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);
        }
    }
}
