using System.Collections.Generic;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Disassembles an object's verb scripts (OBCD/OC) for display: builds the verb-entry labels,
    /// slices the verb bytecode and runs the disassembler for the game's SCUMM version (v4/v5 are the
    /// parameter-bit language, v6+ is stack-based - the same routing GameTextManager.Disassemble
    /// uses). Pure engine - the GUI only formats the returned rows + listing.
    /// </summary>
    public static class ObjectVerbDisassembler
    {
        /// <summary>One verb-table entry's display info: its label and where it lands in the code slice.</summary>
        public struct VerbLabel
        {
            public string Name;
            public int SliceOffset;
            public bool InRange;
        }

        public class Result
        {
            public List<VerbLabel> Verbs = new List<VerbLabel>();
            /// <summary>The disassembled verb bytecode, or null when the object has no verb scripts.</summary>
            public ScummV6Disassembler.Result Code;
        }

        public static Result Disassemble(ObjectCode obcd)
        {
            var result = new Result();

            // Verb-entry labels (and the label map fed to the disassembler). VerbEntryBase makes the
            // offset math version-correct: VerbBlockOffset for v5/v6, -HeaderLength for v4.
            var labels = new Dictionary<int, string>();
            foreach (VerbEntry entry in obcd.VerbEntries)
            {
                int sliceOffset = obcd.VerbEntryBase + entry.Offset - obcd.VerbCodeOffset;
                string name = entry.Id == 0xFF ? "verb_any" : "verb_0x" + entry.Id.ToString("X2");
                bool inRange = sliceOffset >= 0 && sliceOffset < obcd.VerbCodeLength;

                result.Verbs.Add(new VerbLabel { Name = name, SliceOffset = sliceOffset, InRange = inRange });
                if (inRange && !labels.ContainsKey(sliceOffset)) labels.Add(sliceOffset, name);
            }

            if (obcd.VerbCodeOffset < 0 || obcd.VerbCodeLength <= 0)
            {
                return result; // no verb scripts
            }

            var slice = new byte[obcd.VerbCodeLength];
            System.Array.Copy(obcd.RawContent, obcd.VerbCodeOffset, slice, 0, obcd.VerbCodeLength);

            int scummVersion = obcd.GameInfo != null ? obcd.GameInfo.ScummVersion : 0;
            if (scummVersion == 4)
            {
                result.Code = ScummV4Disassembler.Disassemble(slice, 0, labels);
            }
            else if (scummVersion == 5)
            {
                result.Code = Scumm5Disassembler.Disassemble(slice, 0, labels);
            }
            else
            {
                result.Code = ScummV6Disassembler.Disassemble(slice, 0, labels);
            }

            return result;
        }
    }
}
