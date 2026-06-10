using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Structures.DataFile
{
    /*
    OBCD - Object code (scripts/metadata for a room object). Contains:

      CDHD - code header
        obj id   : 16le
        x        : 16le  (active area, upper-left corner)
        y        : 16le
        w        : 16le  (active area size; may differ from the image size)
        h        : 16le
        flags    : 8
        parent   : 8
        unk      : 2 * 16
        actor dir: 8     (direction an actor faces when in front of the object)
      VERB - verb-indexed script entries (offset table + bytecode)
        entries (vlc, 0x00 ends): entry:8, offset:16le
        bytecode follows
      OBNA - default object name (null-terminated string)

    The verb scripts are SCUMM bytecode and are not disassembled here. This is a
    read-only decode: the original bytes are kept and written back verbatim on save,
    so rebuilding the game file is always byte-identical.
    */
    public class ObjectCode : BlockBase
    {
        public ObjectCode(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "OBCD"; }
        }

        public byte[] RawContent { get; set; }

        // CDHD
        public bool HasCodeHeader { get; set; }
        public ushort ObjectId { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public byte Flags { get; set; }
        public byte ParentObject { get; set; }
        public byte ActorDirection { get; set; }

        // VERB
        public int NumVerbs { get; set; }

        // OBNA
        public string Name { get; set; }

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
            Name = string.Empty;

            // Walk the sub-blocks (type:4, size:32be, body) embedded in OBCD.
            int p = 0;
            while (p + 8 <= RawContent.Length)
            {
                string type = Encoding.ASCII.GetString(RawContent, p, 4);
                uint size = ReadUInt32BE(p + 4);
                if (size < 8 || p + size > RawContent.Length) break; // malformed/unknown layout

                int bodyStart = p + 8;
                int bodyLength = (int)size - 8;

                switch (type)
                {
                    case "CDHD":
                        ParseCodeHeader(bodyStart, bodyLength);
                        break;
                    case "VERB":
                        ParseVerbTable(bodyStart, bodyLength);
                        break;
                    case "OBNA":
                        Name = ReadCString(bodyStart, bodyLength);
                        break;
                }

                p += (int)size;
            }
        }

        private void ParseCodeHeader(int p, int length)
        {
            if (length < 17) return; // 5 * uint16 + 2 * byte + 2 * uint16 (unk) + 1 byte
            HasCodeHeader = true;
            ObjectId = ReadUInt16(p + 0);
            X = ReadUInt16(p + 2);
            Y = ReadUInt16(p + 4);
            Width = ReadUInt16(p + 6);
            Height = ReadUInt16(p + 8);
            Flags = RawContent[p + 10];
            ParentObject = RawContent[p + 11];
            // p + 12 .. p + 15 : unknown (2 * 16)
            ActorDirection = RawContent[p + 16];
        }

        private void ParseVerbTable(int p, int length)
        {
            NumVerbs = 0;
            int end = p + length;
            while (p < end)
            {
                byte entry = RawContent[p];
                if (entry == 0x00) break; // end of offset table
                NumVerbs++;
                p += 3; // entry (8) + offset (16le)
            }
        }

        private string ReadCString(int p, int length)
        {
            int end = p + length;
            var sb = new StringBuilder();
            for (int i = p; i < end; i++)
            {
                byte b = RawContent[i];
                if (b == 0x00) break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }

        private ushort ReadUInt16(int p)
        {
            return (ushort)(RawContent[p] | (RawContent[p + 1] << 8));
        }

        private uint ReadUInt32BE(int p)
        {
            return (uint)((RawContent[p] << 24) | (RawContent[p + 1] << 16) | (RawContent[p + 2] << 8) | RawContent[p + 3]);
        }
    }
}
