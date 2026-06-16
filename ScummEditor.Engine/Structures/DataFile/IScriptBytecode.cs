using ScummEditor.Encoders;

namespace ScummEditor.Structures.DataFile
{
    /// <summary>
    /// A block whose body is SCUMM script bytecode that the text pipeline and the script viewer can
    /// read. Implemented by both ScriptBlock (v5/v6) and ScriptBlockV4, so the shared extract/import
    /// code and ScriptControl can drive either without caring about the container version.
    /// Implementers are also BlockBase (the disassembler needs the block as context for its GameInfo).
    /// </summary>
    public interface IScriptBytecode
    {
        /// <summary>The raw block body (the bytecode, after any leading script-id byte).</summary>
        byte[] RawContent { get; set; }

        /// <summary>Offset of the bytecode within RawContent (1 for a local script, 0 otherwise).</summary>
        int CodeOffset { get; }

        /// <summary>Local-script id (LSCR/LS only); -1 for the other script types.</summary>
        int ScriptId { get; }

        /// <summary>Disassembles the bytecode with the engine for this block's SCUMM version.</summary>
        ScummV6Disassembler.Result Disassemble();
    }
}
