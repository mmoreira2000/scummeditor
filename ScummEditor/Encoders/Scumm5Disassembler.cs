using System;
using System.Collections.Generic;
using System.Text;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Disassembles SCUMM v5 bytecode (Monkey Island 1 CD / Monkey Island 2 / Indy 4) into the
    /// same C#-like listing produced by the v6 disassembler, and records the position of every
    /// inline string and relative jump (the base for the text export/import pipeline).
    ///
    /// The v5 language is not stack based: each opcode carries its parameters inline, and the
    /// bits 0x80/0x40/0x20 of the opcode byte tell whether each parameter is a literal or a
    /// variable reference. Conditionals embed the jump offset and jump when the condition is
    /// FALSE, so they are rendered as "if (negated condition) goto label;".
    ///
    /// The opcode table and parameter layouts mirror ScummVM's script_v5.cpp (o5_ handlers).
    /// </summary>
    public class Scumm5Disassembler
    {
        private byte[] _code;
        private int _pos;
        private byte _op; // current opcode byte; its bits select literal vs variable parameters

        private readonly List<Line> _lines = new List<Line>();
        private readonly HashSet<int> _jumpTargets = new HashSet<int>();
        private readonly List<int> _unknown = new List<int>();
        private readonly List<Scumm6Disassembler.StringRef> _strings = new List<Scumm6Disassembler.StringRef>();
        private readonly List<Scumm6Disassembler.JumpRef> _jumps = new List<Scumm6Disassembler.JumpRef>();
        private IDictionary<int, string> _namedLabels;
        private bool _stopped;

        private struct Line
        {
            public int Offset;
            public string Text;
        }

        public static Scumm6Disassembler.Result Disassemble(byte[] code, int startOffset)
        {
            return Disassemble(code, startOffset, null);
        }

        public static Scumm6Disassembler.Result Disassemble(byte[] code, int startOffset, IDictionary<int, string> namedLabels)
        {
            var d = new Scumm5Disassembler();
            d._namedLabels = namedLabels;
            return d.Run(code, startOffset);
        }

        private Scumm6Disassembler.Result Run(byte[] code, int startOffset)
        {
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

            return new Scumm6Disassembler.Result
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

        private int ReadSignedWord()
        {
            return (short)ReadWord();
        }

        private void Emit(int offset, string text)
        {
            _lines.Add(new Line { Offset = offset, Text = text });
        }

        /// <summary>Variable name for a 16-bit variable id (Global / Local / Bit + 0x2000 indexing).</summary>
        private string ReadVarRef()
        {
            int var = ReadWord();

            string index = null;
            if ((var & 0x2000) != 0)
            {
                int extra = ReadWord();
                if ((extra & 0x2000) != 0)
                {
                    index = VarName(extra & ~0x2000);
                }
                else
                {
                    index = (extra & 0xFFF).ToString();
                }
                var &= ~0x2000;
            }

            string name = VarName(var);
            if (index == null) return name;

            // indexed access: the index is added to the variable number
            int bracket = name.IndexOf(']');
            return name.Substring(0, bracket) + " + " + index + "]";
        }

        private static string VarName(int var)
        {
            if ((var & 0x8000) != 0) return "Bit[" + (var & 0x7FFF) + "]";
            if ((var & 0x4000) != 0) return "Local[" + (var & 0xFFF) + "]";
            return "Global[" + var + "]";
        }

        /// <summary>A byte parameter: variable when the given bit is set on the opcode, literal otherwise.</summary>
        private string GetVarOrDirectByte(int paramBit)
        {
            return GetVarOrDirectByteAux(_op, paramBit);
        }

        private string GetVarOrDirectByteAux(byte opcodeBits, int paramBit)
        {
            if ((opcodeBits & paramBit) != 0) return ReadVarRef();
            return ReadByte().ToString();
        }

        /// <summary>A (signed) word parameter: variable when the given bit is set on the opcode.</summary>
        private string GetVarOrDirectWord(int paramBit)
        {
            return GetVarOrDirectWordAux(_op, paramBit);
        }

        private string GetVarOrDirectWordAux(byte opcodeBits, int paramBit)
        {
            if ((opcodeBits & paramBit) != 0) return ReadVarRef();
            return ReadSignedWord().ToString();
        }

        /// <summary>Word vararg list: items prefixed by an aux byte (0x80 = variable), ended by 0xFF.</summary>
        private string ReadWordVarArgs()
        {
            var items = new List<string>();
            while (true)
            {
                byte aux = ReadByte();
                if (aux == 0xFF) break;
                items.Add(GetVarOrDirectWordAux(aux, 0x80));
                if (items.Count > 32) break; // defensive: a corrupt list would run away
            }
            return "[" + string.Join(", ", items.ToArray()) + "]";
        }

        private string Jump(int offset)
        {
            int operandOffset = _pos;
            int rel = ReadSignedWord();
            int target = _pos + rel;
            _jumpTargets.Add(target);
            _jumps.Add(new Scumm6Disassembler.JumpRef { OperandOffset = operandOffset, Target = target });
            return "L" + target.ToString("X4");
        }

        /// <summary>
        /// Conditional: the engine jumps when the condition is FALSE, so the listing shows
        /// "if (negated condition) goto label;".
        /// </summary>
        private void CondJump(int offset, string conditionWhenBodyRuns)
        {
            string negated = Scumm6Disassembler.NegateCondition(conditionWhenBodyRuns);
            Emit(offset, "if (" + negated + ") goto " + Jump(offset) + ";");
        }

        /// <summary>Result-variable statement ("Global[x] = expr;"), or a captured nested expression.</summary>
        private string _nestedResult; // non-null while decoding the nested opcode of an expression

        private void SetResult(int offset, string target, string expr)
        {
            if (_nestedResult == "")
            {
                _nestedResult = expr;
                return;
            }
            Emit(offset, target + " = " + Scumm6Disassembler.StripParens(expr) + ";");
        }

        // Strings use the same friendly tokens as the v6 listings and the text export.
        private static readonly GameTextCodec ListingCodec = GameTextCodec.Default();

        private string ReadString(string kind)
        {
            int start = _pos;
            bool terminated = false;
            while (_pos < _code.Length)
            {
                byte b = ReadByte();
                if (b == 0) { terminated = true; break; }

                if (b == 0xFF || b == 0xFE)
                {
                    byte code = ReadByte();
                    if (code != 1 && code != 2 && code != 3 && code != 8) ReadWord(); // 16-bit argument
                }
            }

            int contentLength = _pos - start - (terminated ? 1 : 0);
            _strings.Add(new Scumm6Disassembler.StringRef
            {
                Offset = start,
                Length = _pos - start,
                Terminated = terminated,
                Kind = kind
            });
            return "\"" + ListingCodec.Decode(_code, start, contentLength).Replace("\"", "\\\"") + "\"";
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
        // Opcode dispatch (table mirrors ScummVM script_v5.cpp)
        // -------------------------------------------------------------------------

        private void Decode(byte op, int offset)
        {
            switch (op)
            {
                case 0x00: case 0xA0: Emit(offset, "stopObjectCode();"); break;

                case 0x01: case 0x21: case 0x41: case 0x61:
                case 0x81: case 0xA1: case 0xC1: case 0xE1:
                {
                    string actor = GetVarOrDirectByte(0x80);
                    string x = GetVarOrDirectWord(0x40);
                    string y = GetVarOrDirectWord(0x20);
                    Emit(offset, "putActor(" + actor + ", " + x + ", " + y + ");");
                    break;
                }

                case 0x02: case 0x82: Emit(offset, "startMusic(" + GetVarOrDirectByte(0x80) + ");"); break;

                case 0x03: case 0x83:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorRoom(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                // ---- conditionals: variable first, then the value; jump happens on FALSE ----
                case 0x04: case 0x84: // isGreaterEqual: body runs when value >= var
                {
                    string var = ReadVarRef();
                    string val = GetVarOrDirectWord(0x80);
                    CondJump(offset, var + " <= " + val);
                    break;
                }
                case 0x08: case 0x88: // isNotEqual
                {
                    string var = ReadVarRef();
                    string val = GetVarOrDirectWord(0x80);
                    CondJump(offset, var + " != " + val);
                    break;
                }
                case 0x28: // equalZero
                {
                    string var = ReadVarRef();
                    CondJump(offset, var + " == 0");
                    break;
                }
                case 0x38: case 0xB8: // isLessEqual: body runs when value <= var
                {
                    string var = ReadVarRef();
                    string val = GetVarOrDirectWord(0x80);
                    CondJump(offset, var + " >= " + val);
                    break;
                }
                case 0x44: case 0xC4: // isLess: body runs when value < var
                {
                    string var = ReadVarRef();
                    string val = GetVarOrDirectWord(0x80);
                    CondJump(offset, var + " > " + val);
                    break;
                }
                case 0x48: case 0xC8: // isEqual
                {
                    string var = ReadVarRef();
                    string val = GetVarOrDirectWord(0x80);
                    CondJump(offset, var + " == " + val);
                    break;
                }
                case 0x78: case 0xF8: // isGreater: body runs when value > var
                {
                    string var = ReadVarRef();
                    string val = GetVarOrDirectWord(0x80);
                    CondJump(offset, var + " < " + val);
                    break;
                }
                case 0xA8: // notEqualZero
                {
                    string var = ReadVarRef();
                    CondJump(offset, var + " != 0");
                    break;
                }

                case 0x05: case 0x85: DrawObject(offset); break;

                case 0x25: case 0x65: case 0xA5: case 0xE5:
                {
                    string obj = GetVarOrDirectWord(0x80);
                    string room = GetVarOrDirectByte(0x40);
                    Emit(offset, "pickupObject(" + obj + ", " + room + ");");
                    break;
                }

                case 0x06: case 0x86:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorElevation(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x07: case 0x47: case 0x87: case 0xC7:
                {
                    string obj = GetVarOrDirectWord(0x80);
                    string state = GetVarOrDirectByte(0x40);
                    Emit(offset, "setState(" + obj + ", " + state + ");");
                    break;
                }

                case 0x09: case 0x49: case 0x89: case 0xC9:
                {
                    string actor = GetVarOrDirectByte(0x80);
                    string obj = GetVarOrDirectWord(0x40);
                    Emit(offset, "faceActor(" + actor + ", " + obj + ");");
                    break;
                }

                case 0x0A: case 0x2A: case 0x4A: case 0x6A:
                case 0x8A: case 0xAA: case 0xCA: case 0xEA:
                {
                    // bits 0x20/0x40 are flags here (freeze resistant / recursive), not param bits
                    string script = GetVarOrDirectByte(0x80);
                    string args = ReadWordVarArgs();
                    var flags = new List<string>();
                    if ((op & 0x20) != 0) flags.Add("freezeResistant");
                    if ((op & 0x40) != 0) flags.Add("recursive");
                    string suffix = flags.Count > 0 ? " // " + string.Join(", ", flags.ToArray()) : "";
                    Emit(offset, "startScript(" + script + ", " + args + ");" + suffix);
                    break;
                }

                case 0x0B: case 0x4B: case 0x8B: case 0xCB:
                {
                    string result = ReadVarRef();
                    string obj = GetVarOrDirectWord(0x80);
                    string verb = GetVarOrDirectWord(0x40);
                    SetResult(offset, result, "getVerbEntrypoint(" + obj + ", " + verb + ")");
                    break;
                }

                case 0x0C: case 0x8C: ResourceRoutines(offset); break;

                case 0x0D: case 0x4D: case 0x8D: case 0xCD:
                {
                    string a = GetVarOrDirectByte(0x80);
                    string b = GetVarOrDirectByte(0x40);
                    int dist = ReadByte();
                    Emit(offset, "walkActorToActor(" + a + ", " + b + ", " + dist + ");");
                    break;
                }

                case 0x0E: case 0x4E: case 0x8E: case 0xCE:
                {
                    string actor = GetVarOrDirectByte(0x80);
                    string obj = GetVarOrDirectWord(0x40);
                    Emit(offset, "putActorAtObject(" + actor + ", " + obj + ");");
                    break;
                }

                case 0x0F: case 0x8F:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getObjectState(" + GetVarOrDirectWord(0x80) + ")");
                    break;
                }

                case 0x10: case 0x90:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getObjectOwner(" + GetVarOrDirectWord(0x80) + ")");
                    break;
                }

                case 0x11: case 0x51: case 0x91: case 0xD1:
                {
                    string actor = GetVarOrDirectByte(0x80);
                    string anim = GetVarOrDirectByte(0x40);
                    Emit(offset, "animateActor(" + actor + ", " + anim + ");");
                    break;
                }

                case 0x12: case 0x92: Emit(offset, "panCameraTo(" + GetVarOrDirectWord(0x80) + ");"); break;

                case 0x13: case 0x53: case 0x93: case 0xD3: ActorOps(offset); break;

                case 0x14: case 0x94: // print(actor, ...)
                {
                    string actor = GetVarOrDirectByte(0x80);
                    PrintOps(offset, "print(" + actor + ")");
                    break;
                }

                case 0x15: case 0x55: case 0x95: case 0xD5:
                {
                    string result = ReadVarRef();
                    string x = GetVarOrDirectWord(0x80);
                    string y = GetVarOrDirectWord(0x40);
                    SetResult(offset, result, "actorFromPos(" + x + ", " + y + ")");
                    break;
                }

                case 0x16: case 0x96:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getRandomNr(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x17: case 0x97: BinaryAssign(offset, "&="); break;
                case 0x1A: case 0x9A: BinaryAssign(offset, "="); break;
                case 0x1B: case 0x9B: BinaryAssign(offset, "*="); break;
                case 0x3A: case 0xBA: BinaryAssign(offset, "-="); break;
                case 0x57: case 0xD7: BinaryAssign(offset, "|="); break;
                case 0x5A: case 0xDA: BinaryAssign(offset, "+="); break;
                case 0x5B: case 0xDB: BinaryAssign(offset, "/="); break;

                case 0x18: Emit(offset, "goto " + Jump(offset) + ";"); break;

                case 0x19: case 0x39: case 0x59: case 0x79:
                case 0x99: case 0xB9: case 0xD9: case 0xF9: DoSentence(offset); break;

                case 0x1C: case 0x9C: Emit(offset, "startSound(" + GetVarOrDirectByte(0x80) + ");"); break;

                case 0x1D: case 0x9D: // ifClassOfIs
                {
                    string obj = GetVarOrDirectWord(0x80);
                    string classes = ReadWordVarArgs();
                    CondJump(offset, "classOfIs(" + obj + ", " + classes + ")");
                    break;
                }

                case 0x1E: case 0x3E: case 0x5E: case 0x7E:
                case 0x9E: case 0xBE: case 0xDE: case 0xFE:
                {
                    string actor = GetVarOrDirectByte(0x80);
                    string x = GetVarOrDirectWord(0x40);
                    string y = GetVarOrDirectWord(0x20);
                    Emit(offset, "walkActorTo(" + actor + ", " + x + ", " + y + ");");
                    break;
                }

                case 0x1F: case 0x5F: case 0x9F: case 0xDF: // isActorInBox
                {
                    string actor = GetVarOrDirectByte(0x80);
                    string box = GetVarOrDirectByte(0x40);
                    CondJump(offset, "isActorInBox(" + actor + ", " + box + ")");
                    break;
                }

                case 0x20: Emit(offset, "stopMusic();"); break;

                case 0x22: case 0xA2:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getAnimCounter(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x23: case 0xA3:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorY(" + GetVarOrDirectWord(0x80) + ")");
                    break;
                }

                case 0x24: case 0x64: case 0xA4: case 0xE4:
                {
                    string obj = GetVarOrDirectWord(0x80);
                    string room = GetVarOrDirectByte(0x40);
                    int x = ReadSignedWord();
                    int y = ReadSignedWord();
                    Emit(offset, "loadRoomWithEgo(" + obj + ", " + room + ", " + x + ", " + y + ");");
                    break;
                }

                case 0x26: case 0xA6: SetVarRange(offset); break;

                case 0x27: case 0xA7:
                    if (op == 0xA7) { Emit(offset, "dummy();"); break; }
                    StringOps(offset);
                    break;

                case 0x29: case 0x69: case 0xA9: case 0xE9:
                {
                    string obj = GetVarOrDirectWord(0x80);
                    string owner = GetVarOrDirectByte(0x40);
                    Emit(offset, "setOwnerOf(" + obj + ", " + owner + ");");
                    break;
                }

                case 0x2B: Emit(offset, "delayVariable(" + ReadVarRef() + ");"); break;

                case 0x2C: CursorCommand(offset); break;

                case 0x2D: case 0x6D: case 0xAD: case 0xED:
                {
                    string actor = GetVarOrDirectByte(0x80);
                    string room = GetVarOrDirectByte(0x40);
                    Emit(offset, "putActorInRoom(" + actor + ", " + room + ");");
                    break;
                }

                case 0x2E: // delay: 24-bit literal
                {
                    int a = ReadByte();
                    int b = ReadByte();
                    int c = ReadByte();
                    int delay = a | (b << 8) | (c << 16);
                    Emit(offset, "delay(" + delay + ");");
                    break;
                }

                case 0x30: case 0xB0: MatrixOps(offset); break;

                case 0x31: case 0xB1:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getInventoryCount(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x32: case 0xB2: Emit(offset, "setCameraAt(" + GetVarOrDirectWord(0x80) + ");"); break;

                case 0x33: case 0x73: case 0xB3: case 0xF3: RoomOps(offset); break;

                case 0x34: case 0x74: case 0xB4: case 0xF4:
                {
                    string result = ReadVarRef();
                    string a = GetVarOrDirectWord(0x80);
                    string b = GetVarOrDirectWord(0x40);
                    SetResult(offset, result, "getDist(" + a + ", " + b + ")");
                    break;
                }

                case 0x35: case 0x75: case 0xB5: case 0xF5:
                {
                    string result = ReadVarRef();
                    string x = GetVarOrDirectByte(0x80);
                    string y = GetVarOrDirectByte(0x40);
                    SetResult(offset, result, "findObject(" + x + ", " + y + ")");
                    break;
                }

                case 0x36: case 0x76: case 0xB6: case 0xF6:
                {
                    string actor = GetVarOrDirectByte(0x80);
                    string obj = GetVarOrDirectWord(0x40);
                    Emit(offset, "walkActorToObject(" + actor + ", " + obj + ");");
                    break;
                }

                case 0x37: case 0x77: case 0xB7: case 0xF7:
                {
                    string obj = GetVarOrDirectWord(0x80);
                    string script = GetVarOrDirectByte(0x40);
                    string args = ReadWordVarArgs();
                    Emit(offset, "startObject(" + obj + ", " + script + ", " + args + ");");
                    break;
                }

                case 0x3B: case 0xBB:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorScale(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x3C: case 0xBC: Emit(offset, "stopSound(" + GetVarOrDirectByte(0x80) + ");"); break;

                case 0x3D: case 0x7D: case 0xBD: case 0xFD:
                {
                    string result = ReadVarRef();
                    string owner = GetVarOrDirectByte(0x80);
                    string index = GetVarOrDirectByte(0x40);
                    SetResult(offset, result, "findInventory(" + owner + ", " + index + ")");
                    break;
                }

                case 0x3F: case 0x7F: case 0xBF: case 0xFF: DrawBox(offset); break;

                case 0x40: Emit(offset, "cutscene(" + ReadWordVarArgs() + ");"); break;
                case 0xC0: Emit(offset, "endCutscene();"); break;

                case 0x42: case 0xC2:
                {
                    string script = GetVarOrDirectByte(0x80);
                    string args = ReadWordVarArgs();
                    Emit(offset, "chainScript(" + script + ", " + args + ");");
                    break;
                }

                case 0x43: case 0xC3:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorX(" + GetVarOrDirectWord(0x80) + ")");
                    break;
                }

                case 0x46: Emit(offset, ReadVarRef() + "++;"); break;
                case 0xC6: Emit(offset, ReadVarRef() + "--;"); break;

                case 0x4C: Emit(offset, "soundKludge(" + ReadWordVarArgs() + ");"); break;

                case 0x52: case 0xD2: Emit(offset, "actorFollowCamera(" + GetVarOrDirectByte(0x80) + ");"); break;

                case 0x54: case 0xD4:
                {
                    string obj = GetVarOrDirectWord(0x80);
                    string name = ReadString("objectName");
                    Emit(offset, "setObjectName(" + obj + ", " + name + ");");
                    break;
                }

                case 0x56: case 0xD6:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorMoving(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x58: // beginOverride / endOverride (the override jump follows as a normal goto)
                {
                    int mode = ReadByte();
                    Emit(offset, mode != 0 ? "beginOverride();" : "endOverride();");
                    break;
                }

                case 0x5D: case 0xDD:
                {
                    string obj = GetVarOrDirectWord(0x80);
                    string classes = ReadWordVarArgs();
                    Emit(offset, "setClass(" + obj + ", " + classes + ");");
                    break;
                }

                case 0x60: case 0xE0: Emit(offset, "freezeScripts(" + GetVarOrDirectByte(0x80) + ");"); break;

                case 0x62: case 0xE2: Emit(offset, "stopScript(" + GetVarOrDirectByte(0x80) + ");"); break;

                case 0x63: case 0xE3:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorFacing(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x66: case 0xE6:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getClosestObjActor(" + GetVarOrDirectWord(0x80) + ")");
                    break;
                }

                case 0x67: case 0xE7:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getStringWidth(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x68: case 0xE8:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "isScriptRunning(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x6B: case 0xEB: Emit(offset, "debug(" + GetVarOrDirectWord(0x80) + ");"); break;

                case 0x6C: case 0xEC:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorWidth(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x6E: case 0xEE: Emit(offset, "stopObjectScript(" + GetVarOrDirectByte(0x80) + ");"); break;

                case 0x70: case 0xF0:
                {
                    string a = GetVarOrDirectByte(0x80);
                    int b = ReadByte();
                    int c = ReadByte();
                    Emit(offset, "lights(" + a + ", " + b + ", " + c + ");");
                    break;
                }

                case 0x71: case 0xF1:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorCostume(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x72: case 0xF2: Emit(offset, "loadRoom(" + GetVarOrDirectByte(0x80) + ");"); break;

                case 0x7A: case 0xFA: VerbOps(offset); break;

                case 0x7B: case 0xFB:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "getActorWalkBox(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x7C: case 0xFC:
                {
                    string result = ReadVarRef();
                    SetResult(offset, result, "isSoundRunning(" + GetVarOrDirectByte(0x80) + ")");
                    break;
                }

                case 0x80: Emit(offset, "breakHere();"); break;

                case 0x98: // systemOps
                {
                    int sub = ReadByte();
                    string name;
                    if (sub == 1) name = "restartGame";
                    else if (sub == 2) name = "pauseGame";
                    else if (sub == 3) name = "quitGame";
                    else name = "op_0x" + sub.ToString("X2");
                    Emit(offset, "systemOps." + name + "();");
                    break;
                }

                case 0xAB: SaveRestoreVerbs(offset); break;

                case 0xAC: Expression(offset); break;

                case 0xAE: WaitOps(offset); break;

                case 0xCC: PseudoRoom(offset); break;

                case 0xD8: PrintOps(offset, "printEgo()"); break;

                default:
                    Unknown(op, offset);
                    break;
            }
        }

        private void Unknown(byte op, int offset)
        {
            _unknown.Add(op);
            Emit(offset, "; <unknown opcode 0x" + op.ToString("X2") + " - disassembly stopped>");
            _stopped = true;
        }

        // -------------------------------------------------------------------------
        // Compound opcodes
        // -------------------------------------------------------------------------

        private void BinaryAssign(int offset, string assignOp)
        {
            string var = ReadVarRef();
            string val = GetVarOrDirectWord(0x80);
            Emit(offset, var + " " + assignOp + " " + val + ";");
        }

        private void DoSentence(int offset)
        {
            // a literal verb 0xFE means "stop sentence" and has no further parameters
            if ((_op & 0x80) == 0 && _code[_pos] == 0xFE)
            {
                _pos++;
                Emit(offset, "doSentence(STOP);");
                return;
            }
            string verb = GetVarOrDirectByte(0x80);
            string objA = GetVarOrDirectWord(0x40);
            string objB = GetVarOrDirectWord(0x20);
            Emit(offset, "doSentence(" + verb + ", " + objA + ", " + objB + ");");
        }

        private void DrawObject(int offset)
        {
            string obj = GetVarOrDirectWord(0x80);
            byte sub = ReadByte();
            switch (sub & 0x1F)
            {
                case 1:
                {
                    string x = GetVarOrDirectWordAux(sub, 0x80);
                    string y = GetVarOrDirectWordAux(sub, 0x40);
                    Emit(offset, "drawObject(" + obj + ", at: " + x + ", " + y + ");");
                    break;
                }
                case 2:
                {
                    string state = GetVarOrDirectWordAux(sub, 0x80);
                    Emit(offset, "drawObject(" + obj + ", state: " + state + ");");
                    break;
                }
                case 0x1F:
                    Emit(offset, "drawObject(" + obj + ");");
                    break;
                default:
                    Emit(offset, "drawObject(" + obj + ", op_0x" + (sub & 0x1F).ToString("X2") + ");");
                    break;
            }
        }

        private void SetVarRange(int offset)
        {
            string var = ReadVarRef();
            int count = ReadByte();
            var values = new List<string>();
            for (int i = 0; i < count; i++)
            {
                if ((_op & 0x80) != 0) values.Add(ReadSignedWord().ToString());
                else values.Add(ReadByte().ToString());
            }
            Emit(offset, "setVarRange(" + var + ", [" + string.Join(", ", values.ToArray()) + "]);");
        }

        private void PseudoRoom(int offset)
        {
            int room = ReadByte();
            var mapped = new List<string>();
            while (true)
            {
                int b = ReadByte();
                if (b == 0) break;
                mapped.Add((b & 0x7F).ToString());
                if (mapped.Count > 64) break; // defensive
            }
            Emit(offset, "pseudoRoom(" + room + ", [" + string.Join(", ", mapped.ToArray()) + "]);");
        }

        private void DrawBox(int offset)
        {
            string x = GetVarOrDirectWord(0x80);
            string y = GetVarOrDirectWord(0x40);
            byte aux = ReadByte();
            string x2 = GetVarOrDirectWordAux(aux, 0x80);
            string y2 = GetVarOrDirectWordAux(aux, 0x40);
            string color = GetVarOrDirectByteAux(aux, 0x20);
            Emit(offset, "drawBox(" + x + ", " + y + ", " + x2 + ", " + y2 + ", " + color + ");");
        }

        private void Expression(int offset)
        {
            string result = ReadVarRef();
            var stack = new List<string>();

            while (true)
            {
                byte item = ReadByte();
                if (item == 0xFF) break;

                switch (item & 0x1F)
                {
                    case 1:
                        stack.Add(GetVarOrDirectWordAux(item, 0x80));
                        break;
                    case 2: ExpressionOp(stack, "+"); break;
                    case 3: ExpressionOp(stack, "-"); break;
                    case 4: ExpressionOp(stack, "*"); break;
                    case 5: ExpressionOp(stack, "/"); break;
                    case 6:
                    {
                        // a nested opcode whose result is pushed on the expression stack
                        int nestedOffset = _pos;
                        byte nestedOp = ReadByte();
                        byte savedOp = _op;
                        _op = nestedOp;
                        _nestedResult = "";
                        Decode(nestedOp, nestedOffset);
                        _op = savedOp;
                        stack.Add(_nestedResult.Length > 0 ? _nestedResult : "<?>");
                        _nestedResult = null;
                        break;
                    }
                    default:
                        stack.Add("<expr_op_0x" + (item & 0x1F).ToString("X2") + ">");
                        break;
                }
                if (stack.Count > 64) break; // defensive
            }

            string expr = stack.Count > 0 ? stack[stack.Count - 1] : "<empty>";
            Emit(offset, result + " = " + Scumm6Disassembler.StripParens(expr) + ";");
        }

        private static void ExpressionOp(List<string> stack, string op)
        {
            string b = stack.Count > 0 ? stack[stack.Count - 1] : "?";
            if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            string a = stack.Count > 0 ? stack[stack.Count - 1] : "?";
            if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
            stack.Add("(" + a + " " + op + " " + b + ")");
        }

        // -------------------------------------------------------------------------
        // Sub-opcode groups
        // -------------------------------------------------------------------------

        private void ActorOps(int offset)
        {
            string actor = GetVarOrDirectByte(0x80);
            var parts = new List<string>();

            while (true)
            {
                byte sub = ReadByte();
                if (sub == 0xFF) break;

                switch (sub & 0x1F)
                {
                    case 0: parts.Add("dummy(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 1: parts.Add("costume(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 2: parts.Add("stepDist(" + GetVarOrDirectByteAux(sub, 0x80) + ", " + GetVarOrDirectByteAux(sub, 0x40) + ")"); break;
                    case 3: parts.Add("sound(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 4: parts.Add("walkAnim(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 5: parts.Add("talkAnim(" + GetVarOrDirectByteAux(sub, 0x80) + ", " + GetVarOrDirectByteAux(sub, 0x40) + ")"); break;
                    case 6: parts.Add("standAnim(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 7: parts.Add("animations(" + GetVarOrDirectByteAux(sub, 0x80) + ", " + GetVarOrDirectByteAux(sub, 0x40) + ", " + GetVarOrDirectByteAux(sub, 0x20) + ")"); break;
                    case 8: parts.Add("init()"); break;
                    case 9: parts.Add("elevation(" + GetVarOrDirectWordAux(sub, 0x80) + ")"); break;
                    case 10: parts.Add("defaultAnims()"); break;
                    case 11: parts.Add("palette(" + GetVarOrDirectByteAux(sub, 0x80) + ", " + GetVarOrDirectByteAux(sub, 0x40) + ")"); break;
                    case 12: parts.Add("talkColor(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 13: parts.Add("setName(" + ReadString("actorName") + ")"); break;
                    case 14: parts.Add("initAnim(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 16: parts.Add("width(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 17: parts.Add("scale(" + GetVarOrDirectByteAux(sub, 0x80) + ", " + GetVarOrDirectByteAux(sub, 0x40) + ")"); break;
                    case 18: parts.Add("neverZClip()"); break;
                    case 19: parts.Add("setZClip(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 20: parts.Add("ignoreBoxes()"); break;
                    case 21: parts.Add("followBoxes()"); break;
                    case 22: parts.Add("animSpeed(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 23: parts.Add("shadow(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    default: parts.Add("op_0x" + (sub & 0x1F).ToString("X2") + "()"); break;
                }
                if (parts.Count > 32) break; // defensive
            }

            Emit(offset, "actorOps(" + actor + ", " + string.Join(", ", parts.ToArray()) + ");");
        }

        private void VerbOps(int offset)
        {
            string verb = GetVarOrDirectByte(0x80);
            var parts = new List<string>();

            while (true)
            {
                byte sub = ReadByte();
                if (sub == 0xFF) break;

                switch (sub & 0x1F)
                {
                    case 1: parts.Add("image(" + GetVarOrDirectWordAux(sub, 0x80) + ")"); break;
                    case 2: parts.Add("text(" + ReadString("verbName") + ")"); break;
                    case 3: parts.Add("color(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 4: parts.Add("hiColor(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 5: parts.Add("at(" + GetVarOrDirectWordAux(sub, 0x80) + ", " + GetVarOrDirectWordAux(sub, 0x40) + ")"); break;
                    case 6: parts.Add("on()"); break;
                    case 7: parts.Add("off()"); break;
                    case 8: parts.Add("delete()"); break;
                    case 9: parts.Add("new()"); break;
                    case 16: parts.Add("dimColor(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 17: parts.Add("dim()"); break;
                    case 18: parts.Add("key(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 19: parts.Add("center()"); break;
                    case 20: parts.Add("setToString(" + GetVarOrDirectWordAux(sub, 0x80) + ")"); break;
                    case 22: parts.Add("setToObject(" + GetVarOrDirectWordAux(sub, 0x80) + ", " + GetVarOrDirectByteAux(sub, 0x40) + ")"); break;
                    case 23: parts.Add("backColor(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    default: parts.Add("op_0x" + (sub & 0x1F).ToString("X2") + "()"); break;
                }
                if (parts.Count > 32) break; // defensive
            }

            Emit(offset, "verbOps(" + verb + ", " + string.Join(", ", parts.ToArray()) + ");");
        }

        private void PrintOps(int offset, string group)
        {
            var parts = new List<string>();

            while (true)
            {
                byte sub = ReadByte();
                if (sub == 0xFF) break;

                bool endsHere = false;
                switch (sub & 0x1F)
                {
                    case 0: parts.Add("at(" + GetVarOrDirectWordAux(sub, 0x80) + ", " + GetVarOrDirectWordAux(sub, 0x40) + ")"); break;
                    case 1: parts.Add("color(" + GetVarOrDirectByteAux(sub, 0x80) + ")"); break;
                    case 2: parts.Add("clipped(" + GetVarOrDirectWordAux(sub, 0x80) + ")"); break;
                    case 3: parts.Add("erase(" + GetVarOrDirectWordAux(sub, 0x80) + ", " + GetVarOrDirectWordAux(sub, 0x40) + ")"); break;
                    case 4: parts.Add("center()"); break;
                    case 6: parts.Add("left()"); break;
                    case 7: parts.Add("overhead()"); break;
                    case 8: parts.Add("playCDTrack(" + GetVarOrDirectWordAux(sub, 0x80) + ", " + GetVarOrDirectWordAux(sub, 0x40) + ")"); break;
                    case 15:
                        parts.Add("text(" + ReadString("talk") + ")");
                        endsHere = true; // the text sub-op ends the print instruction (no 0xFF)
                        break;
                    default:
                        parts.Add("op_0x" + (sub & 0x1F).ToString("X2") + "()");
                        break;
                }
                if (endsHere) break;
                if (parts.Count > 32) break; // defensive
            }

            Emit(offset, group.Replace("()", "") + "." + string.Join(".", parts.ToArray()) + ";");
        }

        private void StringOps(int offset)
        {
            byte sub = ReadByte();
            switch (sub & 0x1F)
            {
                case 1:
                {
                    string id = GetVarOrDirectByteAux(sub, 0x80);
                    string text = ReadString("string");
                    Emit(offset, "stringOps.putCodeInString(" + id + ", " + text + ");");
                    break;
                }
                case 2:
                {
                    string a = GetVarOrDirectByteAux(sub, 0x80);
                    string b = GetVarOrDirectByteAux(sub, 0x40);
                    Emit(offset, "stringOps.copyString(" + a + ", " + b + ");");
                    break;
                }
                case 3:
                {
                    string id = GetVarOrDirectByteAux(sub, 0x80);
                    string index = GetVarOrDirectByteAux(sub, 0x40);
                    string ch = GetVarOrDirectByteAux(sub, 0x20);
                    Emit(offset, "stringOps.setStringChar(" + id + ", " + index + ", " + ch + ");");
                    break;
                }
                case 4:
                {
                    string result = ReadVarRef();
                    string id = GetVarOrDirectByteAux(sub, 0x80);
                    string index = GetVarOrDirectByteAux(sub, 0x40);
                    SetResult(offset, result, "stringOps.getStringChar(" + id + ", " + index + ")");
                    break;
                }
                case 5:
                {
                    string id = GetVarOrDirectByteAux(sub, 0x80);
                    string size = GetVarOrDirectByteAux(sub, 0x40);
                    Emit(offset, "stringOps.createString(" + id + ", " + size + ");");
                    break;
                }
                default:
                    Emit(offset, "stringOps.op_0x" + (sub & 0x1F).ToString("X2") + "();");
                    break;
            }
        }

        private void CursorCommand(int offset)
        {
            byte sub = ReadByte();
            switch (sub & 0x1F)
            {
                case 1: Emit(offset, "cursorCommand.cursorOn();"); break;
                case 2: Emit(offset, "cursorCommand.cursorOff();"); break;
                case 3: Emit(offset, "cursorCommand.userPutOn();"); break;
                case 4: Emit(offset, "cursorCommand.userPutOff();"); break;
                case 5: Emit(offset, "cursorCommand.softCursorOn();"); break;
                case 6: Emit(offset, "cursorCommand.softCursorOff();"); break;
                case 7: Emit(offset, "cursorCommand.softUserPutOn();"); break;
                case 8: Emit(offset, "cursorCommand.softUserPutOff();"); break;
                case 10:
                {
                    string cursor = GetVarOrDirectByteAux(sub, 0x80);
                    string img = GetVarOrDirectByteAux(sub, 0x40);
                    Emit(offset, "cursorCommand.setCursorImg(" + cursor + ", " + img + ");");
                    break;
                }
                case 11:
                {
                    string a = GetVarOrDirectByteAux(sub, 0x80);
                    string b = GetVarOrDirectByteAux(sub, 0x40);
                    string c = GetVarOrDirectByteAux(sub, 0x20);
                    Emit(offset, "cursorCommand.setCursorHotspot(" + a + ", " + b + ", " + c + ");");
                    break;
                }
                case 12: Emit(offset, "cursorCommand.initCursor(" + GetVarOrDirectByteAux(sub, 0x80) + ");"); break;
                case 13: Emit(offset, "cursorCommand.initCharset(" + GetVarOrDirectByteAux(sub, 0x80) + ");"); break;
                case 14: Emit(offset, "cursorCommand.charsetColors(" + ReadWordVarArgs() + ");"); break;
                default:
                    Emit(offset, "cursorCommand.op_0x" + (sub & 0x1F).ToString("X2") + "();");
                    break;
            }
        }

        private void ResourceRoutines(int offset)
        {
            byte sub = ReadByte();
            int code = sub & 0x3F;

            string resId = null;
            if (code != 17) resId = GetVarOrDirectByteAux(sub, 0x80);

            string name;
            switch (code)
            {
                case 1: name = "loadScript"; break;
                case 2: name = "loadSound"; break;
                case 3: name = "loadCostume"; break;
                case 4: name = "loadRoom"; break;
                case 5: name = "nukeScript"; break;
                case 6: name = "nukeSound"; break;
                case 7: name = "nukeCostume"; break;
                case 8: name = "nukeRoom"; break;
                case 9: name = "lockScript"; break;
                case 10: name = "lockSound"; break;
                case 11: name = "lockCostume"; break;
                case 12: name = "lockRoom"; break;
                case 13: name = "unlockScript"; break;
                case 14: name = "unlockSound"; break;
                case 15: name = "unlockCostume"; break;
                case 16: name = "unlockRoom"; break;
                case 17: name = "clearHeap"; break;
                case 18: name = "loadCharset"; break;
                case 19: name = "nukeCharset"; break;
                case 20:
                {
                    string room = GetVarOrDirectWordAux(sub, 0x40);
                    Emit(offset, "resourceRoutines.loadFlObject(" + resId + ", " + room + ");");
                    return;
                }
                default: name = "op_0x" + code.ToString("X2"); break;
            }

            if (resId == null) Emit(offset, "resourceRoutines." + name + "();");
            else Emit(offset, "resourceRoutines." + name + "(" + resId + ");");
        }

        private void MatrixOps(int offset)
        {
            byte sub = ReadByte();
            switch (sub & 0x1F)
            {
                case 1:
                {
                    string box = GetVarOrDirectByteAux(sub, 0x80);
                    string val = GetVarOrDirectByteAux(sub, 0x40);
                    Emit(offset, "matrixOps.setBoxFlags(" + box + ", " + val + ");");
                    break;
                }
                case 2:
                case 3:
                {
                    string box = GetVarOrDirectByteAux(sub, 0x80);
                    string val = GetVarOrDirectByteAux(sub, 0x40);
                    string name = (sub & 0x1F) == 2 ? "setBoxScale" : "setBoxScaleSlot";
                    Emit(offset, "matrixOps." + name + "(" + box + ", " + val + ");");
                    break;
                }
                case 4: Emit(offset, "matrixOps.createBoxMatrix();"); break;
                default:
                    Emit(offset, "matrixOps.op_0x" + (sub & 0x1F).ToString("X2") + "();");
                    break;
            }
        }

        private void RoomOps(int offset)
        {
            byte sub = ReadByte();
            switch (sub & 0x1F)
            {
                case 1:
                {
                    string a = GetVarOrDirectWordAux(sub, 0x80);
                    string b = GetVarOrDirectWordAux(sub, 0x40);
                    Emit(offset, "roomOps.roomScroll(" + a + ", " + b + ");");
                    break;
                }
                case 3:
                {
                    string a = GetVarOrDirectWordAux(sub, 0x80);
                    string b = GetVarOrDirectWordAux(sub, 0x40);
                    Emit(offset, "roomOps.setScreen(" + a + ", " + b + ");");
                    break;
                }
                case 4:
                {
                    string r = GetVarOrDirectWordAux(sub, 0x80);
                    string g = GetVarOrDirectWordAux(sub, 0x40);
                    string b = GetVarOrDirectWordAux(sub, 0x20);
                    byte aux = ReadByte();
                    string index = GetVarOrDirectByteAux(aux, 0x80);
                    Emit(offset, "roomOps.setPalColor(" + r + ", " + g + ", " + b + ", " + index + ");");
                    break;
                }
                case 5: Emit(offset, "roomOps.shakeOn();"); break;
                case 6: Emit(offset, "roomOps.shakeOff();"); break;
                case 8:
                {
                    string a = GetVarOrDirectByteAux(sub, 0x80);
                    string b = GetVarOrDirectByteAux(sub, 0x40);
                    string c = GetVarOrDirectByteAux(sub, 0x20);
                    Emit(offset, "roomOps.intensity(" + a + ", " + b + ", " + c + ");");
                    break;
                }
                case 9:
                {
                    string flag = GetVarOrDirectByteAux(sub, 0x80);
                    string slot = GetVarOrDirectByteAux(sub, 0x40);
                    Emit(offset, "roomOps.saveLoad(" + flag + ", " + slot + ");");
                    break;
                }
                case 10: Emit(offset, "roomOps.screenEffect(" + GetVarOrDirectWordAux(sub, 0x80) + ");"); break;
                case 11:
                {
                    string r = GetVarOrDirectWordAux(sub, 0x80);
                    string g = GetVarOrDirectWordAux(sub, 0x40);
                    string b = GetVarOrDirectWordAux(sub, 0x20);
                    byte aux = ReadByte();
                    string start = GetVarOrDirectByteAux(aux, 0x80);
                    string end = GetVarOrDirectByteAux(aux, 0x40);
                    Emit(offset, "roomOps.setRGBIntensity(" + r + ", " + g + ", " + b + ", " + start + ", " + end + ");");
                    break;
                }
                case 12:
                {
                    string r = GetVarOrDirectWordAux(sub, 0x80);
                    string g = GetVarOrDirectWordAux(sub, 0x40);
                    string b = GetVarOrDirectWordAux(sub, 0x20);
                    byte aux = ReadByte();
                    string start = GetVarOrDirectByteAux(aux, 0x80);
                    string end = GetVarOrDirectByteAux(aux, 0x40);
                    Emit(offset, "roomOps.setShadowPalette(" + r + ", " + g + ", " + b + ", " + start + ", " + end + ");");
                    break;
                }
                case 13:
                {
                    string slot = GetVarOrDirectByteAux(sub, 0x80);
                    string text = ReadString("file");
                    Emit(offset, "roomOps.saveString(" + slot + ", " + text + ");");
                    break;
                }
                case 14:
                {
                    string slot = GetVarOrDirectByteAux(sub, 0x80);
                    string text = ReadString("file");
                    Emit(offset, "roomOps.loadString(" + slot + ", " + text + ");");
                    break;
                }
                case 15:
                {
                    string a = GetVarOrDirectByteAux(sub, 0x80);
                    byte aux = ReadByte();
                    string b = GetVarOrDirectByteAux(aux, 0x80);
                    string c = GetVarOrDirectByteAux(aux, 0x40);
                    byte aux2 = ReadByte();
                    string d = GetVarOrDirectByteAux(aux2, 0x80);
                    Emit(offset, "roomOps.palManipulate(" + a + ", " + b + ", " + c + ", " + d + ");");
                    break;
                }
                case 16:
                {
                    string a = GetVarOrDirectByteAux(sub, 0x80);
                    string b = GetVarOrDirectByteAux(sub, 0x40);
                    Emit(offset, "roomOps.colorCycleDelay(" + a + ", " + b + ");");
                    break;
                }
                default:
                    Emit(offset, "roomOps.op_0x" + (sub & 0x1F).ToString("X2") + "();");
                    break;
            }
        }

        private void SaveRestoreVerbs(int offset)
        {
            byte sub = ReadByte();
            string a = GetVarOrDirectByteAux(sub, 0x80);
            string b = GetVarOrDirectByteAux(sub, 0x40);
            string c = GetVarOrDirectByteAux(sub, 0x20);

            string name;
            switch (sub & 0x1F)
            {
                case 1: name = "saveVerbs"; break;
                case 2: name = "restoreVerbs"; break;
                case 3: name = "deleteVerbs"; break;
                default: name = "op_0x" + (sub & 0x1F).ToString("X2"); break;
            }
            Emit(offset, "saveRestoreVerbs." + name + "(" + a + ", " + b + ", " + c + ");");
        }

        private void WaitOps(int offset)
        {
            byte sub = ReadByte();
            switch (sub & 0x1F)
            {
                case 1: Emit(offset, "wait.waitForActor(" + GetVarOrDirectByteAux(sub, 0x80) + ");"); break;
                case 2: Emit(offset, "wait.waitForMessage();"); break;
                case 3: Emit(offset, "wait.waitForCamera();"); break;
                case 4: Emit(offset, "wait.waitForSentence();"); break;
                default:
                    Emit(offset, "wait.op_0x" + (sub & 0x1F).ToString("X2") + "();");
                    break;
            }
        }
    }
}
