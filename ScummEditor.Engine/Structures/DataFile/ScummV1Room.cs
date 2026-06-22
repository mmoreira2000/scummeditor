namespace ScummEditor.Engine.Structures.DataFile
{
    /// <summary>
    /// Typed view of a SCUMM v1 room (Maniac Mansion 1987 / Zak McKracken 1988, classic DOS floppy). The
    /// object, verb, exit/entry-script, box and count layout is byte-identical to v2, so this extends
    /// <see cref="ScummV2Room"/> and inherits all of those accessors unchanged. The v1 header differs in
    /// only two ways (ScummVM ScummEngine_v3old::setupRoomSubBlocks, version &lt;= 1):
    ///   - width/height are single BYTES in 8-pixel CHAR units at +4/+5 (v2 stores u16 PIXELS at +4/+6);
    ///   - there is NO single IM00 background image at +0x0A. Instead five 16-bit room-relative pointers at
    ///     +10/+12/+14/+16/+18 (charMap, picMap, colorMap, maskMap, maskData) feed the GdiV1 four-map
    ///     tilemap codec, with four room colour bytes at +6..+9. Decoded by <see cref="Encoders.ScummV1ImageDecoder"/>.
    /// </summary>
    public class ScummV1Room : ScummV2Room
    {
        public ScummV1Room(byte[] roomFileBytes) : base(roomFileBytes) { }

        /// <summary>Room width in 8-pixel char columns (strips). Pixel width = WidthInChars * 8.</summary>
        public int WidthInChars { get { return Data.Length > 4 ? Data[4] : 0; } }

        /// <summary>Room height in 8-pixel char rows. Pixel height = HeightInChars * 8.</summary>
        public int HeightInChars { get { return Data.Length > 5 ? Data[5] : 0; } }

        public override int Width { get { return WidthInChars * 8; } }
        public override int Height { get { return HeightInChars * 8; } }

        /// <summary>One of the four room colour indices (EGA 0..15) at +6..+9; index 3 is replaced per cell by colorMap &amp; 7.</summary>
        public int Color(int i)
        {
            int p = 6 + i;
            return (i >= 0 && i < 4 && p < Data.Length) ? Data[p] : 0;
        }

        /// <summary>Room-relative offset of the shared 256-tile charMap (decodes to 2048 bytes).</summary>
        public int CharMapOffset { get { return ReadU16(10); } }

        /// <summary>Room-relative offset of the picMap (one tile-index byte per cell; WidthInChars * HeightInChars bytes).</summary>
        public int PicMapOffset { get { return ReadU16(12); } }

        /// <summary>Room-relative offset of the colorMap (one byte per cell; only low 3 bits used as the 4th colour).</summary>
        public int ColorMapOffset { get { return ReadU16(14); } }

        /// <summary>Room-relative offset of the maskMap (one mask-tile-index byte per cell).</summary>
        public int MaskMapOffset { get { return ReadU16(16); } }

        /// <summary>Room-relative offset of the maskData block: a u16 length (8 too big - ScummVM bug #3458) then the RLE.</summary>
        public int MaskDataOffset { get { return ReadU16(18); } }
    }
}
