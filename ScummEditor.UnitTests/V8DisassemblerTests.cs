using System.Collections.Generic;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;
using Xunit.Abstractions;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// SCUMM v8 (The Curse of Monkey Island) disassembler: decodes the real global scripts (SCRP) of the
    /// game through <see cref="ScummV8Disassembler"/> and asserts they decode to the end (no unknown
    /// opcode, no desync). v8 is a stack VM like v6/v7 but with a fully remapped opcode table and 4-byte
    /// inline operands, so a wrong opcode/operand width would stop the decode early - the decode-to-end
    /// rate is the gate. SCRP blocks are already typed as ScriptBlock by the v8 container walk.
    /// </summary>
    public class V8DisassemblerTests
    {
        private readonly ITestOutputHelper _output;

        public V8DisassemblerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public void GlobalScriptsDecodeToEnd()
        {
            ScummGameData game = GameLibrary.Load(GameLibrary.CurseOfMonkeyIsland);
            Skip.If(game == null, "COMI (v8) not present");

            List<ScriptBlock> scripts = GameLibrary.AllBlocks(game)
                .OfType<ScriptBlock>()
                .Where(s => s.BlockType == "SCRP")
                .ToList();

            Assert.True(scripts.Count > 0, "no SCRP global scripts found in COMI");

            int decoded = 0;
            var failures = new List<string>();
            foreach (ScriptBlock script in scripts)
            {
                ScummV6Disassembler.Result result = script.Disassemble();
                if (result.DecodedToEnd)
                {
                    decoded++;
                }
                else if (failures.Count < 10)
                {
                    string unknown = result.UnknownOpcodes != null && result.UnknownOpcodes.Count > 0
                        ? "0x" + result.UnknownOpcodes[0].ToString("X2")
                        : "(desync, no unknown opcode)";
                    failures.Add(string.Format("SCRP @0x{0:X} len {1}: stopped after {2} bytes, first unknown {3}",
                        script.BlockOffSet, script.RawContent.Length, result.BytesDecoded, unknown));
                }
            }

            _output.WriteLine("COMI SCRP decode-to-end: {0}/{1}", decoded, scripts.Count);
            foreach (string f in failures) _output.WriteLine(f);

            // The disassembler must decode every global script to the end. A single early stop means a
            // wrong opcode or operand width - exactly what this gate exists to catch.
            Assert.Equal(scripts.Count, decoded);
        }

        [Fact]
        public void HandlesArrayIncDecStopObjectScriptAndStartMusic()
        {
            // Regression for the opcodes the adversarial review found missing (0x72 wordArrayInc,
            // 0x73 wordArrayDec, 0x80 stopObjectScript, 0xB0 startMusic): each must decode (no early stop),
            // or any script using them would be excluded from text editing (DecodedToEnd gates the rebuild).
            byte[] local0 = { 0x00, 0x00, 0x00, 0x40 }; // v8 var Local[0] (bit 30 set)
            var code = new List<byte>();
            code.AddRange(new byte[] { 0x01, 0x05, 0x00, 0x00, 0x00 });   // pushWord 5 (array index)
            code.Add(0x72); code.AddRange(local0);                        // wordArrayInc Local[0]
            code.AddRange(new byte[] { 0x01, 0x05, 0x00, 0x00, 0x00 });   // pushWord 5
            code.Add(0x73); code.AddRange(local0);                        // wordArrayDec Local[0]
            code.AddRange(new byte[] { 0x01, 0x07, 0x00, 0x00, 0x00 });   // pushWord 7
            code.Add(0x80);                                               // stopObjectScript
            code.AddRange(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00 });   // pushWord 3
            code.Add(0xB0);                                               // startMusic

            ScummV6Disassembler.Result result = ScummV8Disassembler.Disassemble(code.ToArray(), 0);

            Assert.True(result.DecodedToEnd, "synthetic v8 bytecode did not decode to the end");
            Assert.Empty(result.UnknownOpcodes);
            Assert.Contains("++", result.Listing);
            Assert.Contains("--", result.Listing);
            Assert.Contains("stopObjectScript", result.Listing);
            Assert.Contains("startMusic", result.Listing);
        }
    }
}
