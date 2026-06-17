using System;
using System.Collections.Generic;
using System.Linq;

namespace ScummEditor.Engine.Structures.DataFile
{
    /*
    A SCUMM v3 "old bundle" costume (Loom EGA, Indy3 EGA). Unlike v4 (a tagged "CO" block) the costume
    is a raw region inside a room file, located by the index COSTUME directory's (roomNumber, offset).
    The layout is the classic RLE costume read by ScummVM ClassicCostumeLoader::loadCostume, with the
    GF_OLD_BUNDLE adjustments (costume.cpp:417-483):

        base = the costume's offset (the resource's [size:u16] word position in the room file)
        base+4   numAnim
        base+5   format (low 7 bits; 0x58 = 16-colour, 0x59 = 32-colour; bit7 = no west mirror)
        base+6   ONE colour byte (old-bundle has no real palette - a fixed 16-colour HW palette is used)
        base+7   animCmds offset (LE16, relative to base)
        base+9   16 x frame-table offset (LE16, relative to base)
        base+41  16 x limb-data offset (LE16, relative to base)
    A frame table is a list of LE16 CEL offsets (relative to base); a CEL is a 12-byte header
    (w,h,relX,relY,moveX,moveY) followed by column-major RLE pixels - identical to v4, so the existing
    CostumeImageDecoderV4 decodes the frames. The 16-colour EGA palette is the fixed hardware one.

    Read-only view over the room bytes (no own storage): the frames are a decode/edit overlay.
    */
    public class CostumeV3Old
    {
        private readonly byte[] _data;
        private readonly int _base;

        public CostumeV3Old(byte[] roomData, int costumeOffset)
        {
            _data = roomData;
            _base = costumeOffset;
            Palette = new List<byte>();
            Frames = new List<CostumeImageData>();
            CelBaseOffsets = new List<int>();
            CelOffsetEntryPositions = new List<int>();
            try { Parse(); } catch { Frames = new List<CostumeImageData>(); }
        }

        /// <summary>Total costume resource length (the [size:u16] word at the costume offset).</summary>
        public int ResourceSize { get; private set; }
        public byte NumAnim { get; private set; }
        public byte Format { get; private set; }
        public int PaletteSize { get; private set; }
        public List<byte> Palette { get; private set; }
        public List<CostumeImageData> Frames { get; private set; }

        /// <summary>base-relative offset of each frame's CEL header (parallel to Frames), for editing.</summary>
        public List<int> CelBaseOffsets { get; private set; }
        /// <summary>roomData indexes of every frame-table entry holding a CEL offset, for editing.</summary>
        public List<int> CelOffsetEntryPositions { get; private set; }
        /// <summary>roomData index of the first CEL (start of the CEL region), or -1.</summary>
        public int CelDataStart { get; private set; } = -1;

        private int ReadU16(int rawIndex)
        {
            return _data[rawIndex] | (_data[rawIndex + 1] << 8);
        }

        private short ReadI16(int rawIndex)
        {
            return (short)ReadU16(rawIndex);
        }

        /// <summary>End of the costume resource in the room bytes (bounds the last CEL's pixel data).</summary>
        private int ResourceEnd
        {
            get
            {
                int end = _base + ResourceSize;
                return (ResourceSize >= 4 && end <= _data.Length) ? end : _data.Length;
            }
        }

