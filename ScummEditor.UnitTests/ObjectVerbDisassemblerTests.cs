using System.Collections.Generic;
using System.Linq;
using ScummEditor.Engine.Encoders;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// The engine object-verb disassembler (extracted from ObjectCodeControl in Stage 2b) must
    /// produce a label per verb entry and fully decode every shipping verb script, across the v4
    /// (parameter-bit + v4 opcode deltas), v5 and v6 (stack-based) bytecode languages.
    /// </summary>
    public class ObjectVerbDisassemblerTests
    {
        [SkippableTheory]
        [InlineData(GameLibrary.MonkeyIsland1FloppyVga)]
        [InlineData(GameLibrary.Loom)]
        [InlineData(GameLibrary.MonkeyIsland2Floppy)]
        [InlineData(GameLibrary.DayOfTheTentacleFloppy)]
        public void EveryVerbScriptDecodesToTheEndWithInRangeLabels(string relativePath)
        {
            Skip.If(GameLibrary.Folder(relativePath) == null, "GameData folder not present: " + relativePath);

            ScummGameData game = GameLibrary.Load(relativePath);
            Assert.NotNull(game);

            List<ObjectCode> objectCodes = GameLibrary.AllBlocks(game).OfType<ObjectCode>().ToList();
            Assert.NotEmpty(objectCodes);

            int withVerbCode = 0;
            foreach (ObjectCode oc in objectCodes)
            {
                ObjectVerbDisassembler.Result result = ObjectVerbDisassembler.Disassemble(oc);

                // One label row per verb entry, always.
                Assert.Equal(oc.VerbEntries.Count, result.Verbs.Count);

                if (oc.VerbCodeOffset >= 0 && oc.VerbCodeLength > 0)
                {
                    withVerbCode++;
                    Assert.NotNull(result.Code);
                    Assert.True(result.Code.DecodedToEnd,
                        "verb script did not fully decode for object '" + oc.Name + "' in " + relativePath);

                    foreach (ObjectVerbDisassembler.VerbLabel verb in result.Verbs)
                    {
                        Assert.True(verb.InRange,
                            "verb label '" + verb.Name + "' out of range for object '" + oc.Name + "' in " + relativePath);
                    }
                }
                else
                {
                    Assert.Null(result.Code);
                }
            }

            Assert.True(withVerbCode > 0, "expected at least one object with verb code in " + relativePath);
        }
    }
}
