using System.IO;
using ScummEditor.Encoders;

namespace ScummEditor.Structures.DataFile
{
    /*
    Script blocks holding SCUMM v6 bytecode:
      SCRP - global script
      LSCR - local script (room-scoped); begins with a 1-byte script id, then the bytecode
      EXCD - exit script  (runs when leaving the room)
      ENCD - entry script (runs when entering the room)

    The bytecode is disassembled on demand by Scumm6Disassembler. This is a read-only view:
    the original bytes are kept and written back verbatim on save, so rebuilding the game file
    is always byte-identical.
    */
    public class ScriptBlock : BlockBase
    {
        private readonly string _blockType;

        public ScriptBlock(BlockBase blockBase, string blockType)
            : base(blockBase)
        {
            _blockType = blockType;
            ScriptId = -1;
        }

        public override string BlockType
        {
            get { return _blockType; }
        }

        public byte[] RawContent { get; set; }

        /// <summary>Local-script id (LSCR only); -1 for the other script types.</summary>
        public int ScriptId { get; private set; }

        /// <summary>Offset of the bytecode within RawContent (1 for LSCR, 0 otherwise).</summary>
        public int CodeOffset { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - 8));

            if (_blockType == "LSCR" && RawContent.Length > 0)
            {
                ScriptId = RawContent[0];
                CodeOffset = 1;
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }

        public Scumm6Disassembler.Result Disassemble()
        {
            // The bytecode language changed at v6: v5 opcodes carry parameter bits, v6 is
            // stack based. The result shape (listing + string/jump positions) is shared.
            if (_gameInfo != null && _gameInfo.ScummVersion == 5)
            {
                return Scumm5Disassembler.Disassemble(RawContent, CodeOffset);
            }
            return Scumm6Disassembler.Disassemble(RawContent, CodeOffset);
        }
    }
}
