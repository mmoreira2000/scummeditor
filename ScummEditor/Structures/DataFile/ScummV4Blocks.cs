using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    /// <summary>Shared walk for the v4 containers: read child blocks until the parent block ends.</summary>
    public static class ScummV4Blocks
    {
        public static void WalkChildren(BlockBase parent, Stream binaryReader, long endPosition)
        {
            while (binaryReader.Position < endPosition)
            {
                // Not every byte in a container is a self-describing block: a room's sound
                // region is raw resource data whose block size does not tile to the parent end.
                // When the next header is not a plausible block, keep the rest of the parent
                // verbatim so the file still round-trips exactly.
                if (!LooksLikeBlock(binaryReader, parent.GameInfo, endPosition))
                {
                    var raw = new RawDataBlock(parent, (int)(endPosition - binaryReader.Position));
                    raw.LoadFromBinaryReader(binaryReader);
                    parent.Childrens.Add(raw);
                    break;
                }

                string tag = BlockBase.PeekTag(binaryReader, parent.GameInfo);
                BlockBase child = CreateChild(parent, tag);
                child.LoadFromBinaryReader(binaryReader);
                parent.Childrens.Add(child);
            }
        }

        private static BlockBase CreateChild(BlockBase parent, string tag)
        {
            switch (tag)
            {
                case "FO":
                    return new RoomOffsetTable(parent); // room offset table (= v5/v6 LOFF)
                case "LF":
                    return new ScummV4DiskBlock(parent);
                case "RO":
                    return new ScummV4RoomBlock(parent);
                case "HD": // room header (= v5/v6 RMHD)
                    return new RoomHeader(parent);
                case "PA": // room palette (= v5/v6 CLUT)
                    return new PaletteData(parent, "PA");
                case "BM": // room background image (= v5/v6 RMIM/IM00/SMAP combined)
                    return new ScummV4ImageBlock(parent, "BM");
                case "OI": // object image (= v5/v6 OBIM/IMnn/SMAP combined)
                    return new ScummV4ImageBlock(parent, "OI");
                case "OC": // object code/metadata (= v5/v6 OBCD)
                    return new ObjectCode(parent);
                case "SC": // global script (= v5/v6 SCRP)
                case "LS": // local script  (= v5/v6 LSCR)
                case "EX": // exit script   (= v5/v6 EXCD)
                case "EN": // entry script  (= v5/v6 ENCD)
                    return new ScriptBlockV4(parent, tag);
                case "CO": // costume (= v5/v6 COST)
                    return new CostumeV4(parent);
                case "CC": // colour cycling (= v5 CYCL; v4 fixed 16x4 format)
                    return new ColorCyclesV4(parent);
                case "SP": // EGA/shadow palette (= v5 EPAL)
                    return new EgaShadowPaletteV4(parent);
                case "BX": // walk boxes + matrix (= v5 BOXD + BOXM combined)
                    return new BoxDataV4(parent);
                case "SA": // actor scale slots (= v5 SCAL)
                    return new ScaleV4(parent);
                case "LC": // local-script count (= v5 NLSC)
                    return new LocalScriptCountV4(parent);
                case "NL": // local-object/name list
                    return new LocalObjectListV4(parent);
                case "SL": // sound list
                    return new SoundListV4(parent);
                case "SO": // sound resource (WA/AD sub-blocks; raw AdLib tail stays a RawDataBlock sibling)
                    return new SoundBlockV4(parent);
                default:
                    return new NotImplementedDataBlock(parent, tag);
            }
        }

        /// <summary>
        /// True when the bytes at the current position form a plausible v4 block header: a size
        /// that fits within the parent and a 2-character uppercase/digit tag.
        /// </summary>
        private static bool LooksLikeBlock(Stream binaryReader, GameInfo gameInfo, long endPosition)
        {
            if (binaryReader.Position + 6 > endPosition)
            {
                return false;
            }

            byte[] head = binaryReader.PeekBytes(6);
            uint size = (uint)(head[0] | (head[1] << 8) | (head[2] << 16) | (head[3] << 24));

            if (size < 6 || binaryReader.Position + size > endPosition)
            {
                return false;
            }
            return IsTagByte(head[4]) && IsTagByte(head[5]);
        }

        private static bool IsTagByte(byte b)
        {
            return (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'A' && b <= (byte)'Z');
        }
    }
}
