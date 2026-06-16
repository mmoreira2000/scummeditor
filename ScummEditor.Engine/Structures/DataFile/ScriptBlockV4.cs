using System.IO;
using ScummEditor.Encoders;

namespace ScummEditor.Structures.DataFile
{
    /*
    Script blocks holding SCUMM v4 bytecode (the v4 small-header equivalents of the v5/v6
    ScriptBlock types):
      SC - global script  (= SCRP, child of the LF disk block)
      LS - local script   (= LSCR, child of RO; begins with a 1-byte script id, then the bytecode)
      EX - exit script    (= EXCD)
      EN - entry script   (= ENCD)

    v4 shares the v5 (parameter-bit) opcode language, so the bytecode is disassembled by
    Scumm5Disassembler. Like the v5/v6 ScriptBlock this is a read-only view: the body bytes are
    kept verbatim and written back unchanged on save, so the container always round-trips
    byte-identical until the text pipeline deliberately rewrites a string.
    */
    public class ScriptBlockV4 : BlockBase, IScriptBytecode
    {
        private readonly string _blockType;

        public ScriptBlockV4(BlockBase blockBase, string blockType)
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

        /// <summary>Local-script id (LS only); -1 for the other script types.</summary>
        public int ScriptId { get; private set; }

        /// <summary>Offset of the bytecode within RawContent (1 for LS, 0 otherwise).</summary>
        public int CodeOffset { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            // v4 uses the 6-byte small header (BlockSize includes it), unlike the v5/v6 8-byte header.
            RawContent = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));

            if (_blockType == "LS" && RawContent.Length > 0)
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

        public ScummV6Disassembler.Result Disassemble()
        {
            // v4 bytecode is the parameter-bit (pre-stack) language; ScummV4Disassembler is the v5
            // decoder with the v4 opcode deltas applied. The result shape is shared with v6.
            return ScummV4Disassembler.Disassemble(RawContent, CodeOffset);
        }
    }
}
