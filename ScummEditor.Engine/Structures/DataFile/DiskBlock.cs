using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Engine.Structures.DataFile
{
    //LFLF - Disk Block
    //  ROOM - Room Block
    //  SCRP - Script Block
    //  SOUN - Sound Block
    //  COST - Costume Block
    //  CHAR - Charset
    public class DiskBlock : BlockBase
    {
        public DiskBlock(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "LFLF"; }
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);

            // The LFLF children end where the LFLF block ends; unknown block types are kept
            // as byte-preserved generic blocks (some fan editions pack stray blocks here).
            long endPosition = binaryReader.Position - 8 + BlockSize;

            var ROOM = new RoomBlock(this);
            ROOM.LoadFromBinaryReader(binaryReader);
            Childrens.Add(ROOM);

            while (binaryReader.Position < endPosition)
            {
                string typeRead = BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4));

                if (_gameInfo != null && (_gameInfo.ScummVersion == 7 || _gameInfo.ScummVersion == 8))
                {
                    LoadV7Child(binaryReader, typeRead, endPosition);
                    continue;
                }

                switch (typeRead)
                {
                    case "COST":
                        var costumeBlock = new Costume(this);
                        costumeBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(costumeBlock);
                        break;

                    case "SOUN":
                        // The sound block keeps its original bytes and only parses for display,
                        // so it is safe on both v5 (iMUSE in MI2/Indy4) and v6.
                        var soundBlock = new SoundBlock(this);
                        soundBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(soundBlock);
                        break;

                    case "SCRP":
                        // Typed script block; the disassembler is picked by SCUMM version.
                        var scriptBlock = new ScriptBlock(this, "SCRP");
                        scriptBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(scriptBlock);
                        break;

                    case "CHAR":
                        // The charset format (color map + offsets + glyphs) is the same on v5 and v6.
                        var charsetBlock = new Charset(this);
                        charsetBlock.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(charsetBlock);
                        break;

                    default:
                        var Default = new NotImplementedDataBlock(this, typeRead);
                        Default.LoadFromBinaryReader(binaryReader);
                        Childrens.Add(Default);
                        break;
                }
            }

            /*
            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "SCRP")
            {
                var scriptBlock = new NotImplementedDataBlock(this, "SCRP");
                scriptBlock.LoadFromBinaryReader(binaryReader);
                Childrens.Add(scriptBlock);
            }

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "SOUN")
            {
                var soundBlock = new NotImplementedDataBlock(this, "SOUN");
                soundBlock.LoadFromBinaryReader(binaryReader);
                Childrens.Add(soundBlock);
            }

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "COST")
            {
                var costumeBlock = new Costume(this);
                costumeBlock.LoadFromBinaryReader(binaryReader);
                Childrens.Add(costumeBlock);
            }

            while (BinaryHelper.ConvertByteArrayToUTF8String(binaryReader.PeekBytes(4)) == "CHAR")
            {
                var charsetBlock = new NotImplementedDataBlock(this, "CHAR");
                charsetBlock.LoadFromBinaryReader(binaryReader);
                Childrens.Add(charsetBlock);
            }
             */
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);

            foreach (var child in Childrens)
            {
                child.SaveToBinaryWriter(binaryWriter);
            }
        }

        /// <summary>
        /// Reads one v7 LFLF child. The LFLF holds the room (ROOM, already read) followed by its
        /// resources (SCRP, SOUN/iMUS, AKOS costumes, CHAR), read with the generic recursive reader so
        /// the file round-trips exactly and the AKOS/CHAR tags stay visible for index linking. Some
        /// rooms (e.g. Full Throttle room 88) also store an UNTAGGED data table between two AKOS blocks;
        /// that data is captured verbatim as a header-less raw block up to the next known resource, so
        /// the rest of the LFLF still parses (it is not a stream of self-describing IFF blocks).
        /// </summary>
        private void LoadV7Child(Stream binaryReader, string tag, long endPosition)
        {
            if (IsKnownV7LflfTag(tag) && HasValidBlockSize(binaryReader, endPosition))
            {
                // Global scripts are typed so the text pipeline can read them; AKOS costumes are typed so
                // the GUI shows the costume viewer; CHAR fonts are typed as Charset so the font viewer and
                // export/import work; SOUN sounds are typed as SoundBlockV7 so the sound viewer can decode
                // the iMUS/VOC audio. All of these subclass the generic container (or keep RawContent
                // verbatim), so the file still rebuilds byte-identically.
                BlockBase child;
                if (tag == "SCRP")
                {
                    child = new ScriptBlock(this, "SCRP");
                }
                else if (tag == "AKOS")
                {
                    child = new CostumeAkos(this, "AKOS");
                }
                else if (tag == "CHAR")
                {
                    child = new Charset(this);
                }
                else if (tag == "SOUN")
                {
                    child = new SoundBlockV7(this, "SOUN");
                }
                else if (tag == "RMSC")
                {
                    // v8 (The Curse of Monkey Island) holds a room's scripts/object code in a sibling RMSC
                    // block (v7 kept them inside ROOM); type it so the text pipeline finds the scripts/OBCD.
                    child = new RoomScriptsBlock(this);
                }
                else
                {
                    child = new RawContainerBlock(this, tag);
                }
                child.LoadFromBinaryReader(binaryReader);
                Childrens.Add(child);
                return;
            }

            long gapEnd = FindNextV7BlockOffset(binaryReader, endPosition);
            var gap = new RawDataBlock(this, (int)(gapEnd - binaryReader.Position));
            gap.LoadFromBinaryReader(binaryReader);
            Childrens.Add(gap);
        }

        /// <summary>The block tags that appear directly inside a v7/v8 LFLF (room disk block). v8 adds
        /// RMSC (the room-scripts block, holding the room's ENCD/EXCD/OBCD that v7 kept inside ROOM).</summary>
        private static bool IsKnownV7LflfTag(string tag)
        {
            return tag == "ROOM" || tag == "SCRP" || tag == "SOUN" || tag == "AKOS" || tag == "CHAR"
                || tag == "RMSC";
        }

        /// <summary>Peeks the block header at the current position; true when the size is well-formed
        /// (&gt;= 8) and the block fits before <paramref name="endPosition"/>.</summary>
        private static bool HasValidBlockSize(Stream binaryReader, long endPosition)
        {
            byte[] head = binaryReader.PeekBytes(8);
            if (head.Length < 8)
            {
                return false;
            }
            uint size = (uint)((head[4] << 24) | (head[5] << 16) | (head[6] << 8) | head[7]);
            return size >= 8 && binaryReader.Position + size <= endPosition;
        }

        /// <summary>
        /// Scans forward (in memory) from the current position for the next known LFLF child block
        /// (a known tag with a valid size). Returns <paramref name="endPosition"/> when there is none,
        /// so the trailing data is captured to the end of the LFLF.
        /// </summary>
        private static long FindNextV7BlockOffset(Stream binaryReader, long endPosition)
        {
            long start = binaryReader.Position;
            byte[] buffer = binaryReader.PeekBytes((int)(endPosition - start));
            for (int i = 1; i + 8 <= buffer.Length; i++)
            {
                if (!IsKnownV7LflfTagBytes(buffer, i))
                {
                    continue;
                }
                uint size = (uint)((buffer[i + 4] << 24) | (buffer[i + 5] << 16) | (buffer[i + 6] << 8) | buffer[i + 7]);
                if (size >= 8 && start + i + size <= endPosition)
                {
                    return start + i;
                }
            }
            return endPosition;
        }

        private static bool IsKnownV7LflfTagBytes(byte[] b, int o)
        {
            return MatchTag(b, o, "ROOM") || MatchTag(b, o, "SCRP") || MatchTag(b, o, "SOUN")
                || MatchTag(b, o, "AKOS") || MatchTag(b, o, "CHAR") || MatchTag(b, o, "RMSC");
        }

        private static bool MatchTag(byte[] b, int o, string tag)
        {
            return b[o] == tag[0] && b[o + 1] == tag[1] && b[o + 2] == tag[2] && b[o + 3] == tag[3];
        }

        public RoomBlock GetROOM()
        {
            return (RoomBlock)Childrens.Single(x => x.GetType() == typeof(RoomBlock));
        }

        public List<Costume> GetCostumes()
        {
            return Childrens.OfType<Costume>().ToList();
        }
    }
}