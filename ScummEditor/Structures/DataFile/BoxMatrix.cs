using System.Collections.Generic;
using System.IO;

namespace ScummEditor.Structures.DataFile
{
    /*
    BOXM - Box matrix. Tells, for each box, which box to walk to next in order to
    reach any other box. One line per box, each line terminated by 0xFF:

      line (one per box):
        link (repeated):
          start : 8   first box of a destination range
          end   : 8   last box of the range
          box   : 8   box to step into to reach that range
        0xFF        end of line

    NOTE: this is a read-only decode. The original bytes are kept and written back
    verbatim on save, so rebuilding the game file is always byte-identical.
    */
    public class BoxMatrix : BlockBase
    {
        public BoxMatrix(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "BOXM"; }
        }

        public byte[] RawContent { get; set; }

        public List<MatrixRow> Rows { get; set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - 8));
            ParseForDisplay();
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }

        private void ParseForDisplay()
        {
            Rows = new List<MatrixRow>();

            int p = 0;
            var current = new MatrixRow();
            while (p < RawContent.Length)
            {
                byte first = RawContent[p++];
                if (first == 0xFF)
                {
                    // End of the current line.
                    Rows.Add(current);
                    current = new MatrixRow();
                    continue;
                }

                // A link needs two more bytes; stop if the block is truncated.
                if (p + 1 >= RawContent.Length) break;

                current.Links.Add(new BoxLink
                {
                    Start = first,
                    End = RawContent[p++],
                    Box = RawContent[p++]
                });
            }

            // Keep a trailing non-terminated line if present.
            if (current.Links.Count > 0) Rows.Add(current);
        }
    }

    public class MatrixRow
    {
        public MatrixRow()
        {
            Links = new List<BoxLink>();
        }

        public List<BoxLink> Links { get; set; }
    }

    public class BoxLink
    {
        public byte Start { get; set; }
        public byte End { get; set; }
        public byte Box { get; set; }
    }
}
