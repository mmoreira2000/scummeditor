using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ScummEditor.Exceptions;

namespace ScummEditor.Structures
{
    /*
    The block header has two layouts depending on the SCUMM version:
      - v5/v6 ("big header"):   Block tag (4 ASCII bytes), then Block size (4 bytes, big-endian).
      - v4    ("small header"): Block size (4 bytes, little-endian), then Block tag (2 ASCII bytes).
    In both layouts the size INCLUDES the header itself.
    */

    public abstract class BlockBase : IBinaryPersistence
    {
        protected readonly GameInfo _gameInfo;

        /// <summary>Info of the loaded game (SCUMM version etc.); used to pick version-specific code paths.</summary>
        public GameInfo GameInfo
        {
            get { return _gameInfo; }
        }

        /// <summary>True for SCUMM v4, which uses the "small header" (size-first LE, 2-char tag).</summary>
        protected bool IsSmallHeader
        {
            get { return _gameInfo != null && _gameInfo.ScummVersion == 4; }
        }

        /// <summary>
        /// Size in bytes of this block's header = tag bytes + the 4-byte size field. It is 8 for
        /// the v5/v6 4-char tags and 6 for the v4 2-char tags, derived from the tag length so the
        /// same arithmetic works for both layouts.
        /// </summary>
        public int HeaderLength
        {
            get { return BinaryHelper.ConvertUTF8StringToByteArray(BlockType).Length + 4; }
        }

        /// <summary>
        /// Reads the tag of the block at the current stream position without consuming it. For v4
        /// the tag sits after the 4-byte size, so 6 bytes are peeked and the last 2 are the tag.
        /// </summary>
        public static string PeekTag(Stream binaryReader, GameInfo gameInfo)
        {
            if (gameInfo != null && gameInfo.ScummVersion == 4)
            {
                byte[] head = binaryReader.PeekBytes(6);
                return BinaryHelper.ConvertByteArrayToUTF8String(new[] { head[4], head[5] });
            }

            return BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));
        }

        public List<BlockBase> Childrens { get; set; }

        public abstract string BlockType { get; }
        private uint _blockSize;
        public uint _firstBlockSize;

        //I created this uniqueid only to make life easier
        //on sincronize blocks with index files when reconstructinh the files.
        public string UniqueId { get; private set; }

        public BlockBase Parent { get; set; }

        protected BlockBase(BlockBase parent):this(parent,parent._gameInfo){}

        protected BlockBase(BlockBase parent, GameInfo gameInfo)
        {
            Parent = parent;
            _gameInfo = gameInfo;

            UniqueId = Guid.NewGuid().ToString();
            Childrens = new List<BlockBase>();
        }

        public uint BlockSize
        {
            get { return _blockSize; }
            set
            {
                if (_firstBlockSize == 0)
                {
                    _blockSize = value;
                    _firstBlockSize = value;
                }
                else
                {
                    //if (value != _firstBlockSize) Debugger.Break();
                }
                _blockSize = value;
            }
        }

        //Debug pourpose only, not used in the final file, at least for now.
        private long _blockOffSet;



        public long BlockOffSet
        {
            get { return _blockOffSet; }
            set
            {
                //if (_blockOffSet > 0 && _blockOffSet != value) Debugger.Break();
                _blockOffSet = value;
            }
        }

        public virtual void CalculateBlockSize()
        {
            uint block = (uint)(BinaryHelper.ConvertUTF8StringToByteArray(BlockType).Length + 4);

            Childrens.ForEach(b => b.CalculateBlockSize());
            Childrens.ForEach(b => block += b.BlockSize);

            _blockSize = block;
        }

        public virtual void CalculateOffsets()
        {
            long nextOffSet = BlockOffSet + HeaderLength;

            foreach (BlockBase child in Childrens)
            {
                nextOffSet = ConfigureAndReturnNextOffset(child, nextOffSet);
                child.CalculateOffsets();
            }
        }

        protected long ConfigureAndReturnNextOffset(BlockBase blockBase, long currentOffSet)
        {
            blockBase.BlockOffSet = currentOffSet;
            return currentOffSet + blockBase._blockSize;
        }

        public virtual void LoadFromBinaryReader(Stream binaryReader)
        {
            ReadBlockHeader(binaryReader);
        }

        /// <summary>Reads (and validates) the block header in the layout of the loaded SCUMM version.</summary>
        protected void ReadBlockHeader(Stream binaryReader)
        {
            BlockOffSet = binaryReader.Position;

            string typeRead;
            if (IsSmallHeader)
            {
                BlockSize = binaryReader.ReadUint32(false); // little-endian size, first
                typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.ReadBytes(2));
            }
            else
            {
                typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.ReadBytes(4));
                BlockSize = binaryReader.ReadUint32(true); // big-endian size, second
            }

            if (BlockType != typeRead)
            {
                throw new InvalidFileFormatException(string.Format("Unexpected block tag: expected '{0}' but found '{1}'.", BlockType, typeRead));
            }
        }

        public virtual void SaveToBinaryWriter(Stream binaryWriter)
        {
            WriteBlockHeader(binaryWriter);
        }

        /// <summary>Writes the block header in the layout of the loaded SCUMM version.</summary>
        protected void WriteBlockHeader(Stream binaryWriter)
        {
            byte[] tagBytes = BinaryHelper.ConvertUTF8StringToByteArray(BlockType);

            if (IsSmallHeader)
            {
                if (tagBytes.Length != 2)
                {
                    throw new InvalidFileFormatException(string.Format("v4 block type must be 2 chars, got '{0}'.", BlockType));
                }
                binaryWriter.Write(BlockSize, false); // little-endian size, first
                binaryWriter.Write(tagBytes);
            }
            else
            {
                if (tagBytes.Length != 4)
                {
                    throw new InvalidFileFormatException(string.Format("Block type with invalid length ({0}).", tagBytes.Length));
                }
                binaryWriter.Write(tagBytes);
                binaryWriter.Write(BlockSize, true); // big-endian size, second
            }
        }


    }
}