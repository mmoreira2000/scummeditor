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

            // v1 (Maniac/Zak classic, 0x57) differs: the frame table is at base+8 and its CEL offsets are
            // relative to limbBase (base+4), the CEL header is 6 bytes, and pixels are a C64 2-bit RLE.
            if (Format == 0x57) { ParseV1(); return; }

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
        /// Parses a v1 (format 0x57) costume. The 16 frame-table offsets are at base+8 and their CEL offsets
        /// are relative to limbBase = base+4 (not base); each CEL is a 6-byte header (widthBytes, height,
        /// relX*8, -relY, moveX*8, -moveY) followed by a C64 2-bit RLE. CELs are enumerated by WALKING THE
        /// ANIMATION command stream (mirrors ScummVM costumeDecodeData) so only CELs the costume can actually
        /// draw are listed - a deterministic, re-pack-invariant set (a bare frame-table scan would surface
        /// dead slots as phantom frames after an import re-pack). CelBaseOffsets are limbBase-relative.
        /// </summary>
        private void ParseV1()
        {
            int limbBase = _base + 4;
            if (_base + 26 > _data.Length) return; // header + frameOffsets (base+8) + start of dataOffsets (base+24)

            int numAnim = _data[_base + 4];
            int animCmds = limbBase + ReadU16(_base + 6); // the per-limb command stream
            int frameOffsetsTbl = _base + 8;              // 16 uint16 frame-table offsets, limbBase-relative
            int dataOffsetsTbl = _base + 24;              // 16 uint16 per-animation offsets, limbBase-relative

            // Enumerate CELs DETERMINISTICALLY by walking the animation definitions (mirrors ScummVM
            // costumeDecodeData): for every animation, the 1-byte limb mask selects limbs, each limb gives a
            // start index + length into the command stream, and each non-command code indexes that limb's
            // frame table to a CEL. This reads only the (re-pack-invariant) anim / frame-table structure, never
            // CEL-data bytes, so the frame set is stable across an import re-pack - no phantom frames.
            var celOffsets = new SortedSet<int>();
            for (int a = 0; a <= numAnim; a++)
            {
                int dpos = dataOffsetsTbl + a * 2;
                if (dpos + 2 > _data.Length) break;
                int animOff = ReadU16(dpos);
                if (animOff == 0) continue;
                int r = limbBase + animOff;
                if (r < 0 || r >= _data.Length) continue;
                int mask = _data[r] << 8; r++;
                for (int i = 0; i < 16 && (mask & 0xFFFF) != 0; i++, mask <<= 1)
                {
                    if ((mask & 0x8000) == 0) continue;
                    if (r >= _data.Length) break;
                    int j = _data[r++];
                    if (j == 0xFF) continue; // sentinel: this limb has no command (no extra byte follows)
                    if (r >= _data.Length) break;
                    int len = _data[r++] & 0x7F;
                    int frameTbl = limbBase + ReadU16(frameOffsetsTbl + i * 2);
                    for (int k = j; k <= j + len; k++)
                    {
                        int cmdPos = animCmds + k;
                        if (cmdPos < 0 || cmdPos >= _data.Length) break;
                        int code = _data[cmdPos] & 0x7F;
                        if (code == 0x79 || code == 0x7A || code == 0x7B) continue; // start/stop/no-draw commands
                        int entryPos = frameTbl + code * 2;
                        if (entryPos < 0 || entryPos + 2 > _data.Length) continue;
                        int celOffset = ReadU16(entryPos);
                        if (celOffset > 0 && IsSaneCelV1(limbBase + celOffset))
                        {
                            celOffsets.Add(celOffset);
                            CelOffsetEntryPositions.Add(entryPos);
                        }
                    }
                }
            }

            var sorted = celOffsets.ToList();
            CelBaseOffsets = sorted;
            if (sorted.Count > 0) CelDataStart = limbBase + sorted[0];
            for (int i = 0; i < sorted.Count; i++)
            {
                int start = limbBase + sorted[i]; // CEL header in roomData
                int widthBytes = _data[start];
                int height = _data[start + 1];
                int dataStart = start + 6;
                int rleLen = C64RleLength(_data, dataStart, widthBytes, height, ResourceEnd);
                if (rleLen < 0) continue;

                var data = new byte[rleLen];
                Array.Copy(_data, dataStart, data, 0, rleLen);

                Frames.Add(new CostumeImageData
                {
                    Width = (ushort)(widthBytes * 8),
                    Height = (ushort)height,
                    RelX = (short)((sbyte)_data[start + 2] * 8),
                    RelY = (short)(-(sbyte)_data[start + 3]),
                    MoveX = (short)((sbyte)_data[start + 4] * 8),
                    MoveY = (short)(-(sbyte)_data[start + 5]),
                    ImageData = data
                });
            }
        }

        /// <summary>True when a 0x57 CEL header + its C64 RLE are well-formed and decode within the resource.</summary>
        private bool IsSaneCelV1(int rawIndex)
        {
            if (rawIndex < 0 || rawIndex + 6 > _data.Length) return false;
            int widthBytes = _data[rawIndex];
            int height = _data[rawIndex + 1];
            if (widthBytes <= 0 || widthBytes > 64 || height <= 0 || height > 200) return false;
            return C64RleLength(_data, rawIndex + 6, widthBytes, height, ResourceEnd) >= 0;
        }

        /// <summary>
        /// Number of bytes the C64 2-bit costume RLE at <paramref name="offset"/> consumes to produce exactly
        /// widthBytes*height sample bytes, or -1 if it runs past <paramref name="maxEnd"/> (malformed / not a CEL).
        /// </summary>
        private static int C64RleLength(byte[] data, int offset, int widthBytes, int height, int maxEnd)
        {
            int total = widthBytes * height;
            if (total <= 0 || offset < 0) return -1;
            int p = offset, idx = 0;
            while (idx < total)
            {
                if (p >= maxEnd) return -1;
                byte len = data[p++];
                bool rep = (len & 0x80) != 0;
                int n = len & 0x7F;
                if (rep) { if (p >= maxEnd) return -1; p++; }
                for (int k = 0; k < n && idx < total; k++)
                {
                    if (!rep) { if (p >= maxEnd) return -1; p++; }
                    idx++;
                }
            }
            return p - offset;
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

            // v1 (0x57) CELs have a 6-byte header and limbBase(=base+4)-relative offsets; v2/v3-old
            // (0x58/0x59) have a 12-byte header and base-relative offsets.
            int headerSize = (Format == 0x57) ? 6 : 12;
            int offsetBaseDelta = (Format == 0x57) ? 4 : 0;

            int celDataStartRel = CelDataStart - _base; // base-relative start of the CEL region
            var region = new System.IO.MemoryStream();
            var newOffsetByOld = new Dictionary<int, int>();
            for (int i = 0; i < Frames.Count; i++)
            {
                int oldBaseOffset = CelBaseOffsets[i];
                int celHeaderPos = _base + offsetBaseDelta + oldBaseOffset;
                // record the new CEL position in the SAME convention the frame-table entries use
                newOffsetByOld[oldBaseOffset] = celDataStartRel + (int)region.Length - offsetBaseDelta;
                region.Write(_data, celHeaderPos, headerSize); // CEL header verbatim (size unchanged)
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
