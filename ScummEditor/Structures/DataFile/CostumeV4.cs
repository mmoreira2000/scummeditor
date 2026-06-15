using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ScummEditor.Structures.DataFile
{
    /*
    SCUMM v4 costume ("CO" block, a child of the LF disk block; = v5/v6 "COST"). The body is the
    classic RLE costume (ScummVM ClassicCostumeLoader, formats 0x58 = 16 colours / 0x59 = 32 colours):

        body[0]            numAnim
        body[1]            format        (bit 7 = no west-mirror, bit 0 = palette size: 0=16, 1=32)
        body[2..]          palette        (16 or 32 bytes; indexes into the room palette)
        +2+numColors       animCmds offset (LE16, block-relative)
        +4+numColors       frame table offsets (16 x LE16, block-relative)
        +36+numColors      limb-data offsets (16 x LE16) ... then frame tables, then the CEL pictures

    All internal offsets are relative to the OC BLOCK start (6 bytes before RawContent), so a
    RawContent index is (offset - HeaderLength). A frame table is a list of LE16 CEL offsets; a CEL
    is a 12-byte header (w, h, relX, relY, moveX, moveY) followed by the column-major RLE pixels.

    This is a READ-ONLY view: the body is kept verbatim and written back unchanged, so the container
    round-trips byte-for-byte. The frame parse is best-effort and fully guarded - a stray "CO" block
    surfaced by the defensive container walk (or any malformed body) yields an empty frame list rather
    than throwing, exactly like the v4 image decoder.
    */
    public class CostumeV4 : BlockBase
    {
        public CostumeV4(BlockBase blockBase) : base(blockBase) { }

        public override string BlockType
        {
            get { return "CO"; }
        }

        public byte[] RawContent { get; set; }

        public byte NumAnim { get; private set; }
        public byte Format { get; private set; }
        public int PaletteSize { get; private set; }
        public List<byte> Palette { get; private set; }
        /// <summary>The distinct costume frames (CELs), decoded for display.</summary>
        public List<CostumeImageData> Frames { get; private set; }

        public override void CalculateBlockSize()
        {
            base.CalculateBlockSize();
            BlockSize += (uint)RawContent.Length;
        }

        public override void LoadFromBinaryReader(Stream binaryReader)
        {
            base.LoadFromBinaryReader(binaryReader);
            RawContent = binaryReader.ReadBytes((int)(BlockSize - HeaderLength));

            Palette = new List<byte>();
            Frames = new List<CostumeImageData>();
            try { ParseFrames(); }
            catch { Frames = new List<CostumeImageData>(); } // stray/garbage "CO": show nothing, never crash the load
        }

        public override void SaveToBinaryWriter(Stream binaryWriter)
        {
            base.SaveToBinaryWriter(binaryWriter);
            binaryWriter.WriteBytes(RawContent); // verbatim - read-only view, round-trips byte-for-byte
        }

        private int ReadUInt16At(int rawIndex)
        {
            return RawContent[rawIndex] | (RawContent[rawIndex + 1] << 8);
        }

        private short ReadInt16At(int rawIndex)
        {
            return (short)ReadUInt16At(rawIndex);
        }

        private void ParseFrames()
        {
            int headerLength = (int)HeaderLength; // 6 for v4; block-relative offset -> RawContent index = off - headerLength
            if (RawContent.Length < 4) return;

            NumAnim = RawContent[0];
            Format = RawContent[1];
            PaletteSize = (Format & 0x01) != 0 ? 32 : 16;

            int paletteStart = 2;
            if (paletteStart + PaletteSize > RawContent.Length) return;
            for (int i = 0; i < PaletteSize; i++) Palette.Add(RawContent[paletteStart + i]);

            // Frame table offsets: 16 LE16 entries after the anim-commands offset word.
            int frameTablePos = paletteStart + PaletteSize + 2;
            if (frameTablePos + 32 > RawContent.Length) return;
            var frameOffsets = new int[16];
            for (int i = 0; i < 16; i++) frameOffsets[i] = ReadUInt16At(frameTablePos + i * 2);

            // The CEL data begins at the largest frame-table offset (unused limbs point there); only
            // limbs whose offset is below that boundary hold a real frame table.
            int boundary = frameOffsets.Max();
            var distinct = frameOffsets.Where(o => o > 0).Distinct().OrderBy(o => o).ToList();

            var celOffsets = new SortedSet<int>();
            foreach (int fo in frameOffsets.Where(o => o > 0 && o < boundary).Distinct())
            {
                int tableEnd = boundary;
                foreach (int cand in distinct) { if (cand > fo) { tableEnd = Math.Min(cand, boundary); break; } }

                for (int e = fo; e + 2 <= tableEnd; e += 2)
                {
                    int ri = e - headerLength;
                    if (ri < 0 || ri + 2 > RawContent.Length) continue;
                    int celOffset = ReadUInt16At(ri);
                    if (celOffset >= boundary && IsSaneCel(celOffset - headerLength)) celOffsets.Add(celOffset);
                }
            }

            // Build the frames; each CEL's RLE runs to the next CEL (the decoder self-terminates anyway).
            var sorted = celOffsets.ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                int start = sorted[i] - headerLength;            // RawContent index of the CEL header
                int dataStart = start + 12;
                int end = (i + 1 < sorted.Count) ? sorted[i + 1] - headerLength : RawContent.Length;
                if (end < dataStart) end = RawContent.Length;

                var data = new byte[end - dataStart];
                Array.Copy(RawContent, dataStart, data, 0, data.Length);

                Frames.Add(new CostumeImageData
                {
                    Width = (ushort)ReadUInt16At(start),
                    Height = (ushort)ReadUInt16At(start + 2),
                    RelX = ReadInt16At(start + 4),
                    RelY = ReadInt16At(start + 6),
                    MoveX = ReadInt16At(start + 8),
                    MoveY = ReadInt16At(start + 10),
                    ImageData = data
                });
            }
        }

        /// <summary>True when the bytes at a RawContent index look like a real CEL header (sane size).</summary>
        private bool IsSaneCel(int rawIndex)
        {
            if (rawIndex < 0 || rawIndex + 12 > RawContent.Length) return false;
            int w = ReadUInt16At(rawIndex);
            int h = ReadUInt16At(rawIndex + 2);
            return w > 0 && w <= 1024 && h > 0 && h <= 1024;
        }
    }
}