        private void Parse()
        {
            if (_base < 0 || _base + 9 + 32 > _data.Length) return;

            ResourceSize = ReadU16(_base);
            NumAnim = _data[_base + 4];
            Format = (byte)(_data[_base + 5] & 0x7F);
            // old-bundle: 1 colour byte (or 0 for the v1 format 0x57); palette is the fixed EGA HW one
            PaletteSize = (Format == 0x57) ? 0 : (Format == 0x59 ? 32 : 16);
            int colorBytes = (Format == 0x57) ? 0 : 1;
            for (int i = 0; i < colorBytes; i++) Palette.Add(_data[_base + 6 + i]);

            // 16 frame-table offsets at base+9 (base-relative).
            int frameTablePos = _base + 9;
            var frameOffsets = new int[16];
            for (int i = 0; i < 16; i++) frameOffsets[i] = ReadU16(frameTablePos + i * 2);

            int boundary = frameOffsets.Max();
            var distinct = frameOffsets.Where(o => o > 0).Distinct().OrderBy(o => o).ToList();

            var celOffsets = new SortedSet<int>();
            foreach (int fo in frameOffsets.Where(o => o > 0 && o < boundary).Distinct())
            {
                int tableEnd = boundary;
                foreach (int cand in distinct) { if (cand > fo) { tableEnd = Math.Min(cand, boundary); break; } }

                for (int e = fo; e + 2 <= tableEnd; e += 2)
                {
                    int ri = _base + e; // base-relative -> roomData index
                    if (ri < 0 || ri + 2 > _data.Length) continue;
                    int celOffset = ReadU16(ri);
                    if (celOffset >= boundary && IsSaneCel(_base + celOffset))
                    {
                        celOffsets.Add(celOffset);
                        CelOffsetEntryPositions.Add(ri);
                    }
                }
            }

            var sorted = celOffsets.ToList();
            CelBaseOffsets = sorted;
            if (sorted.Count > 0) CelDataStart = _base + sorted[0];
            for (int i = 0; i < sorted.Count; i++)
            {
                int start = _base + sorted[i];           // CEL header in roomData
                int dataStart = start + 12;
                int end = (i + 1 < sorted.Count) ? _base + sorted[i + 1] : ResourceEnd;
                if (end < dataStart) end = ResourceEnd;

                var data = new byte[end - dataStart];
                Array.Copy(_data, dataStart, data, 0, data.Length);

                Frames.Add(new CostumeImageData
                {
                    Width = (ushort)ReadU16(start),
                    Height = (ushort)ReadU16(start + 2),
                    RelX = ReadI16(start + 4),
                    RelY = ReadI16(start + 6),
                    MoveX = ReadI16(start + 8),
                    MoveY = ReadI16(start + 10),
                    ImageData = data
                });
            }
        }

        private bool IsSaneCel(int rawIndex)
        {
            if (rawIndex < 0 || rawIndex + 12 > _data.Length) return false;
            int w = ReadU16(rawIndex);
            int h = ReadU16(rawIndex + 2);
            return w > 0 && w <= 1024 && h > 0 && h <= 1024;
        }

        /// <summary>
        /// Rebuilds the whole costume resource with one frame's RLE replaced, mirroring
        /// CostumeV4.ReplaceFrameImage but with base-relative offsets. Everything before the first CEL
        /// (header, palette, frame/limb tables) is preserved; the CEL region is rebuilt (each CEL keeps
        /// its 12-byte header, only the edited frame's pixels change) and every frame-table CEL offset
        /// is re-pointed. Returns the new resource bytes (the leading [size:u16] is left unchanged; the
        /// caller grows it via ScummV3OldWriter.ApplyEdit's sizeWordOffset). Throws if not editable.
        /// </summary>
        public byte[] BuildWithReplacedFrames(Dictionary<int, byte[]> replacements)
        {
            if (CelDataStart < 0 || CelBaseOffsets.Count != Frames.Count)
            {
                throw new Exceptions.ImageEncodeException("This costume's frame table could not be parsed, so it cannot be edited.");
            }

            int celDataStartRel = CelDataStart - _base; // base-relative start of the CEL region
            var region = new System.IO.MemoryStream();
            var newOffsetByOld = new Dictionary<int, int>();
            for (int i = 0; i < Frames.Count; i++)
            {
                int oldBaseOffset = CelBaseOffsets[i];
                int celHeaderPos = _base + oldBaseOffset;
                newOffsetByOld[oldBaseOffset] = celDataStartRel + (int)region.Length;
                region.Write(_data, celHeaderPos, 12); // CEL header verbatim (size unchanged)
                byte[] data;
                if (!replacements.TryGetValue(i, out data)) data = Frames[i].ImageData;
                region.Write(data, 0, data.Length);
            }

            byte[] regionBytes = region.ToArray();
            var rebuilt = new byte[celDataStartRel + regionBytes.Length];
            Array.Copy(_data, _base, rebuilt, 0, celDataStartRel);                 // [base, firstCEL) verbatim
            Array.Copy(regionBytes, 0, rebuilt, celDataStartRel, regionBytes.Length);

            foreach (int entryPos in CelOffsetEntryPositions)
            {
                int rel = entryPos - _base; // frame-table entry's position within the resource
                if (rel < 0 || rel + 2 > rebuilt.Length) continue;
                int oldOffset = rebuilt[rel] | (rebuilt[rel + 1] << 8);
                int updated;
                if (newOffsetByOld.TryGetValue(oldOffset, out updated))
                {
                    rebuilt[rel] = (byte)(updated & 0xFF);
                    rebuilt[rel + 1] = (byte)((updated >> 8) & 0xFF);
                }
            }
            return rebuilt;
        }
    }
}
