using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ScummEditor.Engine.Structures.DataFile
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

        // v4 calls this block "OC"; v5/v6 call it "OBCD".
        public override string BlockType
        {
            get { return IsSmallHeader ? "OC" : "OBCD"; }
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
        /// <summary>Verb offset-table entries; offsets are relative to the VERB tag position.</summary>
        public List<VerbEntry> VerbEntries { get; private set; }
        /// <summary>Position of the VERB tag within RawContent, or -1 when absent.</summary>
        public int VerbBlockOffset { get; private set; }
        /// <summary>Total VERB block size (8-byte header + table + bytecode).</summary>
        public int VerbBlockSize { get; private set; }
        /// <summary>First bytecode position within RawContent (after the offset table), or -1.</summary>
        public int VerbCodeOffset { get; private set; }
        public int VerbCodeLength { get; private set; }
        /// <summary>
        /// Value to add to a VerbEntry.Offset to get that verb's index within RawContent. The two
        /// container layouts anchor the verb offsets differently:
        ///   v5/v6: relative to the VERB tag, so this equals VerbBlockOffset.
        ///   v4:    block-relative (the block starts HeaderLength bytes before RawContent), so this is -HeaderLength.
        /// </summary>
        public int VerbEntryBase { get; private set; }

        // OBNA
        public string Name { get; set; }
        /// <summary>Position of the OBNA tag within RawContent, or -1 when absent.</summary>
        public int ObnaBlockOffset { get; private set; }
        /// <summary>Position/length of the OBNA body (name bytes + terminator + padding).</summary>
        public int ObnaBodyOffset { get; private set; }
        public int ObnaBodyLength { get; private set; }

        // v4-specific OC layout positions within RawContent (used by the v4 text splicer).
        /// <summary>RawContent index of the v4 1-byte name pointer (a block-relative offset to the name); -1 if absent.</summary>
        public int NamePointerPos { get; private set; }
        /// <summary>RawContent index where the v4 verb table begins; -1 if absent.</summary>
        public int VerbTablePos { get; private set; }
        /// <summary>RawContent index just past the v4 verb table's 0x00 terminator; -1 if absent.</summary>
        public int VerbTableEnd { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));

            if (IsSmallHeader)
            {
                ParseV4CodeHeader();
            }
            else
            {
                ParseForDisplay();
            }
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent);
        }

        private void ParseForDisplay()
        {
            Name = string.Empty;
            VerbEntries = new List<VerbEntry>();
            VerbBlockOffset = -1;
            VerbEntryBase = -1;
            VerbCodeOffset = -1;
            ObnaBlockOffset = -1;
            ObnaBodyOffset = -1;

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
                        VerbBlockOffset = p;
                        VerbEntryBase = p; // v5/v6 verb offsets are relative to the VERB tag
                        VerbBlockSize = (int)size;
                        // SCUMM v8 (The Curse of Monkey Island) widened the verb offset table to 8-byte
                        // entries (id:32le + offset:32le, terminated by a 32-bit 0; offset relative to the
                        // VERB body, i.e. engine returns verboffs+8+stored). That differs from the v5/v6/v7
                        // 3-byte [id:8][offset:16le] table, so the shared ParseVerbTable would mis-parse it.
                        // v8 verb-code editing is a separate milestone; until then the v8 verb table is left
                        // unparsed (VerbCodeOffset stays -1) so nothing downstream extracts garbage.
                        if (_gameInfo == null || _gameInfo.ScummVersion != 8)
                        {
                            ParseVerbTable(bodyStart, bodyLength);
                        }
                        break;
                    case "OBNA":
                        ObnaBlockOffset = p;
                        ObnaBodyOffset = bodyStart;
                        ObnaBodyLength = bodyLength;
                        Name = ReadCString(bodyStart, bodyLength);
                        break;
                }

                p += (int)size;
            }
        }

        /// <summary>
        /// Parses the SCUMM v4 object header. Unlike v5/v6, a v4 OC block has no inner CDHD tag:
        /// its body IS the header, with a layout of its own (taken from ScummVM
        /// ScummEngine_v4::resetRoomObject). The verb scripts and object name follow the header
        /// and are kept in RawContent for byte-exact save; they are parsed by the text pipeline.
        ///   obj id : 16le @ +0
        ///   (unused): 8   @ +2
        ///   x      : 8    @ +3  (8-pixel units)
        ///   y      : 8    @ +4  (low 7 bits, 8-pixel units; bit 7 = parent state)
        ///   width  : 8    @ +5  (8-pixel units)
        ///   parent : 8    @ +6
        ///   walk x : 16le @ +7
        ///   walk y : 16le @ +9
        ///   dir/h  : 8    @ +11 (low 3 bits = actor direction; high 5 bits = height in pixels)
        /// </summary>
        private void ParseV4CodeHeader()
        {
            Name = string.Empty;
            VerbEntries = new List<VerbEntry>();
            VerbBlockOffset = -1;
            VerbEntryBase = 0;
            VerbCodeOffset = -1;
            VerbCodeLength = 0;
            ObnaBlockOffset = -1;
            ObnaBodyOffset = -1;
            ObnaBodyLength = 0;
            NamePointerPos = -1;
            VerbTablePos = -1;
            VerbTableEnd = -1;
            NumVerbs = 0;

            if (RawContent.Length < 12)
            {
                return;
            }

            HasCodeHeader = true;
            ObjectId = ReadUInt16(0);
            X = (ushort)(RawContent[3] * 8);
            Y = (ushort)((RawContent[4] & 0x7F) * 8);
            Width = (ushort)(RawContent[5] * 8);
            ParentObject = RawContent[6];
            // +7 .. +10 : walk_x:16le, walk_y:16le
            ActorDirection = (byte)(RawContent[11] & 0x07);
            Height = (ushort)(RawContent[11] & 0xF8); // already a multiple of 8 (pixels)

            ParseV4NameAndVerbs();
        }

        /// <summary>
        /// Parses the v4 object name and verb table that follow the 12-byte header. Both the 1-byte
        /// name pointer (at body+12) and the verb-table offsets (at body+13: [verbId:8][offset:16le]
        /// entries, ended by verbId 0x00) are relative to the OC BLOCK start - 6 bytes before
        /// RawContent (per ScummVM getObjOrActorName / getVerbEntrypoint, GF_SMALL_HEADER) - so a
        /// RawContent index is the stored offset minus HeaderLength. The name precedes the verb
        /// bytecode; the verb code is one contiguous stream from the lowest verb offset onward.
        /// </summary>
        private void ParseV4NameAndVerbs()
        {
            if (RawContent.Length < 13)
            {
                return;
            }

            int headerLength = (int)HeaderLength;

            // v4 verb offsets are block-relative; the block begins headerLength bytes before RawContent.
            VerbEntryBase = -headerLength;

            // 1-byte name pointer at body+12 (block-relative; 0 means the object has no name).
            NamePointerPos = 12;
            byte namePointer = RawContent[12];
            int nameIndex = namePointer - headerLength;

            // Verb table at body+13.
            VerbTablePos = 13;
            int p = 13;
            int minVerbIndex = int.MaxValue;
            while (p < RawContent.Length)
            {
                byte verbId = RawContent[p];
                if (verbId == 0x00) { p++; break; } // end of table
                if (p + 3 > RawContent.Length) { p = RawContent.Length; break; }

                int blockOffset = ReadUInt16(p + 1); // block-relative offset to this verb's bytecode
                VerbEntries.Add(new VerbEntry { Id = verbId, Offset = (ushort)blockOffset });

                int rawIndex = blockOffset - headerLength;
                if (rawIndex >= 0 && rawIndex < RawContent.Length && rawIndex < minVerbIndex)
                {
                    minVerbIndex = rawIndex;
                }
                p += 3;
            }
            VerbTableEnd = p;
            NumVerbs = VerbEntries.Count;

            // Object name (null-terminated) at nameIndex.
            if (namePointer != 0 && nameIndex >= 0 && nameIndex < RawContent.Length)
            {
                int term = nameIndex;
                while (term < RawContent.Length && RawContent[term] != 0x00) term++;
                ObnaBodyOffset = nameIndex;
                ObnaBodyLength = term - nameIndex; // name bytes, excluding the 0x00 terminator
                Name = ReadCString(nameIndex, ObnaBodyLength);
            }

            // Verb bytecode: one contiguous stream from the lowest verb offset to the block end,
            // but stopping before the name if the name happens to sit after the verb code.
            if (minVerbIndex != int.MaxValue)
            {
                int verbCodeEnd = RawContent.Length;
                if (ObnaBodyOffset >= 0 && ObnaBodyOffset >= minVerbIndex)
                {
                    verbCodeEnd = ObnaBodyOffset;
                }
                VerbCodeOffset = minVerbIndex;
                VerbCodeLength = verbCodeEnd - minVerbIndex;
            }
        }

        private void ParseCodeHeader(int p, int length)
        {
            // SCUMM v7 CDHD body has 8 bytes: version:32le, obj id:16le, parent:8, parent state:8.
            // (v7 moved the position/size fields out of the code header; only the id is kept here.)
            if (length == 8)
            {
                HasCodeHeader = true;
                ObjectId = ReadUInt16(p + 4);
                ParentObject = RawContent[p + 6];
                return;
            }

            // SCUMM v5 CDHD body has 13 bytes; x/y/w/h are bytes in 8-pixel units.
            //   obj id:16le, x:8, y:8, w:8, h:8, flags:8, parent:8, walk_x:16le, walk_y:16le, actor dir:8
            if (length == 13)
            {
                HasCodeHeader = true;
                ObjectId = ReadUInt16(p + 0);
                X = (ushort)(RawContent[p + 2] * 8);
                Y = (ushort)(RawContent[p + 3] * 8);
                Width = (ushort)(RawContent[p + 4] * 8);
                Height = (ushort)(RawContent[p + 5] * 8);
                Flags = RawContent[p + 6];
                ParentObject = RawContent[p + 7];
                // p + 8 .. p + 11 : walk_x:16le, walk_y:16le
                ActorDirection = RawContent[p + 12];
                return;
            }

            // SCUMM v6 CDHD body has 17 bytes, all positions in pixels.
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
                if (entry == 0x00) { p++; break; } // end of offset table
                if (p + 3 > end) { p = end; break; }
                NumVerbs++;
                VerbEntries.Add(new VerbEntry { Id = entry, Offset = ReadUInt16(p + 1) });
                p += 3; // entry (8) + offset (16le)
            }

            // Bytecode runs from the end of the table to the end of the VERB block.
            VerbCodeOffset = p;
            VerbCodeLength = end - p;
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

        /// <summary>Re-parses the structural info after RawContent is replaced (text import).</summary>
        public void Reparse()
        {
            if (IsSmallHeader)
            {
                ParseV4CodeHeader();
            }
            else
            {
                ParseForDisplay();
            }
        }
    }
}
