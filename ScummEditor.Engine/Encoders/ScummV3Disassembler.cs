using System.Collections.Generic;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Disassembles SCUMM v3 bytecode (Indiana Jones 3, Loom EGA, Zak). v3 shares the v4/v5
    /// parameter-bit opcode language (ScummEngine_v3 : ScummEngine_v4), so this forks
    /// ScummV4Disassembler and only changes the opcodes v3 lays out differently (scummvm
    /// engines/scumm/script_v3.cpp + descumm's v3 path):
    ///   0x30/0xB0  setBoxFlags(varOrByte, byte)      - v4/v5 route this to matrixOps
    ///   0x33/0x73/0xB3/0xF3  roomOps                 - the two word params come from the MAIN opcode
    ///                                                  bits BEFORE the sub-op (the "old" layout)
    ///   0x3B/0xBB  waitForActor(varOrByte)           - v4 disabled this (v5 = getActorScale)
    ///   0x4C       waitForSentence (no operands)      - v4 disabled this (v5 = soundKludge)
    ///   print sub-op 6   -> reads a word (text height) instead of the no-arg "left"
    ///   cursorCommand 0x0E -> loadCharset(byte, byte) instead of the word-vararg charset colours
    /// Indy3 additionally reads getActorX/getActorY with a BYTE actor param; set IsIndy3 for that game.
    /// Everything else is delegated to the v4/v5 base unchanged.
    /// </summary>
    public class ScummV3Disassembler : ScummV4Disassembler
    {
        /// <summary>Indiana Jones 3 reads getActorX/getActorY with a byte (not word) direct actor param.</summary>
        public bool IsIndy3 { get; set; }

        /// <summary>
        /// True for the single-byte v3 "old bundle" games (Loom EGA, Indy3 EGA), where 0xFE is a second
        /// in-string escape marker. The FM-Towns/256 v3 games (which may be Japanese SJIS) leave this
        /// false so 0xFE stays a content byte, exactly like v5+.
        /// </summary>
        public bool IsOldBundle { get; set; }

        public static new ScummV6Disassembler.Result Disassemble(byte[] code, int startOffset)
        {
            return Disassemble(code, startOffset, null, false);
        }

        public static new ScummV6Disassembler.Result Disassemble(byte[] code, int startOffset, IDictionary<int, string> namedLabels)
        {
            return Disassemble(code, startOffset, namedLabels, false);
        }

        public static ScummV6Disassembler.Result Disassemble(byte[] code, int startOffset, IDictionary<int, string> namedLabels, bool isIndy3)
        {
            return Disassemble(code, startOffset, namedLabels, isIndy3, false);
        }

        public static ScummV6Disassembler.Result Disassemble(byte[] code, int startOffset, IDictionary<int, string> namedLabels, bool isIndy3, bool isOldBundle)
        {
            return new ScummV3Disassembler { IsIndy3 = isIndy3, IsOldBundle = isOldBundle }.RunDisassembly(code, startOffset, namedLabels);
        }

        protected override bool IsStringEscape(byte b)
        {
            return b == 0xFF || (IsOldBundle && b == 0xFE);
        }

        protected override void Decode(byte op, int offset)
        {
            switch (op)
            {
                case 0x02: case 0x82: // startMusic - in v3 this is the FM-Towns CD query: it STORES a
                    // result var (word) then reads a var-or-direct byte (3 operand bytes), not the
                    // single byte the v4/v5 base reads. Reading only one byte desyncs the rest of the
                    // script (this was the root of the Loom/Indy3 verb-script over-reads).
                    // scummvm o5_startMusic, script_v5.cpp (the version==3 path).
                    SetResult(offset, ReadVarRef(), "startMusic(" + GetVarOrDirectByte(0x80) + ")");
                    break;

                case 0x30: case 0xB0: // setBoxFlags: a var-or-direct byte + a plain byte
                {
                    string box = GetVarOrDirectByte(0x80);
                    string flags = ReadByte().ToString();
                    Emit(offset, "setBoxFlags(" + box + ", " + flags + ");");
                    break;
                }

                case 0xAE: // wait - Indiana Jones 3 (non-Macintosh) hardwires this to waitForMessage
                    // with NO sub-op byte; every other v3 game reads a sub-op (base WaitOps). Reading
                    // the absent sub-op over-reads and desyncs Indy3 scripts. scummvm o5_wait.
                    if (IsIndy3)
                    {
                        Emit(offset, "wait.waitForMessage();");
                        break;
                    }
                    base.Decode(op, offset);
                    break;

                case 0x3B: case 0xBB: // waitForActor (re-enabled in v3; NOP-ish, but consumes the actor param)
                    Emit(offset, "waitForActor(" + GetVarOrDirectByte(0x80) + ");");
                    break;

                case 0x4C: // waitForSentence (re-enabled in v3; no operands)
                    Emit(offset, "waitForSentence();");
                    break;

                case 0x43: case 0xC3: // getActorX - Indy3 reads a BYTE actor param (others a word)
                    if (IsIndy3)
                    {
                        SetResult(offset, ReadVarRef(), "getActorX(" + GetVarOrDirectByte(0x80) + ")");
                        break;
                    }
                    base.Decode(op, offset);
                    break;

                case 0x23: case 0xA3: // getActorY - Indy3 reads a BYTE actor param (others a word)
                    if (IsIndy3)
                    {
                        SetResult(offset, ReadVarRef(), "getActorY(" + GetVarOrDirectByte(0x80) + ")");
                        break;
                    }
                    base.Decode(op, offset);
                    break;

                default:
                    base.Decode(op, offset);
                    break;
            }
        }

        /// <summary>
        /// v3 roomOps: the two word parameters are read from the MAIN opcode's bits 0x80/0x40 BEFORE
        /// the sub-op byte, and the sub-op carries no further parameter bits (descumm do_room_ops_old,
        /// scriptVersion==3). v4/v5 instead read the params from the sub-op bits.
        /// </summary>
        protected override void RoomOps(int offset)
        {
            string a = GetVarOrDirectWord(0x80);
            string b = GetVarOrDirectWord(0x40);
            byte sub = ReadByte();

            switch (sub & 0x1F)
            {
                case 1: Emit(offset, "roomOps.roomScroll(" + a + ", " + b + ");"); break;
                case 2: Emit(offset, "roomOps.roomColor(" + a + ", " + b + ");"); break;
                case 3: Emit(offset, "roomOps.setScreen(" + a + ", " + b + ");"); break;
                case 4: Emit(offset, "roomOps.setPalColor(" + a + ", " + b + ");"); break;
                case 5: Emit(offset, "roomOps.shakeOn();"); break;
                case 6: Emit(offset, "roomOps.shakeOff();"); break;
                default: Emit(offset, "roomOps.op_0x" + (sub & 0x1F).ToString("X2") + "(" + a + ", " + b + ");"); break;
            }
        }

        protected override string PrintSubOp6(byte sub)
        {
            // v3/Loom: a word text-height argument (vs the v4/v5 no-arg "left").
            return "height(" + GetVarOrDirectWordAux(sub, 0x80) + ")";
        }

        protected override string CursorSubOp14(byte sub)
        {
            // v3: loadCharset(charsetId, room) - two var-or-direct bytes (vs the v4/v5 charset colours).
            return "cursorCommand.loadCharset(" + GetVarOrDirectByteAux(sub, 0x80) + ", " + GetVarOrDirectByteAux(sub, 0x40) + ");";
        }
    }
}
