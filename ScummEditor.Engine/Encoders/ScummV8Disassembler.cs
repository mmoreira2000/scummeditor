using System.Collections.Generic;
using System.Text;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Disassembles SCUMM v8 bytecode (The Curse of Monkey Island) into a readable, C#-like listing.
    /// v8 is the same stack-based VM as v6/v7 but with a COMPLETELY remapped opcode table and, crucially,
    /// 4-byte (little-endian) inline operands everywhere v6 used 2-byte ones: every pushed literal,
    /// variable index, array id and jump offset is 4 bytes, and every inline string escape carries a
    /// 4-byte argument. So this is a fresh table (like the v1/v2 disassembler), not a subclass of
    /// <see cref="ScummV6Disassembler"/> - but it keeps the same descumm-style output and the same
    /// Result/StringRef/JumpRef shape so the whole text pipeline reuses it.
    ///
    /// Best-effort, like the v6/v7 one: control flow is shown with goto/labels and decoding stops cleanly
    /// at the first unknown opcode so the output never desynchronises into garbage. Opcode table mirrors
    /// ScummVM script_v8.cpp + scummvm-tools descumm6.cpp next_line_V8.
    /// </summary>
    public class ScummV8Disassembler
    {
        // Returns the SAME Result/StringRef/JumpRef DTOs as the v6/v7 disassembler so the whole text
        // pipeline (GameTextManager, the script/object viewers) consumes either interchangeably.
        private byte[] _code;
        private int _pos;
        private readonly List<string> _stack = new List<string>();
        private readonly List<Line> _lines = new List<Line>();
        private readonly HashSet<int> _jumpTargets = new HashSet<int>();
        private readonly List<int> _unknown = new List<int>();
        private readonly List<ScummV6Disassembler.StringRef> _strings = new List<ScummV6Disassembler.StringRef>();
        private readonly List<ScummV6Disassembler.JumpRef> _jumps = new List<ScummV6Disassembler.JumpRef>();
        private bool _stopped;
        private IDictionary<int, string> _namedLabels;

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
            var d = new ScummV8Disassembler();
            d._namedLabels = namedLabels;
            return d.Run(code, startOffset);
        }

        private ScummV6Disassembler.Result Run(byte[] code, int startOffset)
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
                catch (System.IndexOutOfRangeException)
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
        // Stack / emit helpers (identical in spirit to the v6 disassembler)
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

        private void PushCall(string name, int argc)
        {
            Push(name + "(" + Args(argc) + ")");
        }

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

        // A variable-length "stack list": the element count is pushed last, the values before it.
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

        private void StmtCallWithExtra(int offset, string name, int argc, string extra)
        {
            string args = Args(argc);
            if (args.Length > 0) args += ", ";
            Emit(offset, name + "(" + args + extra + ");");
        }

        // -------------------------------------------------------------------------
        // Reading helpers - the v8 deltas: every inline integer/var/jump is 4 bytes.
        // -------------------------------------------------------------------------

        private byte ReadByte() { return _code[_pos++]; }

        /// <summary>Reads a 4-byte little-endian signed integer (v8 inline operands are 32-bit).</summary>
        private int ReadWord()
        {
            int v = _code[_pos] | (_code[_pos + 1] << 8) | (_code[_pos + 2] << 16) | (_code[_pos + 3] << 24);
            _pos += 4;
            return v;
        }

        private int ReadSignedWord()
        {
            return ReadWord(); // already a signed 32-bit read
        }

        private uint ReadVarRaw()
        {
            uint v = (uint)(_code[_pos] | (_code[_pos + 1] << 8) | (_code[_pos + 2] << 16) | (_code[_pos + 3] << 24));
            _pos += 4;
            return v;
        }

        // v8 variable encoding (32-bit): bit 31 = bit variable, bit 30 = local variable, else global.
        private static string Var(uint var)
        {
            if ((var & 0x80000000u) != 0) return "Bit[" + (var & 0x0FFFFFFFu) + "]";
            if ((var & 0x40000000u) != 0) return "Local[" + (var & 0x0FFFFFFFu) + "]";
            return "Global[" + var + "]";
        }

        private string ReadVarName()
        {
            return Var(ReadVarRaw());
        }

        private string Jump(int offset)
        {
            int operandOffset = _pos;
            int rel = ReadSignedWord(); // v8 jump offset is a 4-byte signed word
            int target = _pos + rel;
            _jumpTargets.Add(target);
            _jumps.Add(new ScummV6Disassembler.JumpRef { OperandOffset = operandOffset, Target = target });
            return "L" + target.ToString("X4");
        }

        private static readonly GameTextCodec ListingCodec = GameTextCodec.Default();

        /// <summary>
        /// Reads an inline SCUMM v8 message string (returns a quoted literal). The escape rule matches
        /// ScummVM convertMessageToString for v8: a 0xFF lead is followed by a code byte; codes 1/2/3/8
        /// carry NO argument, every other code carries a 4-byte argument (v6/v7 used 2-byte arguments).
        /// </summary>
        private string ReadString(string kind = "msg")
        {
            int start = _pos;
            bool terminated = false;
            while (_pos < _code.Length)
            {
                byte b = ReadByte();
                if (b == 0) { terminated = true; break; }
                if (b == 0xFF)
                {
                    byte code = _pos < _code.Length ? ReadByte() : (byte)0;
                    if (code != 1 && code != 2 && code != 3 && code != 8)
                    {
                        _pos += 4; // v8: 4-byte escape argument
                        if (_pos > _code.Length) _pos = _code.Length;
                    }
                }
            }

            int contentLength = _pos - start - (terminated ? 1 : 0);
            if (contentLength < 0) contentLength = 0;
            _strings.Add(new ScummV6Disassembler.StringRef { Offset = start, Length = _pos - start, Terminated = terminated, Kind = kind });
            return "\"" + ListingCodec.Decode(_code, start, contentLength).Replace("\"", "\\\"") + "\"";
        }

        // -------------------------------------------------------------------------
        // Rendering (identical to the v6 disassembler)
        // -------------------------------------------------------------------------

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
        // Opcode table (ScummVM script_v8.cpp setupOpcodes / descumm6.cpp next_line_V8)
        // -------------------------------------------------------------------------

        // v8 binary operators 0x08..0x16, in order: == != > < <= >= + - * / && || & | %
        private static readonly string[] BinaryOps =
        {
            "==", "!=", ">", "<", "<=", ">=", "+", "-", "*", "/", "&&", "||", "&", "|", "%"
        };

        private void Decode(byte op, int offset)
        {
            // ---- binary arithmetic / comparison (0x08..0x16) ----
            if (op >= 0x08 && op <= 0x16)
            {
                Binary(BinaryOps[op - 0x08]);
                return;
            }

            switch (op)
            {
                // ---- stack / values ----
                case 0x01: Push(ReadSignedWord().ToString()); break;                          // pushWord
                case 0x02: Push(ReadVarName()); break;                                         // pushWordVar
                case 0x03: { string a = ReadVarName(); Push(a + "[" + Pop() + "]"); break; }   // wordArrayRead
                case 0x04: { string idx = Pop(); string a = ReadVarName(); string b = Pop(); Push(a + "[" + b + "][" + idx + "]"); break; } // wordArrayIndexedRead
                case 0x05: { string v = Pop(); Push(v); Push(v); break; }                      // dup
                case 0x06: Emit(offset, StripParens(Pop()) + ";"); break;                      // pop (discard)
                case 0x07: Push("!" + Pop()); break;                                           // not

                // ---- control flow ----
                case 0x64: { string cond = Pop(); Emit(offset, "if (" + StripParens(cond) + ") goto " + Jump(offset) + ";"); break; }   // if (jumpTrue)
                case 0x65: { string cond = Pop(); Emit(offset, "if (" + NegateCondition(cond) + ") goto " + Jump(offset) + ";"); break; } // ifNot (jumpFalse)
                case 0x66: Emit(offset, "goto " + Jump(offset) + ";"); break;                  // jump
                case 0x67: Emit(offset, "breakHere();"); break;
                case 0x68: StmtCall(offset, "delayFrames", 1); break;
                case 0x69: WaitOp(offset); break;
                case 0x6A: StmtCall(offset, "delay", 1); break;
                case 0x6B: StmtCall(offset, "delaySeconds", 1); break;
                case 0x6C: StmtCall(offset, "delayMinutes", 1); break;

                // ---- variable / array writes ----
                case 0x6D: { string v = ReadVarName(); Emit(offset, v + " = " + StripParens(Pop()) + ";"); break; } // writeWordVar
                case 0x6E: Emit(offset, ReadVarName() + "++;"); break;                         // wordVarInc
                case 0x6F: Emit(offset, ReadVarName() + "--;"); break;                         // wordVarDec
                case 0x70: DimArray(offset, "dimArray"); break;
                case 0x71: { string val = StripParens(Pop()); string a = ReadVarName(); string idx = Pop(); Emit(offset, a + "[" + idx + "] = " + val + ";"); break; } // wordArrayWrite
                case 0x74: DimArray(offset, "dim2dimArray"); break;
                case 0x75: { string val = StripParens(Pop()); string idx = Pop(); string a = ReadVarName(); string b = Pop(); Emit(offset, a + "[" + b + "][" + idx + "] = " + val + ";"); break; } // wordArrayIndexedWrite
                case 0x76: ArrayOps(offset); break;

                // ---- scripts / objects ----
                case 0x79: { string a = PopStackList(); string s = Pop(); string f = Pop(); Emit(offset, "startScript(" + s + ", " + f + ", " + a + ");"); break; }
                case 0x7A: { string a = PopStackList(); string s = Pop(); Emit(offset, "startScriptQuick(" + s + ", " + a + ");"); break; }
                case 0x7B: Emit(offset, "stopObjectCode();"); break;
                case 0x7C: StmtCall(offset, "stopScript", 1); break;
                case 0x7D: { string a = PopStackList(); string s = Pop(); string f = Pop(); Emit(offset, "jumpToScript(" + s + ", " + f + ", " + a + ");"); break; }
                case 0x7E: StmtCall(offset, "return", 1); break;
                case 0x7F: { string a = PopStackList(); string e = Pop(); string en = Pop(); string s = Pop(); Emit(offset, "startObject(" + s + ", " + en + ", " + e + ", " + a + ");"); break; }
                case 0x81: { string a = PopStackList(); Emit(offset, "beginCutscene(" + a + ");"); break; }
                case 0x82: Emit(offset, "endCutscene();"); break;
                case 0x83: StmtCall(offset, "freezeUnfreeze", 1); break;
                case 0x84: Emit(offset, "beginOverride();"); break;
                case 0x85: Emit(offset, "endOverride();"); break;
                case 0x86: Emit(offset, "stopSentence();"); break;
                case 0x87: StmtCall(offset, "debug", 1); break;
                case 0x89: { string a = PopStackList(); string o = Pop(); Emit(offset, "setClass(" + o + ", " + a + ");"); break; }
                case 0x8A: StmtCall(offset, "setState", 2); break;
                case 0x8B: StmtCall(offset, "setOwner", 2); break;
                case 0x8C: StmtCall(offset, "panCameraTo", 2); break;
                case 0x8D: StmtCall(offset, "actorFollowCamera", 1); break;
                case 0x8E: StmtCall(offset, "setCameraAt", 2); break;

                // ---- print / talk (sub-opcode groups; the 0xD1 sub-op carries an inline string) ----
                case 0x8F: PrintOp(offset, "printActor"); break;
                case 0x90: PrintOp(offset, "printEgo"); break;
                case 0x91: { string actor = Pop(); string s = ReadString("talk"); Emit(offset, "talkActor(" + actor + ", " + s + ");"); break; }
                case 0x92: Emit(offset, "talkEgo(" + ReadString("talk") + ");"); break;
                case 0x93: PrintOp(offset, "printLine"); break;
                case 0x94: PrintOp(offset, "printCursor"); break;
                case 0x95: PrintOp(offset, "printDebug"); break;
                case 0x96: PrintOp(offset, "printSystem"); break;
                case 0x97: PrintOp(offset, "blastText"); break;
                case 0x98: StmtCall(offset, "drawObject", 4); break;

                // ---- cursor / rooms / actors ----
                case 0x9C: SubOp(offset, "cursorCommand", CursorCommand); break;
                case 0x9D: StmtCall(offset, "loadRoom", 1); break;
                case 0x9E: StmtCall(offset, "loadRoomWithEgo", 3); break;
                case 0x9F: StmtCall(offset, "walkActorToObj", 3); break;
                case 0xA0: StmtCall(offset, "walkActorTo", 3); break;
                case 0xA1: StmtCall(offset, "putActorAtXY", 4); break;
                case 0xA2: StmtCall(offset, "putActorAtObject", 2); break;
                case 0xA3: StmtCall(offset, "faceActor", 2); break;
                case 0xA4: StmtCall(offset, "animateActor", 2); break;
                case 0xA5: StmtCall(offset, "doSentence", 3); break;
                case 0xA6: StmtCall(offset, "pickupObject", 1); break;
                case 0xA7: { string list = PopStackList(); string box = Pop(); Emit(offset, "setBoxFlags(" + box + ", " + list + ");"); break; }
                case 0xA8: Emit(offset, "createBoxMatrix();"); break;
                case 0xAA: SubOp(offset, "resourceRoutines", ResourceRoutines); break;
                case 0xAB: SubOp(offset, "roomOps", RoomOps); break;
                case 0xAC: SubOpMaybeString(offset, "actorOps", 0x71, ActorOps, "actorName"); break; // setActorName -> inline string
                case 0xAD: SubOp(offset, "cameraOps", CameraOps); break;
                case 0xAE: SubOpMaybeString(offset, "verbOps", 0x99, VerbOps, "verbName"); break;    // verbLoadString -> inline string
                case 0xAF: StmtCall(offset, "startSound", 1); break;
                case 0xB1: StmtCall(offset, "stopSound", 1); break;
                case 0xB2: Emit(offset, "soundKludge(" + PopStackList() + ");"); break;
                case 0xB3: SubOp(offset, "systemOps", SystemOps); break;
                case 0xB4: SubOp(offset, "saveRestoreVerbs", SaveRestoreVerbs); break;
                case 0xB5: { string o = Pop(); string s = ReadString("objectName"); Emit(offset, "setObjectName(" + o + ", " + s + ");"); break; }
                case 0xB6: Emit(offset, "getDateTime();"); break;
                case 0xB7: StmtCall(offset, "drawBox", 5); break;
                case 0xB9: Emit(offset, "startVideo(" + ReadString("file") + ");"); break;
                case 0xBA: Emit(offset, "kernelSetFunctions(" + PopStackList() + ");"); break;

                // ---- value-producing (results pushed) ----
                case 0xC8: { string a = PopStackList(); string s = Pop(); Push("startScriptQuick2(" + s + ", " + a + ")"); break; }
                case 0xC9: { string a = PopStackList(); string s = Pop(); string e = Pop(); string en = Pop(); Emit(offset, "startObjectQuick(" + s + ", " + en + ", " + e + ", " + a + ");"); break; }
                case 0xCA: { string list = PopStackList(); string idx = Pop(); Push("pickOneOf(" + idx + ", " + list + ")"); break; }
                case 0xCB: { string def = Pop(); string list = PopStackList(); string idx = Pop(); Push("pickOneOfDefault(" + idx + ", " + list + ", " + def + ")"); break; }
                case 0xCD: { string list = PopStackList(); string v = Pop(); Push("isAnyOf(" + v + ", " + list + ")"); break; }
                case 0xCE: PushCall("getRandomNumber", 1); break;
                case 0xCF: PushCall("getRandomNumberRange", 2); break;
                case 0xD0: { string list = PopStackList(); string o = Pop(); Push("classOfIs(" + o + ", " + list + ")"); break; }
                case 0xD1: PushCall("getState", 1); break;
                case 0xD2: PushCall("getOwner", 1); break;
                case 0xD3: PushCall("isScriptRunning", 1); break;
                case 0xD5: PushCall("isSoundRunning", 1); break;
                case 0xD6: PushCall("abs", 1); break;
                case 0xD8: Push("kernelGetFunctions(" + PopStackList() + ")"); break;
                case 0xD9: PushCall("isActorInBox", 2); break;
                case 0xDA: PushCall("getVerbEntrypoint", 2); break;
                case 0xDB: PushCall("getActorFromXY", 2); break;
                case 0xDC: PushCall("findObject", 2); break;
                case 0xDD: PushCall("getVerbFromXY", 2); break;
                case 0xDF: PushCall("findInventory", 2); break;
                case 0xE0: PushCall("getInventoryCount", 1); break;
                case 0xE1: PushCall("getAnimateVariable", 2); break;
                case 0xE2: PushCall("getActorRoom", 1); break;
                case 0xE3: PushCall("getActorWalkBox", 1); break;
                case 0xE4: PushCall("getActorMoving", 1); break;
                case 0xE5: PushCall("getActorCostume", 1); break;
                case 0xE6: PushCall("getActorScaleX", 1); break;
                case 0xE7: PushCall("getActorLayer", 1); break;
                case 0xE8: PushCall("getActorElevation", 1); break;
                case 0xE9: PushCall("getActorWidth", 1); break;
                case 0xEA: PushCall("getObjectDir", 1); break;
                case 0xEB: PushCall("getObjectX", 1); break;
                case 0xEC: PushCall("getObjectY", 1); break;
                case 0xED: PushCall("getActorChore", 1); break;
                case 0xEE: PushCall("getDistObjObj", 2); break;
                case 0xEF: PushCall("getDistPtPt", 4); break;
                case 0xF0: PushCall("getObjectImageX", 1); break;
                case 0xF1: PushCall("getObjectImageY", 1); break;
                case 0xF2: PushCall("getObjectImageWidth", 1); break;
                case 0xF3: PushCall("getObjectImageHeight", 1); break;
                case 0xF4: PushCall("getVerbX", 1); break;
                case 0xF5: PushCall("getVerbY", 1); break;
                case 0xF6: { string p = Pop(); string s = ReadString("stringWidth"); Push("stringWidth(" + p + ", " + s + ")"); break; }
                case 0xF7: PushCall("getActorZPlane", 1); break;

                default:
                    _unknown.Add(op);
                    Emit(offset, "; <unknown opcode 0x" + op.ToString("X2") + " - disassembly stopped>");
                    _stopped = true;
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // Sub-opcode groups. Only the inline-byte consumers matter for decode-to-end:
        // the sub-op byte (always), an inline string (a few subs) and the inline 4-byte
        // array word (dimArray/arrayOps). Everything else is read from the virtual stack.
        // -------------------------------------------------------------------------

        private void SubOp(int offset, string group, Dictionary<int, string> table)
        {
            byte sub = ReadByte();
            Emit(offset, group + "." + SubName(table, sub) + "(" + DrainStack() + ");");
        }

        private void SubOpMaybeString(int offset, string group, byte stringSub, Dictionary<int, string> table, string stringKind)
        {
            byte sub = ReadByte();
            string name = SubName(table, sub);
            if (sub == stringSub)
            {
                Emit(offset, group + "." + name + "(" + ReadString(stringKind) + ");");
            }
            else
            {
                Emit(offset, group + "." + name + "(" + DrainStack() + ");");
            }
        }

        // wait: SO_WAIT_FOR_ACTOR (0x1E), waitUntilActorDrawn (0x22) and waitUntilActorTurned (0x23) each
        // carry an inline 4-byte jump offset (the script loops back while the actor is busy); the others
        // have no inline operand.
        private void WaitOp(int offset)
        {
            byte sub = ReadByte();
            string name = SubName(WaitOps, sub);
            if (sub == 0x1E || sub == 0x22 || sub == 0x23)
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

        private void PrintOp(int offset, string group)
        {
            byte sub = ReadByte();
            if (sub == 0xD1) // msg -> inline string
            {
                Emit(offset, group + ".msg(" + ReadString(group) + ");");
            }
            else
            {
                Emit(offset, group + "." + SubName(PrintOps, sub) + "(" + DrainStack() + ");");
            }
        }

        // dimArray / dim2dimArray: sub-op byte then a single inline 4-byte array variable.
        private void DimArray(int offset, string group)
        {
            byte sub = ReadByte();
            string array = ReadVarName();
            Emit(offset, group + "." + SubName(DimNames, sub) + "(" + array + DrainStackPrefixed() + ");");
        }

        // arrayOps: sub-op byte, inline 4-byte array variable, then either an inline string
        // (assignString, sub 0x14) or stack values (the list/index assignments, 0x15/0x16).
        private void ArrayOps(int offset)
        {
            byte sub = ReadByte();
            string array = ReadVarName();
            if (sub == 0x14) // assignString -> inline string
            {
                Emit(offset, array + " = " + ReadString("array") + ";");
            }
            else
            {
                Emit(offset, "arrayOps." + SubName(ArrayOpsNames, sub) + "(" + array + ", " + DrainStack() + ");");
            }
        }

        private static string SubName(Dictionary<int, string> table, byte sub)
        {
            string name;
            if (table != null && table.TryGetValue(sub, out name)) return name;
            return "op_0x" + sub.ToString("X2");
        }

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

        // Reuse the v6 disassembler's readability helpers (same logic for both engines).
        private static string StripParens(string e) { return ScummV6Disassembler.StripParens(e); }
        private static string NegateCondition(string c) { return ScummV6Disassembler.NegateCondition(c); }

        // -------------------------------------------------------------------------
        // Sub-opcode name tables (ScummVM SO_* values for v8; labels only).
        // -------------------------------------------------------------------------

        private static readonly Dictionary<int, string> WaitOps = new Dictionary<int, string>
        {
            {0x1E,"waitForActor"}, {0x1F,"waitForMessage"}, {0x20,"waitForCamera"}, {0x21,"waitForSentence"},
            {0x22,"waitUntilActorDrawn"}, {0x23,"waitUntilActorTurned"}
        };

        private static readonly Dictionary<int, string> CursorCommand = new Dictionary<int, string>
        {
            {0xDC,"cursorOn"}, {0xDD,"cursorOff"}, {0xDE,"userPutOn"}, {0xDF,"userPutOff"},
            {0xE0,"softCursorOn"}, {0xE1,"softCursorOff"}, {0xE2,"softUserputOn"}, {0xE3,"softUserputOff"},
            {0xE4,"setCursorImg"}, {0xE5,"setCursorHotspot"}, {0xE6,"makeCursorColorTransparent"},
            {0xE7,"initCharset"}, {0xE8,"charsetColors"}, {0xE9,"setCursorPosition"}
        };

        private static readonly Dictionary<int, string> ResourceRoutines = new Dictionary<int, string>
        {
            {0x3C,"loadCharset"}, {0x3D,"loadCostume"}, {0x3E,"loadObject"}, {0x3F,"loadRoom"},
            {0x40,"loadScript"}, {0x41,"loadSound"}, {0x42,"lockCostume"}, {0x43,"lockRoom"},
            {0x44,"lockScript"}, {0x45,"lockSound"}, {0x46,"unlockCostume"}, {0x47,"unlockRoom"},
            {0x48,"unlockScript"}, {0x49,"unlockSound"}, {0x4A,"nukeCostume"}, {0x4B,"nukeRoom"},
            {0x4C,"nukeScript"}, {0x4D,"nukeSound"}
        };

        private static readonly Dictionary<int, string> RoomOps = new Dictionary<int, string>
        {
            {0x52,"setRoomPalette"}, {0x55,"setRoomIntensity"}, {0x57,"fade"}, {0x58,"setRoomRGBIntensity"},
            {0x59,"transformRoom"}, {0x5A,"colorCycleDelay"}, {0x5B,"copyPalette"}, {0x5C,"newPalette"},
            {0x5D,"saveGame"}, {0x5E,"loadGame"}, {0x5F,"setRoomSaturation"}
        };

        private static readonly Dictionary<int, string> ActorOps = new Dictionary<int, string>
        {
            {0x64,"setActorCostume"}, {0x65,"setActorWalkSpeed"}, {0x67,"setActorDefAnim"},
            {0x68,"setActorInitFrame"}, {0x69,"setActorTalkFrame"}, {0x6A,"setActorWalkFrame"},
            {0x6B,"setActorStandFrame"}, {0x6C,"setActorAnimSpeed"}, {0x6D,"setActorDefault"},
            {0x6E,"setActorElevation"}, {0x6F,"setActorPalette"}, {0x70,"setActorTalkColor"},
            {0x71,"setActorName"}, {0x72,"setActorWidth"}, {0x73,"setActorScale"}, {0x74,"setActorNeverZClip"},
            {0x75,"setActorAlwaysZClip"}, {0x76,"setActorIgnoreBoxes"}, {0x77,"setActorFollowBoxes"},
            {0x78,"setShadowMode"}, {0x79,"setActorTalkPos"}, {0x7A,"setCurActor"}, {0x7B,"setActorAnimVar"},
            {0x7C,"setActorIgnoreTurnsOn"}, {0x7D,"setActorIgnoreTurnsOff"}, {0x7E,"newActor"},
            {0x7F,"setActorLayer"}, {0x80,"setActorStanding"}, {0x81,"setActorDirection"},
            {0x82,"actorTurnToDirection"}, {0x83,"setActorWalkScript"}, {0x84,"setTalkScript"},
            {0x85,"freezeActor"}, {0x86,"unfreezeActor"}, {0x87,"setActorVolume"}, {0x88,"setActorFrequency"},
            {0x89,"setActorPan"}
        };

        private static readonly Dictionary<int, string> CameraOps = new Dictionary<int, string>
        {
            {0x32,"freezeCamera"}, {0x33,"unfreezeCamera"}
        };

        private static readonly Dictionary<int, string> VerbOps = new Dictionary<int, string>
        {
            {0x96,"verbInit"}, {0x97,"verbNew"}, {0x98,"verbDelete"}, {0x99,"verbLoadString"},
            {0x9A,"verbSetXY"}, {0x9B,"verbOn"}, {0x9C,"verbOff"}, {0x9D,"verbSetColor"},
            {0x9E,"verbSetHiColor"}, {0xA0,"verbSetDimColor"}, {0xA1,"verbSetDim"}, {0xA2,"verbSetKey"},
            {0xA3,"verbLoadImg"}, {0xA4,"verbSetToString"}, {0xA5,"verbSetCenter"}, {0xA6,"verbSetCharset"},
            {0xA7,"verbSetLineSpacing"}
        };

        private static readonly Dictionary<int, string> SystemOps = new Dictionary<int, string>
        {
            {0x28,"restart"}, {0x29,"quit"}
        };

        private static readonly Dictionary<int, string> SaveRestoreVerbs = new Dictionary<int, string>
        {
            {0xB4,"saveVerbs"}, {0xB5,"restoreVerbs"}, {0xB6,"deleteVerbs"}
        };

        private static readonly Dictionary<int, string> ArrayOpsNames = new Dictionary<int, string>
        {
            {0x14,"assignString"}, {0x15,"assignIntList"}, {0x16,"assign2DimList"}
        };

        private static readonly Dictionary<int, string> DimNames = new Dictionary<int, string>
        {
            {0x0A,"int"}, {0x0B,"string"}, {0xCA,"undim"}
        };

        private static readonly Dictionary<int, string> PrintOps = new Dictionary<int, string>
        {
            {0xC8,"baseop"}, {0xC9,"end"}, {0xCA,"XY"}, {0xCB,"color"}, {0xCC,"center"}, {0xCD,"charset"},
            {0xCE,"left"}, {0xCF,"overhead"}, {0xD0,"mumble"}, {0xD1,"msg"}, {0xD2,"wrap"}
        };
    }
}
