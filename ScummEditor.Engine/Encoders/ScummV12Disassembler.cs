using System;
using System.Collections.Generic;
using System.Text;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Disassembles SCUMM v1/v2 bytecode (Maniac Mansion, Zak McKracken - the pre-v3 games). The v1/v2
    /// language is BYTE-oriented and completely different from the parameter-bit WORD language of v3-v6
    /// (Scumm5Disassembler): a variable reference and a result var are a single BYTE (no 0x2000/0x8000
    /// indexed-array word form), the opcode table is its own, and inline strings use a 0x80="trailing
    /// space" + control-code (&lt; 8) encoding. Jumps are still 2-byte signed words. v1 and v2 share this
    /// table; only v0 (C64) differs. Mirrors descumm next_line_V12 + ScummVM script_v2.cpp.
    ///
    /// Because a BYTE parameter consumes one byte whether it is a var or a literal, the opcode's
    /// var/literal bits (0x80/0x40/0x20) only change the byte count for WORD parameters (a var = 1 byte,
    /// a literal = 2 bytes). Getting that right is what keeps the stream in sync.
    ///
    /// Produces the shared ScummV6Disassembler.Result (Listing + per-string and per-jump positions) so the
    /// text-export/import pipeline and the GUI consume it exactly like the other disassemblers.
    /// </summary>
    public class ScummV12Disassembler
    {
        private byte[] _code;
        private int _pos;
        private byte _op; // current opcode byte; its bits select literal vs variable parameters

        private readonly List<Line> _lines = new List<Line>();
        private readonly HashSet<int> _jumpTargets = new HashSet<int>();
        private readonly List<int> _unknown = new List<int>();
        private readonly List<ScummV6Disassembler.StringRef> _strings = new List<ScummV6Disassembler.StringRef>();
        private readonly List<ScummV6Disassembler.JumpRef> _jumps = new List<ScummV6Disassembler.JumpRef>();
        private IDictionary<int, string> _namedLabels;
        private bool _stopped;

        private struct Line
        {
            public int Offset;
            public string Text;
        }

        public static ScummV6Disassembler.Result Disassemble(byte[] code, int startOffset)
        {
            return Disassemble(code, startOffset, null);
        }

        public static ScummV6Disassembler.Result Disassemble(byte[] code, int startOffset, IDictionary<int, string> namedLabels)
        {
            return new ScummV12Disassembler().RunDisassembly(code, startOffset, namedLabels);
        }

        protected ScummV6Disassembler.Result RunDisassembly(byte[] code, int startOffset, IDictionary<int, string> namedLabels)
        {
            _namedLabels = namedLabels;
            _code = code;
            _pos = startOffset;

            while (_pos < _code.Length && !_stopped)
            {
                int offset = _pos;
                byte op = ReadByte();
                _op = op;
                try
                {
                    Decode(op, offset);
                }
                catch (IndexOutOfRangeException)
                {
                    Emit(offset, "; <truncated while decoding 0x" + op.ToString("X2") + ">");
                    _stopped = true;
                }
            }

            return new ScummV6Disassembler.Result
            {
                Listing = Render(),
                DecodedToEnd = !_stopped && _pos >= _code.Length,
                UnknownOpcodes = _unknown,
                BytesDecoded = _pos - startOffset,
                Strings = _strings,
                Jumps = _jumps
            };
        }

        // -------------------------------------------------------------------------
        // Reading helpers
        // -------------------------------------------------------------------------

        private byte ReadByte() { return _code[_pos++]; }

        private int ReadWord()
        {
            int v = _code[_pos] | (_code[_pos + 1] << 8);
            _pos += 2;
            return v;
        }

        private int ReadSignedWord() { return (short)ReadWord(); }

        private static string Var(int n) { return "Var[" + n + "]"; }

        /// <summary>A v1/v2 variable reference or result var: always ONE byte (a plain global slot).</summary>
        private string ReadVarRef() { return Var(ReadByte()); }

        /// <summary>A byte parameter: one byte either way (a var when the opcode bit is set, else a literal).</summary>
        private string GetVarOrDirectByte(int paramBit)
        {
            byte b = ReadByte();
            return (_op & paramBit) != 0 ? Var(b) : b.ToString();
        }

        /// <summary>A word parameter: a 1-byte var when the opcode bit is set, otherwise a 2-byte signed literal.</summary>
        private string GetVarOrDirectWord(int paramBit)
        {
            if ((_op & paramBit) != 0) return Var(ReadByte());
            return ReadSignedWord().ToString();
        }

        private void Emit(int offset, string text) { _lines.Add(new Line { Offset = offset, Text = text }); }

        private string Jump(int offset)
        {
            int operandOffset = _pos;
            int rel = ReadSignedWord();
            int target = _pos + rel;
            _jumpTargets.Add(target);
            _jumps.Add(new ScummV6Disassembler.JumpRef { OperandOffset = operandOffset, Target = target });
            return "L" + target.ToString("X4");
        }

        private void CondJump(int offset, string condition)
        {
            Emit(offset, "if (" + condition + ") goto " + Jump(offset) + ";");
        }

        private void Unknown(byte op, int offset)
        {
            _unknown.Add(op);
            Emit(offset, "; <unknown opcode 0x" + op.ToString("X2") + " - disassembly stopped>");
            _stopped = true;
        }

        /// <summary>
        /// A v1/v2 inline string: NUL-terminated; each byte's bit 0x80 = "append a trailing space"; the low
        /// 7 bits are the glyph; a low value &lt; 8 is a control code that takes ONE extra byte when &gt; 3
        /// (codes 1-3 take none). Records the byte span as a StringRef for the text pipeline.
        /// </summary>
        private string ReadStringV12(string kind)
        {
            int start = _pos;
            bool terminated = false;
            var sb = new StringBuilder();
            while (_pos < _code.Length)
            {
                byte b = ReadByte();
                if (b == 0) { terminated = true; break; }
                bool trailingSpace = (b & 0x80) != 0;
                int c = b & 0x7F;
                if (c < 8)
                {
                    sb.Append("{0x" + c.ToString("X2") + "}");
                    if (c > 3 && _pos < _code.Length) sb.Append("{0x" + ReadByte().ToString("X2") + "}");
                }
                else
                {
                    if (c == '"' || c == '\\') sb.Append('\\');
                    sb.Append((char)c);
                }
                if (trailingSpace) sb.Append(' ');
            }
            _strings.Add(new ScummV6Disassembler.StringRef
            {
                Offset = start,
                Length = _pos - start,
                Terminated = terminated,
                Kind = kind
            });
            return "\"" + sb.ToString().Replace("\"", "\\\"") + "\"";
        }

        private string Render()
        {
            var namedOffsets = new List<int>();
            if (_namedLabels != null) foreach (int k in _namedLabels.Keys) namedOffsets.Add(k);
            namedOffsets.Sort();
            var targets = new List<int>(_jumpTargets);
            targets.Sort();

            var sb = new StringBuilder();
            int ni = 0, ti = 0;
            foreach (Line line in _lines)
            {
                for (; ni < namedOffsets.Count && namedOffsets[ni] <= line.Offset; ni++)
                    sb.AppendLine(_namedLabels[namedOffsets[ni]] + ":"
                        + (namedOffsets[ni] == line.Offset ? "" : "    ; @" + namedOffsets[ni].ToString("X4")));
                for (; ti < targets.Count && targets[ti] <= line.Offset; ti++)
                    sb.AppendLine("L" + targets[ti].ToString("X4") + ":");
                sb.AppendLine("    " + line.Offset.ToString("X4") + "  " + line.Text);
            }
            for (; ni < namedOffsets.Count; ni++)
                sb.AppendLine(_namedLabels[namedOffsets[ni]] + ":    ; @" + namedOffsets[ni].ToString("X4"));
            for (; ti < targets.Count; ti++)
                sb.AppendLine("L" + targets[ti].ToString("X4") + ":");
            return sb.ToString();
        }

        // -------------------------------------------------------------------------
        // Opcode dispatch (table mirrors descumm next_line_V12 / ScummVM script_v2.cpp)
        // -------------------------------------------------------------------------

        private void Decode(byte op, int offset)
        {
            switch (op)
            {
                // --- no-operand opcodes ---
                case 0x00: case 0xA0: Emit(offset, "stopObjectCode();"); break;
                case 0x20: Emit(offset, "stopMusic();"); break;
                case 0x40: Emit(offset, "cutscene();"); break;
                case 0x4C: Emit(offset, "waitForSentence();"); break;
                case 0x58: Emit(offset, "beginOverride();"); break;
                case 0x80: Emit(offset, "breakHere();"); break;
                case 0xAC: Emit(offset, "drawSentence();"); break;
                case 0xAE: Emit(offset, "waitForMessage();"); break;
                case 0xC0: Emit(offset, "endCutscene();"); break;
                case 0x98: Emit(offset, "restart();"); break;
                case 0x5C: case 0x6B: case 0x6E: case 0xAB: case 0xDC: case 0xEB: case 0xEE:
                    Emit(offset, "dummy(0x" + op.ToString("X2") + ");"); break;

                // --- actor / object actions (byte params) ---
                case 0x01: case 0x21: case 0x41: case 0x61: case 0x81: case 0xA1: case 0xC1: case 0xE1:
                    Emit(offset, "putActor(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ", " + GetVarOrDirectByte(0x20) + ");"); break;
                case 0x11: case 0x51: case 0x91: case 0xD1:
                    Emit(offset, "animateActor(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ");"); break;
                case 0x09: case 0x49: case 0x89: case 0xC9:
                    Emit(offset, "faceActor(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ");"); break;
                case 0x1E: case 0x3E: case 0x5E: case 0x7E: case 0x9E: case 0xBE: case 0xDE: case 0xFE:
                    Emit(offset, "walkActorTo(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ", " + GetVarOrDirectByte(0x20) + ");"); break;
                case 0x0D: case 0x4D: case 0x8D: case 0xCD:
                    Emit(offset, "walkActorToActor(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ", " + ReadByte() + ");"); break;
                case 0x36: case 0x76: case 0xB6: case 0xF6:
                    Emit(offset, "walkActorToObject(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectWord(0x40) + ");"); break;
                case 0x0E: case 0x4E: case 0x8E: case 0xCE:
                    Emit(offset, "putActorAtObject(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectWord(0x40) + ");"); break;
                case 0x2D: case 0x6D: case 0xAD: case 0xED:
                    Emit(offset, "putActorInRoom(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ");"); break;
                case 0x3D: case 0x7D: case 0xBD: case 0xFD:
                    Emit(offset, "setActorElevation(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ");"); break;
                case 0x52: case 0xD2: Emit(offset, "actorFollowCamera(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x3B: case 0xBB: Emit(offset, "waitForActor(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x12: case 0x92: Emit(offset, "panCameraTo(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x32: case 0xB2: Emit(offset, "setCameraAt(" + GetVarOrDirectByte(0x80) + ");"); break;

                // --- object actions (word object param) ---
                case 0x05: case 0x25: case 0x45: case 0x65: case 0x85: case 0xA5: case 0xC5: case 0xE5:
                    Emit(offset, "drawObject(" + GetVarOrDirectWord(0x80) + ", " + GetVarOrDirectByte(0x40) + ", " + GetVarOrDirectByte(0x20) + ");"); break;
                case 0x50: case 0xD0: Emit(offset, "pickupObject(" + GetVarOrDirectWord(0x80) + ");"); break;
                case 0x0B: case 0x4B: case 0x8B: case 0xCB:
                    Emit(offset, "setObjPreposition(" + GetVarOrDirectWord(0x80) + ", " + ReadByte() + ");"); break;
                case 0x29: case 0x69: case 0xA9: case 0xE9:
                    Emit(offset, "setOwnerOf(" + GetVarOrDirectWord(0x80) + ", " + GetVarOrDirectByte(0x40) + ");"); break;

                // --- set/clear object state (word object param, no jump) ---
                case 0x07: case 0x87: Emit(offset, "setState08(" + GetVarOrDirectWord(0x80) + ");"); break;
                case 0x47: case 0xC7: Emit(offset, "clearState08(" + GetVarOrDirectWord(0x80) + ");"); break;
                case 0x27: case 0xA7: Emit(offset, "setState04(" + GetVarOrDirectWord(0x80) + ");"); break;
                case 0x67: case 0xE7: Emit(offset, "clearState04(" + GetVarOrDirectWord(0x80) + ");"); break;
                case 0x17: case 0x97: Emit(offset, "clearState02(" + GetVarOrDirectWord(0x80) + ");"); break;
                case 0x77: case 0xF7: Emit(offset, "setState02(" + GetVarOrDirectWord(0x80) + ");"); break;
                case 0x37: case 0xB7: Emit(offset, "setState01(" + GetVarOrDirectWord(0x80) + ");"); break;
                case 0x57: case 0xD7: Emit(offset, "clearState01(" + GetVarOrDirectWord(0x80) + ");"); break;

                // --- result = query(...) (result var first, then the operand) ---
                case 0x03: case 0x83: ResultThenByte(offset, "getActorRoom"); break;
                case 0x06: case 0x86: ResultThenByte(offset, "getActorElevation"); break;
                case 0x63: case 0xE3: ResultThenByte(offset, "getActorFacing"); break;
                case 0x71: case 0xF1: ResultThenByte(offset, "getActorCostume"); break;
                case 0x56: case 0xD6: ResultThenByte(offset, "getActorMoving"); break;
                case 0x7B: case 0xFB: ResultThenByte(offset, "getActorWalkBox"); break;
                case 0x43: case 0xC3: ResultThenByte(offset, "getActorX"); break;
                case 0x23: case 0xA3: ResultThenByte(offset, "getActorY"); break;
                case 0x16: case 0x96: ResultThenByte(offset, "getRandomNr"); break;
                case 0x22: case 0xA2: ResultThenByte(offset, "saveLoadGame"); break;
                case 0x68: case 0xE8: ResultThenByte(offset, "isScriptRunning"); break;
                case 0x7C: case 0xFC: ResultThenByte(offset, "isSoundRunning"); break;
                case 0x10: case 0x90: ResultThenWord(offset, "getObjectOwner"); break;
                case 0x66: case 0xE6: ResultThenWord(offset, "getClosestObjActor"); break;
                case 0x6C: case 0xEC: ResultThenWord(offset, "getObjPreposition"); break;
                case 0x15: case 0x55: case 0x95: case 0xD5:
                {
                    string r = ReadVarRef();
                    Emit(offset, r + " = actorFromPos(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ");"); break;
                }
                case 0x35: case 0x75: case 0xB5: case 0xF5:
                {
                    string r = ReadVarRef();
                    Emit(offset, r + " = findObject(" + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ");"); break;
                }
                case 0x34: case 0x74: case 0xB4: case 0xF4:
                {
                    string r = ReadVarRef();
                    Emit(offset, r + " = getDist(" + GetVarOrDirectWord(0x80) + ", " + GetVarOrDirectWord(0x40) + ");"); break;
                }
                case 0x31: case 0xB1:
                {
                    string r = ReadVarRef();
                    string field = ReadSignedWord().ToString(); // A1W: always a 2-byte word
                    Emit(offset, r + " = getBitVar(" + field + ", " + GetVarOrDirectByte(0x80) + ");"); break;
                }

                // --- scripts / sound / rooms ---
                case 0x42: case 0xC2: Emit(offset, "startScript(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x4A: case 0xCA: Emit(offset, "chainScript(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x62: case 0xE2: Emit(offset, "stopScript(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x02: case 0x82: Emit(offset, "startMusic(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x1C: case 0x9C: Emit(offset, "startSound(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x3C: case 0xBC: Emit(offset, "stopSound(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x72: case 0xF2: Emit(offset, "loadRoom(" + GetVarOrDirectByte(0x80) + ");"); break;
                case 0x24: case 0x64: case 0xA4: case 0xE4:
                    Emit(offset, "loadRoomWithEgo(" + GetVarOrDirectWord(0x80) + ", " + GetVarOrDirectByte(0x40) + ", " + ReadByte() + ", " + ReadByte() + ");"); break;
                case 0x30: case 0xB0:
                    Emit(offset, "setBoxFlags(" + GetVarOrDirectByte(0x80) + ", " + ReadByte() + ");"); break;
                case 0x70: case 0xF0:
                    Emit(offset, "lights(" + GetVarOrDirectByte(0x80) + ", " + ReadByte() + ", " + ReadByte() + ");"); break;
                case 0x1B: case 0x5B: case 0x9B: case 0xDB:
                {
                    string field = ReadSignedWord().ToString(); // A1W
                    Emit(offset, "setBitVar(" + field + ", " + GetVarOrDirectByte(0x80) + ", " + GetVarOrDirectByte(0x40) + ");"); break;
                }
                case 0x2B: Emit(offset, "delayVariable(" + ReadVarRef() + ");"); break; // A1V
                case 0x2E: // delay: 3-byte little-endian, value = 0xFFFFFF - d
                {
                    int d = ReadByte() | (ReadByte() << 8) | (ReadByte() << 16);
                    Emit(offset, "delay(" + (0xFFFFFF - d) + ");"); break;
                }
                case 0x60: case 0xE0: // cursorCommand: 1 var byte when 0x80, else a 2-byte word
                    if ((op & 0x80) != 0) Emit(offset, "cursorCommand(" + ReadVarRef() + ");");
                    else Emit(offset, "cursorCommand(" + ReadSignedWord() + ");");
                    break;
                case 0x26: case 0xA6: SetVarRange(offset); break;
                case 0xCC: PseudoRoom(offset); break;

                // --- strings (translatable) ---
                case 0x14: case 0x94:
                    Emit(offset, "print(" + GetVarOrDirectByte(0x80) + ", " + ReadStringV12("print") + ");"); break;
                case 0xD8:
                    Emit(offset, "printEgo(" + ReadStringV12("printEgo") + ");"); break;
                case 0x54: case 0xD4:
                    Emit(offset, "setObjectName(" + GetVarOrDirectWord(0x80) + ", " + ReadStringV12("objectName") + ");"); break;

                // --- control flow ---
                case 0x18: Emit(offset, "goto " + Jump(offset) + ";"); break;
                case 0x04: case 0x84: case 0x08: case 0x88: case 0x38: case 0xB8:
                case 0x44: case 0xC4: case 0x48: case 0xC8: case 0x78: case 0xF8:
                case 0x28: case 0xA8: IfCode(offset, op); break;
                case 0x0F: case 0x8F: case 0x1F: case 0x9F: case 0x2F: case 0xAF: case 0x3F: case 0xBF:
                case 0x4F: case 0xCF: case 0x5F: case 0xDF: case 0x6F: case 0xEF: case 0x7F: case 0xFF:
                    IfState(offset); break;
                case 0x1D: case 0x5D: case 0x9D: case 0xDD:
                    CondJump(offset, "classOfIs(" + GetVarOrDirectWord(0x80) + ", " + GetVarOrDirectByte(0x40) + ")"); break;

                // --- variable assignment / arithmetic ---
                case 0x0A: case 0x8A: case 0x1A: case 0x5A: case 0x9A: case 0xDA:
                case 0x2A: case 0xAA: case 0x3A: case 0xBA: case 0x2C: case 0x6A: case 0xEA:
                case 0x46: case 0xC6: VarSet(offset, op); break;

                // --- doSentence ---
                case 0x19: case 0x39: case 0x59: case 0x79: case 0x99: case 0xB9: case 0xD9: case 0xF9:
                    DoSentence(offset, op); break;

                // --- sub-op blocks ---
                case 0x13: case 0x53: case 0x93: case 0xD3: ActorOps(offset, op); break;
                case 0x33: case 0x73: case 0xB3: case 0xF3: RoomOps(offset); break;
                case 0x7A: case 0xFA: VerbOps(offset, op); break;
                case 0x0C: case 0x8C: ResourceRoutines(offset); break;

                default:
                    Unknown(op, offset);
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // Shared opcode shapes
        // -------------------------------------------------------------------------

        private void ResultThenByte(int offset, string name)
        {
            string r = ReadVarRef();
            Emit(offset, r + " = " + name + "(" + GetVarOrDirectByte(0x80) + ");");
        }

        private void ResultThenWord(int offset, string name)
        {
            string r = ReadVarRef();
            Emit(offset, r + " = " + name + "(" + GetVarOrDirectWord(0x80) + ");");
        }

        /// <summary>Comparison conditionals (descumm do_if_code): var + value + jump; 0x28/0xA8 test a var against another.</summary>
        private void IfCode(int offset, byte op)
        {
            string left = (op != 0x28 && op != 0xA8) ? ReadVarRef() : null;
            int code = op & 0x7F;
            string cmp;
            switch (code)
            {
                case 0x38: cmp = ">="; break;
                case 0x04: cmp = "<="; break;
                case 0x08: cmp = "!="; break;
                case 0x48: cmp = "=="; break;
                case 0x78: cmp = "<"; break;
                case 0x44: cmp = ">"; break;
                default: cmp = (op & 0x80) != 0 ? "" : "!"; break; // 0x28 / 0xA8
            }

            string condition;
            if (op == 0x28 || op == 0xA8)
            {
                string v = ReadVarRef();
                condition = cmp + v; // "!var" or "var"
            }
            else
            {
                string right = GetVarOrDirectWord(0x80);
                condition = left + " " + cmp + " " + right;
            }
            CondJump(offset, condition);
        }

        /// <summary>State conditionals (descumm do_if_state_code v1/v2): object word + a baked-in state bit + jump.</summary>
        private void IfState(int offset)
        {
            string obj = GetVarOrDirectWord(0x80);
            CondJump(offset, "objectState(" + obj + ")");
        }

        /// <summary>
        /// Variable assignment / arithmetic (descumm do_varset_code): a 1-byte store target (or the
        /// indirect Var[Var[n]] form for 0x0A/0x2A/0x6A), then an operator, then the value: a plain byte
        /// for assignVarByte (0x2C), nothing for increment/decrement (0x46), else a word parameter.
        /// </summary>
        private void VarSet(int offset, byte op)
        {
            int code = op & 0x7F;
            string target = (code == 0x0A || code == 0x2A || code == 0x6A)
                ? "Var[Var[" + ReadByte() + "]]"
                : ReadVarRef();

            if (code == 0x46) // increment / decrement
            {
                Emit(offset, target + ((op & 0x80) != 0 ? "--;" : "++;"));
                return;
            }

            string oper;
            switch (code)
            {
                case 0x0A: case 0x1A: case 0x2C: oper = "="; break;
                case 0x2A: case 0x5A: oper = "+="; break;
                case 0x3A: case 0x6A: oper = "-="; break;
                default: oper = "="; break;
            }

            string value = code == 0x2C ? ReadByte().ToString() : GetVarOrDirectWord(0x80);
            Emit(offset, target + " " + oper + " " + value + ";");
        }

        /// <summary>doSentence (descumm): a 0xFC/0xFB sentinel (STOP/RESET) when the verb is a literal, else verb + 2 objects + an exec-mode byte.</summary>
        private void DoSentence(int offset, byte op)
        {
            if ((op & 0x80) == 0)
            {
                byte peek = _code[_pos];
                if (peek == 0xFC) { _pos++; Emit(offset, "doSentence(STOP);"); return; }
                if (peek == 0xFB) { _pos++; Emit(offset, "doSentence(RESET);"); return; }
            }
            string verb = GetVarOrDirectByte(0x80);
            string objA = GetVarOrDirectWord(0x40);
            string objB = GetVarOrDirectWord(0x20);
            string mode = ReadByte().ToString();
            Emit(offset, "doSentence(" + verb + ", " + objA + ", " + objB + ", " + mode + ");");
        }

        /// <summary>actorOps (descumm do_actorops_v12): actor + an arg + a sub-op; v2 sub-op 2 reads an extra byte, sub-op 3 reads a name string.</summary>
        private void ActorOps(int offset, byte op)
        {
            string actor = GetVarOrDirectByte(0x80);
            string arg = GetVarOrDirectByte(0x40);
            byte sub = ReadByte();
            string part;
            switch (sub)
            {
                case 1: part = "sound(" + arg + ")"; break;
                case 2: part = "color(" + ReadByte() + ", " + arg + ")"; break; // v2 reads an extra byte before the arg
                case 3: part = "name(" + ReadStringV12("actorName") + ")"; break;
                case 4: part = "costume(" + arg + ")"; break;
                case 5: part = "talkColor(" + arg + ")"; break;
                default: part = "op_" + sub + "(" + arg + ")"; break;
            }
            Emit(offset, "actorOps(" + actor + ", " + part + ");");
        }

        /// <summary>roomOps (descumm do_room_ops_old, v1/v2): two BYTE params from the main opcode bits, then a sub-op byte.</summary>
        private void RoomOps(int offset)
        {
            string a = GetVarOrDirectByte(0x80);
            string b = GetVarOrDirectByte(0x40);
            byte sub = ReadByte();
            string name;
            switch (sub & 0x1F)
            {
                case 1: name = "roomScroll"; break;
                case 2: name = "roomColor"; break;
                case 3: name = "setScreen"; break;
                case 4: name = "setPalColor"; break;
                case 5: Emit(offset, "roomOps.shakeOn();"); return;
                case 6: Emit(offset, "roomOps.shakeOff();"); return;
                default: name = "op_0x" + (sub & 0x1F).ToString("X2"); break;
            }
            Emit(offset, "roomOps." + name + "(" + a + ", " + b + ");");
        }

        /// <summary>verbOps (descumm do_verbops_v2): a sub-op byte then sub-op-specific operands; "New" carries a translatable name string.</summary>
        private void VerbOps(int offset, byte op)
        {
            byte sub = ReadByte();
            if (sub == 0)
            {
                Emit(offset, "verbOps.delete(" + GetVarOrDirectByte(0x80) + ");");
            }
            else if (sub == 0xFF)
            {
                Emit(offset, "verbOps.state(" + ReadByte() + ", " + ReadByte() + ");");
            }
            else
            {
                string a = ReadByte().ToString();
                string b = ReadByte().ToString();
                string c = GetVarOrDirectByte(0x80);
                string d = ReadByte().ToString();
                string text = ReadStringV12("verbName");
                Emit(offset, "verbOps.new" + sub + "(" + a + ", " + b + ", " + c + ", " + d + ", " + text + ");");
            }
        }

        /// <summary>resourceRoutines (descumm do_resource_v2): a resource id byte then a sub-op byte (type/action); no further operands.</summary>
        private void ResourceRoutines(int offset)
        {
            string resId = GetVarOrDirectByte(0x80);
            byte sub = ReadByte();
            Emit(offset, "resourceRoutines.op_0x" + sub.ToString("X2") + "(" + resId + ");");
        }

        /// <summary>setVarRange (descumm): a result var, a count byte, then count items (word when 0x80, else byte).</summary>
        private void SetVarRange(int offset)
        {
            string r = ReadVarRef();
            int count = ReadByte();
            var items = new List<string>();
            for (int i = 0; i < count; i++)
                items.Add((_op & 0x80) != 0 ? ReadSignedWord().ToString() : ReadByte().ToString());
            Emit(offset, "setVarRange(" + r + ", " + count + ", [" + string.Join(", ", items.ToArray()) + "]);");
        }

        /// <summary>pseudoRoom (descumm do_pseudoRoom): an id byte then a 0-terminated list of bytes.</summary>
        private void PseudoRoom(int offset)
        {
            int id = ReadByte();
            var items = new List<string>();
            while (_pos < _code.Length)
            {
                byte b = ReadByte();
                if (b == 0) break;
                items.Add(b.ToString());
            }
            Emit(offset, "pseudoRoom(" + id + ", [" + string.Join(", ", items.ToArray()) + "]);");
        }
    }
}
