using System.Collections.Generic;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Disassembles SCUMM v4 bytecode (Monkey Island 1 floppy, Loom CD). v4 shares the v5
    /// parameter-bit opcode language, so this forks Scumm5Disassembler and only overrides the
    /// handful of opcodes ScummEngine_v4 redefines (scummvm engines/scumm/script_v4.cpp). Every
    /// other opcode is delegated to the v5 base unchanged.
    ///
    /// The deltas that matter for text extraction are the ones the v5 decoder would mis-handle and
    /// then stop on (leaving the rest of the script - and its strings - undecoded):
    ///   0x0F/4F/8F/CF ifState, 0x2F/6F/AF/EF ifNotState  (v5 has these as getObjectState / undefined)
    ///   0x50/D0 pickupObject (one word; v5 has no opcode here)
    ///   0x25/45/65/A5/C5/E5 drawObject (v5 reads 0x25.. as pickupObject(obj,room))
    ///   0x5C/DC oldRoomEffect (absent in v5)
    ///   0xA7 saveLoadVars (v5 emits a zero-byte dummy(), desyncing everything after it)
    /// 0x22/A2 (saveLoadGame) has the SAME byte layout as the v5 getAnimCounter it reuses, so it is
    /// left to the base - only its emitted name would differ, which does not affect decoding.
    /// </summary>
    public class ScummV4Disassembler : Scumm5Disassembler
    {
        public static new ScummV6Disassembler.Result Disassemble(byte[] code, int startOffset)
        {
            return Disassemble(code, startOffset, null);
        }

        public static new ScummV6Disassembler.Result Disassemble(byte[] code, int startOffset, IDictionary<int, string> namedLabels)
        {
            return new ScummV4Disassembler().RunDisassembly(code, startOffset, namedLabels);
        }

        // Selects the v4 operand layouts in the shared base handlers (actorOps numbering + scale,
        // drawObject, roomOps color/palette).
        protected override bool SmallHeader { get { return true; } }

        protected override void Decode(byte op, int offset)
        {
            switch (op)
            {
                case 0x0F: case 0x4F: case 0x8F: case 0xCF: // ifState
                    IfState(offset, false);
                    break;

                case 0x2F: case 0x6F: case 0xAF: case 0xEF: // ifNotState
                    IfState(offset, true);
                    break;

                case 0x50: case 0xD0: // pickupObject (v4 takes only the object id)
                {
                    string obj = GetVarOrDirectWord(0x80);
                    Emit(offset, "pickupObject(" + obj + ");");
                    break;
                }

                case 0x25: case 0x45: case 0x65: case 0xA5: case 0xC5: case 0xE5: // drawObject
                    DrawObject(offset);
                    break;

                case 0x5C: case 0xDC: // oldRoomEffect
                    OldRoomEffect(offset);
                    break;

                case 0xA7: // saveLoadVars (a sub-op block; NOT the v5 zero-byte dummy)
                    SaveLoadVars(offset);
                    break;

                default:
                    base.Decode(op, offset);
                    break;
            }
        }

        /// <summary>
        /// ifState/ifNotState: read an object word (bit 0x80) and a state byte (bit 0x40), then a
        /// relative jump (scummvm o4_ifState / o4_ifNotState). The engine jumps when the body should
        /// be skipped, so the condition passed to CondJump is the one under which the body RUNS:
        /// getState(a) == b for ifState, getState(a) != b for ifNotState.
        /// </summary>
        private void IfState(int offset, bool ifNot)
        {
            string a = GetVarOrDirectWord(0x80);
            string b = GetVarOrDirectByte(0x40);
            string comparison = ifNot ? " != " : " == ";
            CondJump(offset, "getState(" + a + ")" + comparison + b);
        }

        /// <summary>
        /// oldRoomEffect: a sub-opcode byte, and only when (sub &amp; 0x1F) == 3 a following word param
        /// (variable when bit 0x80 of the sub byte is set). scummvm o4_oldRoomEffect.
        /// </summary>
        private void OldRoomEffect(int offset)
        {
            byte sub = ReadByte();
            if ((sub & 0x1F) == 3)
            {
                string a = GetVarOrDirectWordAux(sub, 0x80);
                Emit(offset, "oldRoomEffect(set: " + a + ");");
            }
            else
            {
                Emit(offset, "oldRoomEffect(fadeIn);");
            }
        }

        /// <summary>
        /// saveLoadVars: a mode byte (1 = save, else load) then a list of sub-ops ended by a 0x00
        /// byte (or a 0x04 / 0x1F sub-op). Mirrors scummvm ScummEngine_v4::saveVars/loadVars so the
        /// exact number of operand bytes is consumed - the v5 base mis-decodes this as a zero-byte
        /// dummy() and desyncs the remainder of the script.
        ///   sub &amp; 0x1F: 0x01 = two result-var refs; 0x02 = two var-or-direct bytes (bits 0x80,0x40);
        ///              0x03 = an inline filename string (not translated); 0x04 / 0x1F = end.
        /// </summary>
        private void SaveLoadVars(int offset)
        {
            byte mode = ReadByte();
            bool end = false;
            int guard = 0;
            while (!end && guard++ < 256)
            {
                byte sub = ReadByte();
                if (sub == 0) break; // 0x00 terminates the list
                switch (sub & 0x1F)
                {
                    case 0x01: // a range of variables: two result-var references
                        ReadVarRef();
                        ReadVarRef();
                        break;
                    case 0x02: // a range of string variables
                        GetVarOrDirectByteAux(sub, 0x80);
                        GetVarOrDirectByteAux(sub, 0x40);
                        break;
                    case 0x03: // open file: an inline filename (kind "file" is excluded from translation)
                        ReadString("file");
                        break;
                    case 0x04: // append -> end
                    case 0x1F: // close file -> end
                        end = true;
                        break;
                    default:
                        break;
                }
            }
            Emit(offset, "saveLoadVars(" + (mode == 1 ? "Save" : "Load") + ");");
        }
    }
}
