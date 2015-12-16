using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ScummEditor.Exceptions;

namespace ScummEditor.Structures
{
    /*
    Block Name	  (4 bytes)
    Block Size	  (4 bytes BE)
    */

    public abstract class BlockBase : IBinaryPersistence
    {
        protected readonly GameInfo _gameInfo;

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
            long nextOffSet = BlockOffSet + 8;

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
            //if (binaryReader.Position == 3091223) Debugger.Break();

            BlockOffSet = binaryReader.Position;
            string typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.ReadBytes(4));

            if (BlockType != typeRead)
            {
                throw new InvalidFileFormatException(string.Format("Sequencia de caracteres não esperada. Esperado '{0}' mas veio '{1}'", BlockType, typeRead));
            }

            BlockSize = binaryReader.ReadUint32(true);
        }

        public virtual void SaveToBinaryWriter(Stream binaryWriter)
        {
            byte[] headerBytes = BinaryHelper.ConvertUTF8StringToByteArray(BlockType);
            if (headerBytes.Length != 4)
            {
                throw new InvalidFileFormatException(string.Format("BlockType com tamanho inválido ({0}).", headerBytes.Length));
            }
            binaryWriter.Write(headerBytes);

            binaryWriter.Write(BlockSize, true);
        }


    }
}