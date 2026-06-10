using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ScummEditor.Encoders
{
    /// <summary>
    /// Disassembles SCUMM v6 bytecode (DOTT / Sam &amp; Max scripts) into a readable, C#-like
    /// listing. It is a descumm-style decoder: the stack-based opcodes are reconstructed into
    /// expressions and statements, with unconditional/conditional jumps rendered as goto/labels.
    ///
    /// This is a best-effort disassembler, not a perfect decompiler: control flow is shown with
    /// labels (not reconstructed if/while), and a few rare opcodes may be incomplete. Decoding
    /// stops cleanly at the first unknown opcode so the output never desynchronises into garbage.
    /// </summary>
    public class Scumm6Disassembler
    {
        public class Result
        {
            public string Listing { get; set; }
            public bool DecodedToEnd { get; set; }
            public List<int> UnknownOpcodes { get; set; }
            public int BytesDecoded { get; set; }
        }

        private byte[] _code;
        private int _pos;
        private readonly List<string> _stack = new List<string>();
        private readonly List<Line> _lines = new List<Line>();
        private readonly HashSet<int> _jumpTargets = new HashSet<int>();
        private readonly List<int> _unknown = new List<int>();
        private bool _stopped;

        private struct Line
        {
            public int Offset;
            public string Text;
        }

        public static Result Disassemble(byte[] code, int startOffset)
        {
            var d = new Scumm6Disassembler();
            return d.Run(code, startOffset);
        }

        private Result Run(byte[] code, int startOffset)
        {
            _code = code;
            _pos = startOffset;

            while (_pos < _code.Length && !_stopped)
            {
                int offset = _pos;
                byte op = ReadByte();
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

            return new Result
            {
                Listing = Render(),
                DecodedToEnd = !_stopped && _pos >= _code.Length,
                UnknownOpcodes = _unknown,
                BytesDecoded = _pos - startOffset
            };
        }

        // -------------------------------------------------------------------------
        // Stack / emit helpers
        // -------------------------------------------------------------------------

        private void Push(string expr) { _stack.Add(expr); }

        private string Pop()
        {
            if (_stack.Count == 0) return "STACK_UNDERFLOW";
            string v = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            return v;
        }

        private void Emit(int offset, string text)
        {
            _lines.Add(new Line { Offset = offset, Text = text });
        }

        // A value-producing call: pops argc args (left-to-right) and pushes the call expression.
        private void PushCall(string name, int argc)
        {
            Push(name + "(" + Args(argc) + ")");
        }

        // A statement call: pops argc args and emits "name(args);".
        private void StmtCall(int offset, string name, int argc)
        {
            Emit(offset, name + "(" + Args(argc) + ");");
        }

        private string Args(int argc)
        {
            var parts = new string[argc];
            for (int i = argc - 1; i >= 0; i--) parts[i] = Pop();
            return string.Join(", ", parts);
        }

        // Several opcodes take a variable-length "stack list": the element count is pushed last,
        // then that many values were pushed before it. Renders as "[a, b, c]". When the count is
        // not a literal (a variable/expression) it is shown as "[*<count>]".
        private string PopStackList()
        {
            string countExpr = Pop();
            int count;
            if (int.TryParse(countExpr, out count) && count >= 0 && count <= 128)
            {
                var parts = new string[count];
                for (int i = count - 1; i >= 0; i--) parts[i] = Pop();
                return "[" + string.Join(", ", parts) + "]";
            }
            return "[*" + countExpr + "]";
        }

        private void Binary(string op)
        {
            string b = Pop();
            string a = Pop();
            Push("(" + a + " " + op + " " + b + ")");
        }

        // --- readability: drop redundant parentheses / invert negated comparisons ---

        // Pairs of comparison operators and their logical negation (longer ops listed first).
        private static readonly string[][] CompOps =
        {
            new[] { " <= ", " > " }, new[] { " >= ", " < " },
            new[] { " == ", " != " }, new[] { " != ", " == " },
            new[] { " < ", " >= " }, new[] { " > ", " <= " }
        };

        // Removes one outer parenthesis pair when it wraps the whole expression.
        private static string StripParens(string e)
        {
            if (e == null || e.Length < 2 || e[0] != '(' || e[e.Length - 1] != ')') return e;
            int depth = 0;
            for (int i = 0; i < e.Length; i++)
            {
                if (e[i] == '(') depth++;
                else if (e[i] == ')')
                {
                    depth--;
                    if (depth == 0) return i == e.Length - 1 ? e.Substring(1, e.Length - 2) : e;
                }
            }
            return e;
        }

        // Logical negation of an "ifNot" condition: !!x -> x, !(a == b) -> a != b, etc.
        private static string NegateCondition(string cond)
        {
            string s = StripParens(cond);
            if (s.StartsWith("!")) return StripParens(s.Substring(1));

            foreach (string[] pair in CompOps)
            {
                int idx = TopLevelIndexOf(s, pair[0]);
                if (idx >= 0) return s.Substring(0, idx) + pair[1] + s.Substring(idx + pair[0].Length);
            }
            // A single atom (variable or call, no top-level operator) needs no wrapping parens.
            return TopLevelIndexOf(s, " ") < 0 ? "!" + s : "!(" + s + ")";
        }

        // Index of the first occurrence of op outside any () or [] nesting, or -1.
        private static int TopLevelIndexOf(string s, string op)
        {
            int depth = 0;
            for (int i = 0; i + op.Length <= s.Length; i++)
            {
                char c = s[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (depth == 0 && string.CompareOrdinal(s, i, op, 0, op.Length) == 0) return i;
            }
            return -1;
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

        private static string Var(int var)
        {
            if ((var & 0x8000) != 0) return "Bit[" + (var & 0x7FFF) + "]";
            if ((var & 0x4000) != 0) return "Local[" + (var & 0x3FFF) + "]";
            return "Global[" + var + "]";
        }

        private string ReadVarName(bool word)
        {
            int var = word ? ReadWord() : ReadByte();
            return Var(var);
        }

        private string Jump(int offset)
        {
            int rel = ReadSignedWord();
            int target = _pos + rel;
            _jumpTargets.Add(target);
            return "L" + target.ToString("X4");
        }

        // Reads an inline SCUMM message string, returning a quoted C-like literal.
        private string ReadString()
        {
            var sb = new StringBuilder("\"");
            while (_pos < _code.Length)
            {
                byte b = ReadByte();
                if (b == 0) break;

                if (b == 0xFF || b == 0xFE)
                {
                    byte code = ReadByte();
                    switch (code)
                    {
                        case 1: sb.Append("\\n"); break;
                        case 2: sb.Append("\\keep"); break;
                        case 3: sb.Append("\\wait"); break;
                        case 8: sb.Append("\\escape"); break;
                        default:
                            int val = ReadWord();
                            sb.Append("\\x" + code.ToString("X2") + "[" + val + "]");
                            break;
                    }
                }
                else if (b == (byte)'"' || b == (byte)'\\')
                {
                    sb.Append('\\').Append((char)b);
                }
                else if (b >= 0x20 && b < 0x7F)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append("\\x" + b.ToString("X2"));
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        // -------------------------------------------------------------------------
        // Rendering
        // -------------------------------------------------------------------------

        private string Render()
        {
            var sb = new StringBuilder();
            foreach (Line line in _lines)
            {
                if (_jumpTargets.Contains(line.Offset))
                    sb.AppendLine("L" + line.Offset.ToString("X4") + ":");
                sb.AppendLine("    " + line.Offset.ToString("X4") + "  " + line.Text);
            }
            return sb.ToString();
        }

        // -------------------------------------------------------------------------
        // Opcode table
        // -------------------------------------------------------------------------

        private void Decode(byte op, int offset)
        {
            switch (op)
            {
                // ---- stack / values ----
                case 0x00: Push(ReadByte().ToString()); break;                                   // pushByte
                case 0x01: Push(ReadSignedWord().ToString()); break;                             // pushWord
                case 0x02: Push(ReadVarName(false)); break;                                      // pushByteVar
                case 0x03: Push(ReadVarName(true)); break;                                       // pushWordVar
                case 0x06: { string a = ReadVarName(false); Push(a + "[" + Pop() + "]"); break; } // byteArrayRead
                case 0x07: { string a = ReadVarName(true); Push(a + "[" + Pop() + "]"); break; }  // wordArrayRead
                case 0x0A: { string a = ReadVarName(false); string i = Pop(); string b = Pop(); Push(a + "[" + b + "][" + i + "]"); break; } // byteArrayIndexedRead
                case 0x0B: { string a = ReadVarName(true); string i = Pop(); string b = Pop(); Push(a + "[" + b + "][" + i + "]"); break; }  // wordArrayIndexedRead
                case 0x0C: { string v = Pop(); Push(v); Push(v); break; }                        // dup
                case 0x0D: Push("!" + Pop()); break;                                             // not
                case 0x0E: Binary("=="); break;
                case 0x0F: Binary("!="); break;
                case 0x10: Binary(">"); break;
                case 0x11: Binary("<"); break;
                case 0x12: Binary("<="); break;
                case 0x13: Binary(">="); break;
                case 0x14: Binary("+"); break;
                case 0x15: Binary("-"); break;
                case 0x16: Binary("*"); break;
                case 0x17: Binary("/"); break;
                case 0x18: Binary("&&"); break;
                case 0x19: Binary("||"); break;
                case 0x1A: Emit(offset, StripParens(Pop()) + ";"); break;                        // pop (discard / call result)

                // ---- variable / array writes ----
                case 0x42: { string v = ReadVarName(false); Emit(offset, v + " = " + StripParens(Pop()) + ";"); break; } // writeByteVar
                case 0x43: { string v = ReadVarName(true); Emit(offset, v + " = " + StripParens(Pop()) + ";"); break; }  // writeWordVar
                case 0x46: { string a = ReadVarName(false); string val = StripParens(Pop()); string idx = Pop(); Emit(offset, a + "[" + idx + "] = " + val + ";"); break; } // byteArrayWrite
                case 0x47: { string a = ReadVarName(true); string val = StripParens(Pop()); string idx = Pop(); Emit(offset, a + "[" + idx + "] = " + val + ";"); break; }  // wordArrayWrite
                case 0x4A: { string a = ReadVarName(false); string val = StripParens(Pop()); string idx = Pop(); string b = Pop(); Emit(offset, a + "[" + b + "][" + idx + "] = " + val + ";"); break; } // byteArrayIndexedWrite
                case 0x4B: { string a = ReadVarName(true); string val = StripParens(Pop()); string idx = Pop(); string b = Pop(); Emit(offset, a + "[" + b + "][" + idx + "] = " + val + ";"); break; }  // wordArrayIndexedWrite
                case 0x4E: Emit(offset, ReadVarName(false) + "++;"); break;                      // byteVarInc
                case 0x4F: Emit(offset, ReadVarName(true) + "++;"); break;                       // wordVarInc
                case 0x52: { string a = ReadVarName(false); Emit(offset, a + "[" + Pop() + "]++;"); break; } // byteArrayInc
                case 0x53: { string a = ReadVarName(true); Emit(offset, a + "[" + Pop() + "]++;"); break; }  // wordArrayInc
                case 0x56: Emit(offset, ReadVarName(false) + "--;"); break;                      // byteVarDec
                case 0x57: Emit(offset, ReadVarName(true) + "--;"); break;                       // wordVarDec
                case 0x5A: { string a = ReadVarName(false); Emit(offset, a + "[" + Pop() + "]--;"); break; } // byteArrayDec
                case 0x5B: { string a = ReadVarName(true); Emit(offset, a + "[" + Pop() + "]--;"); break; }  // wordArrayDec

                // ---- control flow ----
                case 0x5C: { string cond = Pop(); Emit(offset, "if (" + StripParens(cond) + ") goto " + Jump(offset) + ";"); break; }   // if (jumpTrue)
                case 0x5D: { string cond = Pop(); Emit(offset, "if (" + NegateCondition(cond) + ") goto " + Jump(offset) + ";"); break; } // ifNot (jumpFalse)
                case 0x73: Emit(offset, "goto " + Jump(offset) + ";"); break;                    // jump
                case 0x65: Emit(offset, "stopObjectCode();"); break;
                case 0x66: Emit(offset, "stopObjectCode();"); break;
                case 0x6C: Emit(offset, "breakHere();"); break;

                // ---- scripts / objects ----
                case 0x5E: { string a = PopStackList(); string s = Pop(); string f = Pop(); Emit(offset, "startScript(" + s + ", " + f + ", " + a + ");"); break; }
                case 0x5F: { string a = PopStackList(); string s = Pop(); Emit(offset, "startScriptQuick(" + s + ", " + a + ");"); break; }
                case 0x60: { string a = PopStackList(); string e = Pop(); string s = Pop(); Emit(offset, "startObject(" + s + ", " + e + ", " + a + ");"); break; }
                case 0x61: StmtCall(offset, "drawObject", 2); break;
                case 0x62: StmtCall(offset, "drawObjectAt", 3); break;
                case 0x63: StmtCall(offset, "drawBlastObject", 5); break;
                case 0x64: StmtCall(offset, "setBlastObjectWindow", 4); break;
                case 0x67: Emit(offset, "endCutscene();"); break;
                case 0x68: StmtCall(offset, "beginCutscene", 1); break;
                case 0x69: Emit(offset, "stopMusic();"); break;
                case 0x6A: StmtCall(offset, "freezeUnfreeze", 1); break;
                case 0x6D: { string cls = Pop(); string obj = Pop(); Push("classOfIs(" + obj + ", " + cls + ")"); break; }
                case 0x6E: StmtCall(offset, "setClass", 2); break;
                case 0x6F: Push("getState(" + Pop() + ")"); break;
                case 0x70: StmtCall(offset, "setState", 2); break;
                case 0x71: StmtCall(offset, "setOwner", 2); break;
                case 0x72: Push("getOwner(" + Pop() + ")"); break;
                case 0x74: StmtCall(offset, "startSound", 1); break;
                case 0x75: StmtCall(offset, "stopSound", 1); break;
                case 0x76: StmtCall(offset, "startMusic", 1); break;
                case 0x77: StmtCall(offset, "stopObjectScript", 1); break;
                case 0x7B: StmtCall(offset, "loadRoom", 1); break;
                case 0x7C: StmtCall(offset, "stopScript", 1); break;

                // ---- camera / actors ----
                case 0x78: StmtCall(offset, "panCameraTo", 1); break;
                case 0x79: StmtCall(offset, "actorFollowCamera", 1); break;
                case 0x7A: StmtCall(offset, "setCameraAt", 1); break;
                case 0x7D: StmtCall(offset, "walkActorToObj", 3); break;
                case 0x7E: StmtCall(offset, "walkActorTo", 3); break;
                case 0x7F: StmtCall(offset, "putActorInRoom", 4); break;
                case 0x80: StmtCall(offset, "putActorAtObject", 3); break;
                case 0x81: StmtCall(offset, "faceActor", 2); break;
                case 0x82: StmtCall(offset, "animateActor", 2); break;
                case 0x83: StmtCall(offset, "doSentence", 4); break;
                case 0x84: StmtCall(offset, "pickupObject", 2); break;
                case 0x85: StmtCall(offset, "loadRoomWithEgo", 4); break;
                case 0x87: Push("getRandomNumber(" + Pop() + ")"); break;
                case 0x88: { string hi = Pop(); string lo = Pop(); Push("getRandomNumberRange(" + lo + ", " + hi + ")"); break; }
                case 0x8A: Push("getActorMoving(" + Pop() + ")"); break;
                case 0x8B: Push("isScriptRunning(" + Pop() + ")"); break;
                case 0x8C: Push("getActorRoom(" + Pop() + ")"); break;
                case 0x8D: Push("getObjectX(" + Pop() + ")"); break;
                case 0x8E: Push("getObjectY(" + Pop() + ")"); break;
                case 0x8F: Push("getObjectOldDir(" + Pop() + ")"); break;
                case 0x90: Push("getActorWalkBox(" + Pop() + ")"); break;
                case 0x91: Push("getActorCostume(" + Pop() + ")"); break;
                case 0x92: { string b = Pop(); string a = Pop(); Push("findInventory(" + a + ", " + b + ")"); break; }
                case 0x93: Push("getInventoryCount(" + Pop() + ")"); break;
                case 0x94: { string b = Pop(); string a = Pop(); Push("getVerbFromXY(" + a + ", " + b + ")"); break; }
                case 0x95: Emit(offset, "beginOverride();"); break;
                case 0x96: Emit(offset, "endOverride();"); break;
                case 0x97: { string name = ReadString(); StmtCallWithExtra(offset, "setObjectName", 1, name); break; }
                case 0x98: Push("isSoundRunning(" + Pop() + ")"); break;
                case 0x99: StmtCall(offset, "setBoxFlags", 2); break;
                case 0x9A: Emit(offset, "createBoxMatrix();"); break;
                case 0x9F: { string y = Pop(); string x = Pop(); Push("getActorFromXY(" + x + ", " + y + ")"); break; }
                case 0xA0: { string b = Pop(); string a = Pop(); Push("findObject(" + a + ", " + b + ")"); break; }
                case 0xA1: StmtCall(offset, "pseudoRoom", 2); break;
                case 0xA2: Push("getActorElevation(" + Pop() + ")"); break;
                case 0xA3: { string b = Pop(); string a = Pop(); Push("getVerbEntrypoint(" + a + ", " + b + ")"); break; }
                case 0xA6: StmtCall(offset, "drawBox", 5); break;
                case 0xA7: Emit(offset, StripParens(Pop()) + ";"); break;                        // pop
                case 0xA8: Push("getActorWidth(" + Pop() + ")"); break;
                case 0xAA: Push("getActorScaleX(" + Pop() + ")"); break;
                case 0xAB: Push("getActorAnimCounter(" + Pop() + ")"); break;
                case 0xAC: Emit(offset, "soundKludge(" + PopStackList() + ");"); break;
                case 0xAD: { string list = PopStackList(); string val = Pop(); Push("isAnyOf(" + val + ", " + list + ")"); break; }
                case 0xAF: { string b = Pop(); string a = Pop(); Push("isActorInBox(" + a + ", " + b + ")"); break; }
                case 0xB0: StmtCall(offset, "delay", 1); break;
                case 0xB1: StmtCall(offset, "delaySeconds", 1); break;
                case 0xB2: StmtCall(offset, "delayMinutes", 1); break;
                case 0xB3: Emit(offset, "stopSentence();"); break;
                case 0xBA: { string s = ReadString(); StmtCallWithExtra(offset, "talkActor", 1, s); break; }
                case 0xBB: Emit(offset, "talkEgo(" + ReadString() + ");"); break;
                case 0xBD: Emit(offset, "dummy();"); break;
                case 0xBE: { string a = PopStackList(); string s = Pop(); Emit(offset, "startObjectQuick(" + s + ", " + a + ");"); break; }
                case 0xBF: { string a = PopStackList(); string s = Pop(); Emit(offset, "startScriptQuick2(" + s + ", " + a + ");"); break; }
                case 0xC4: Push("abs(" + Pop() + ")"); break;
                case 0xC5: { string b = Pop(); string a = Pop(); Push("getDistObjObj(" + a + ", " + b + ")"); break; }
                case 0xC6: { string c = Pop(); string b = Pop(); string a = Pop(); Push("getDistObjPt(" + a + ", " + b + ", " + c + ")"); break; }
                case 0xC7: { string d = Pop(); string c = Pop(); string b = Pop(); string a = Pop(); Push("getDistPtPt(" + a + ", " + b + ", " + c + ", " + d + ")"); break; }
                case 0xC8: Push("kernelGetFunctions(" + PopStackList() + ")"); break;
                case 0xC9: Emit(offset, "kernelSetFunctions(" + PopStackList() + ");"); break;
                case 0xCA: StmtCall(offset, "delayFrames", 1); break;
                case 0xCB: { string list = PopStackList(); string idx = Pop(); Push("pickOneOf(" + idx + ", " + list + ")"); break; }
                case 0xCC: { string def = Pop(); string list = PopStackList(); string idx = Pop(); Push("pickOneOfDefault(" + idx + ", " + list + ", " + def + ")"); break; }
                case 0xCD: StmtCall(offset, "stampObject", 4); break;
                case 0xD0: Push("getDateTime()"); break;
                case 0xD1: Emit(offset, "stopTalking();"); break;
                case 0xD2: { string b = Pop(); string a = Pop(); Push("getAnimateVariable(" + a + ", " + b + ")"); break; }
                case 0xD4: StmtCall(offset, "shuffle", 2); break;
                case 0xD5: StmtCall(offset, "jumpToScript", 3); break;
                case 0xD6: Binary("&"); break;
                case 0xD7: Binary("|"); break;
                case 0xD8: Push("isRoomScriptRunning(" + Pop() + ")"); break;
                case 0xDD: Push("findAllObjects(" + Pop() + ")"); break;

                // ---- sub-opcode groups ----
                case 0x6B: SubOp(offset, "cursorCommand", CursorCommand); break;
                case 0x9B: SubOp(offset, "resourceRoutines", ResourceRoutines); break;
                case 0x9C: SubOp(offset, "roomOps", RoomOps); break;
                case 0x9D: SubOpMaybeString(offset, "actorOps", 0x58, ActorOps); break;   // SO_ACTOR_NAME -> inline string
                case 0x9E: SubOpMaybeString(offset, "verbOps", 0x7D, VerbOps); break;      // SO_VERB_NAME -> inline string
                case 0xA4: ArrayOps(offset); break;
                case 0xA5: SubOp(offset, "saveRestoreVerbs", SaveRestoreVerbs); break;
                case 0xA9: WaitOp(offset); break;
                case 0xAE: SubOp(offset, "systemOps", SystemOps); break;
                case 0xB4: PrintOp(offset, "printLine"); break;
                case 0xB5: PrintOp(offset, "printText"); break;
                case 0xB6: PrintOp(offset, "printDebug"); break;
                case 0xB7: PrintOp(offset, "printSystem"); break;
                case 0xB8: PrintOp(offset, "printActor"); break;
                case 0xB9: PrintOp(offset, "printEgo"); break;
                case 0xBC: DimArray(offset, "dimArray"); break;
                case 0xC0: DimArray(offset, "dim2dimArray"); break;

                default:
                    _unknown.Add(op);
                    Emit(offset, "; <unknown opcode 0x" + op.ToString("X2") + " - disassembly stopped>");
                    _stopped = true;
                    break;
            }
        }

        private void StmtCallWithExtra(int offset, string name, int argc, string extra)
        {
            string args = Args(argc);
            if (args.Length > 0) args += ", ";
            Emit(offset, name + "(" + args + extra + ");");
        }

        // -------------------------------------------------------------------------
        // Sub-opcode name tables (ScummVM SO_* constants). Used only to label the
        // disassembly; the byte decoding never depends on them. Unknown sub-codes fall
        // back to "op_0xNN".
        // -------------------------------------------------------------------------

        private static string SubName(Dictionary<int, string> table, byte sub)
        {
            string name;
            if (table != null && table.TryGetValue(sub, out name)) return name;
            return "op_0x" + sub.ToString("X2");
        }

        private static readonly Dictionary<int, string> CursorCommand = new Dictionary<int, string>
        {
            {0x90,"cursorOn"}, {0x91,"cursorOff"}, {0x92,"userPutOn"}, {0x93,"userPutOff"},
            {0x94,"softCursorOn"}, {0x95,"softCursorOff"}, {0x96,"softUserPutOn"}, {0x97,"softUserPutOff"},
            {0x99,"setCursorImg"}, {0x9A,"setCursorHotspot"}, {0x9C,"initCharset"}, {0x9D,"charsetColors"},
            {0xD6,"cursorTransparent"}
        };

        private static readonly Dictionary<int, string> ResourceRoutines = new Dictionary<int, string>
        {
            {0x64,"loadScript"}, {0x65,"loadSound"}, {0x66,"loadCostume"}, {0x67,"loadRoom"},
            {0x68,"nukeScript"}, {0x69,"nukeSound"}, {0x6A,"nukeCostume"}, {0x6B,"nukeRoom"},
            {0x6C,"lockScript"}, {0x6D,"lockSound"}, {0x6E,"lockCostume"}, {0x6F,"lockRoom"},
            {0x70,"unlockScript"}, {0x71,"unlockSound"}, {0x72,"unlockCostume"}, {0x73,"unlockRoom"},
            {0x74,"clearHeap"}, {0x75,"loadCharset"}, {0x76,"nukeCharset"}, {0x77,"loadFlObject"}
        };

        private static readonly Dictionary<int, string> RoomOps = new Dictionary<int, string>
        {
            {0xAC,"roomScroll"}, {0xAE,"setScreen"}, {0xAF,"setPalColor"}, {0xB0,"shakeOn"},
            {0xB1,"shakeOff"}, {0xB3,"darkenPalette"}, {0xB4,"saveLoadRoom"}, {0xB5,"screenEffect"},
            {0xB6,"darkenPaletteRgb"}, {0xB7,"setupShadowPalette"}, {0xBA,"palManipulate"},
            {0xBB,"colorCycleDelay"}, {0xD5,"setPalette"}, {0xDC,"copyPalColor"}, {0xEC,"setRoomPalette"}
        };

        private static readonly Dictionary<int, string> ActorOps = new Dictionary<int, string>
        {
            {0xC5,"setCurActor"}, {0x4C,"setCostume"}, {0x4D,"setWalkSpeed"}, {0x4E,"setSound"},
            {0x4F,"setWalkFrame"}, {0x50,"setTalkFrame"}, {0x51,"setStandFrame"}, {0x52,"setAnimation"},
            {0x53,"init"}, {0x54,"setElevation"}, {0x55,"setDefaultAnim"}, {0x56,"setPalette"},
            {0x57,"setTalkColor"}, {0x58,"setName"}, {0x59,"setInitFrame"}, {0x5B,"setWidth"},
            {0x5C,"setScale"}, {0x5D,"setNeverZClip"}, {0x5E,"setAlwaysZClip"}, {0x5F,"setIgnoreBoxes"},
            {0x60,"setFollowBoxes"}, {0x61,"setAnimSpeed"}, {0x62,"setShadowMode"}, {0x63,"setTalkPos"},
            {0x9C,"setCharset"}, {0xC6,"setAnimVar"}, {0xD7,"setIgnoreTurnsOn"}, {0xD8,"setIgnoreTurnsOff"},
            {0xD9,"initLittle"}
        };

        private static readonly Dictionary<int, string> VerbOps = new Dictionary<int, string>
        {
            {0xC4,"setCurVerb"}, {0x7C,"loadImage"}, {0x7D,"loadString"}, {0x7E,"setColor"},
            {0x7F,"setHiColor"}, {0x80,"setXY"}, {0x81,"setOn"}, {0x82,"setOff"}, {0x83,"delete"},
            {0x84,"new"}, {0x85,"setDimColor"}, {0x86,"setDim"}, {0x87,"setKey"}, {0x88,"setCenter"},
            {0x89,"setToString"}, {0x8B,"setToObject"}, {0x8C,"setBackColor"}, {0xFF,"redraw"}
        };

        private static readonly Dictionary<int, string> WaitOps = new Dictionary<int, string>
        {
            {0xA8,"waitForActor"}, {0xA9,"waitForMessage"}, {0xAA,"waitForCamera"}, {0xAB,"waitForSentence"}
        };

        private static readonly Dictionary<int, string> SaveRestoreVerbs = new Dictionary<int, string>
        {
            {0x8D,"saveVerbs"}, {0x8E,"restoreVerbs"}, {0x8F,"deleteVerbs"}
        };

        private static readonly Dictionary<int, string> SystemOps = new Dictionary<int, string>
        {
            {0x9E,"restartGame"}, {0x9F,"pauseGame"}, {0xA0,"quitGame"}
        };

        private static readonly Dictionary<int, string> ArrayOpsNames = new Dictionary<int, string>
        {
            {0xCD,"assignString"}, {0xD0,"assignIntList"}, {0xD4,"assign2DimList"}
        };

        private static readonly Dictionary<int, string> DimNames = new Dictionary<int, string>
        {
            {0xC7,"int"}, {0xC8,"bit"}, {0xC9,"nibble"}, {0xCA,"byte"}, {0xCB,"string"}, {0xCC,"undim"}
        };

        private static readonly Dictionary<int, string> PrintOps = new Dictionary<int, string>
        {
            {0x41,"at"}, {0x42,"color"}, {0x43,"clipped"}, {0x45,"center"}, {0x47,"left"},
            {0x48,"overhead"}, {0x4A,"mumble"}, {0x4B,"text"}, {0xFE,"begin"}, {0xFF,"end"}
        };

        // Generic sub-opcode group: read the sub-op byte and emit "group.name(args);", consuming
        // the stack as a free-form argument list (exact per-sub-op arity is not modelled).
        private void SubOp(int offset, string group, Dictionary<int, string> table)
        {
            byte sub = ReadByte();
            Emit(offset, group + "." + SubName(table, sub) + "(" + DrainStack() + ");");
        }

        // wait: SO_WAIT_FOR_ACTOR (0xA8) carries an inline jump offset (the script loops back to
        // it while the actor is still moving).
        private void WaitOp(int offset)
        {
            byte sub = ReadByte();
            string name = SubName(WaitOps, sub);
            if (sub == 0xA8)
            {
                string actor = DrainStack();
                string label = Jump(offset);
                Emit(offset, "wait." + name + "(" + actor + ") retry " + label + ";");
            }
            else
            {
                Emit(offset, "wait." + name + "(" + DrainStack() + ");");
            }
        }

        // Sub-opcode group where one sub-op (the "name" op) carries an inline string.
        private void SubOpMaybeString(int offset, string group, byte stringSub, Dictionary<int, string> table)
        {
            byte sub = ReadByte();
            string name = SubName(table, sub);
            if (sub == stringSub)
            {
                Emit(offset, group + "." + name + "(" + ReadString() + ");");
            }
            else
            {
                Emit(offset, group + "." + name + "(" + DrainStack() + ");");
            }
        }

        private void PrintOp(int offset, string group)
        {
            byte sub = ReadByte();
            if (sub == 0x4B) // textstring -> inline message
            {
                Emit(offset, group + ".text(" + ReadString() + ");");
            }
            else
            {
                Emit(offset, group + "." + SubName(PrintOps, sub) + "(" + DrainStack() + ");");
            }
        }

        private void ArrayOps(int offset)
        {
            byte sub = ReadByte();
            int array = ReadWord();
            string arrayName = Var(array);
            if (sub == 0xCD) // assignString -> inline string
            {
                Emit(offset, arrayName + " = " + ReadString() + ";");
            }
            else
            {
                Emit(offset, "arrayOps." + SubName(ArrayOpsNames, sub) + "(" + arrayName + ", " + DrainStack() + ");");
            }
        }

        private void DimArray(int offset, string group)
        {
            byte sub = ReadByte();
            int array = ReadWord();
            Emit(offset, group + "." + SubName(DimNames, sub) + "(" + Var(array) + DrainStackPrefixed() + ");");
        }

        // Consumes whatever is currently on the virtual stack as a comma-separated list.
        private string DrainStack()
        {
            if (_stack.Count == 0) return "";
            var parts = new List<string>(_stack);
            _stack.Clear();
            return string.Join(", ", parts.ToArray());
        }

        private string DrainStackPrefixed()
        {
            string s = DrainStack();
            return s.Length > 0 ? ", " + s : "";
        }
    }
}
