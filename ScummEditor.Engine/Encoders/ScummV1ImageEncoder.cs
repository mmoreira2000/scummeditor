using System;
using System.Collections.Generic;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited v1 (GdiV1 tilemap) room image back into a rebuilt room resource. v1 keeps the
    /// room background and EVERY object image in ONE shared 256-tile charMap (the background's picMap and each
    /// object's tile plane both index it via room+10), and the background and every object mask in ONE shared
    /// maskChar table (room+18). So the cardinal rule here is: PRESERVE the existing charMap / maskChar exactly
    /// (every other image keeps decoding) and only ADD the edited image's new tiles - into the charMap's free
    /// (unreferenced) slots, or appended to the variable-length maskChar - NEVER renumber. The room is then
    /// rebuilt COMPACTLY (AssembleCompactRoom): every region (the box, the 5 maps, each object image/code, the
    /// scripts) is re-laid back to back with real RLE compression and every offset field re-pointed, so an edit
    /// grows the room by only a few bytes. (An earlier version appended the new maps and left the originals as
    /// dead bytes, ~doubling the room - which the real v1 engine could not load, so the game black-screened.)
    ///
    /// LOSSY BY FORMAT: each 8x8 cell can show only 4 colours (3 fixed room colours + 1 free per cell), the
    /// charMap holds at most 256 distinct tiles and the maskChar at most 256 distinct mask tiles, so an edit
    /// that exceeds those limits is REJECTED with an error rather than silently corrupted. Re-encoding an
    /// UNEDITED decoded image is pixel-lossless (it satisfies the constraints by construction) and leaves the
    /// shared tables byte-identical (so it cannot disturb any other image). Mirrors ScummV1ImageDecoder.
    /// </summary>
    public class ScummV1ImageEncoder
    {
        // Same v1ColorMaps render-remap tables as the decoder (Maniac vs Zak differ at slots 10/15).
        private static readonly byte[] ZakEgaColorMap =
            { 0x00, 0x0F, 0x04, 0x03, 0x05, 0x02, 0x01, 0x0E, 0x0C, 0x06, 0x0D, 0x08, 0x07, 0x0A, 0x09, 0x07 };
        private static readonly byte[] ManiacEgaColorMap =
            { 0x00, 0x0F, 0x04, 0x03, 0x05, 0x02, 0x01, 0x0E, 0x0C, 0x06, 0x0C, 0x08, 0x07, 0x0A, 0x09, 0x08 };

        private readonly byte[] _renderMap;
        private readonly int[] _inverseLow8; // EGA index -> the colorMap value 0..7 that renders to it, or -1
        private readonly ScummV1ImageDecoder _decoder;

        public ScummV1ImageEncoder(bool isManiac)
        {
            _renderMap = isManiac ? ManiacEgaColorMap : ZakEgaColorMap;
            _inverseLow8 = new int[16];
            for (int i = 0; i < 16; i++) _inverseLow8[i] = -1;
            for (int c = 0; c < 8; c++) _inverseLow8[_renderMap[c]] = c; // the first 8 render-map entries are distinct
            _decoder = new ScummV1ImageDecoder(isManiac);
        }

        // --- background image -------------------------------------------------

        /// <summary>
        /// Rebuilds the room resource with the background re-encoded from <paramref name="matrix"/> (a
        /// [width,height] matrix of render-remapped EGA indices, as produced by ScummV1ImageDecoder.BackgroundMatrix).
        /// Returns null with an error if the edit cannot be represented (unrepresentable colour / &gt;256 tiles / too big).
        /// </summary>
        public byte[] EncodeBackground(ScummV1Room room, byte[,] matrix, out string error)
        {
            error = null;
            int w = room.WidthInChars, h = room.HeightInChars;
            if (w <= 0 || h <= 0) { error = "The room has no decodable background."; return null; }
            if (matrix.GetLength(0) != w * 8 || matrix.GetLength(1) != h * 8)
            {
                error = "The image must be " + (w * 8) + "x" + (h * 8) + " (the original size).";
                return null;
            }

            // Start from the room's real charMap so every object image keeps its tiles; reserve the tiles the
            // objects use so adding background tiles can never overwrite them.
            byte[] charMap = LoadCharMap(room);
            bool[] locked = ReferencedCharIndices(room, includeBackground: false, excludeObject: -1);
            var existing = BuildTileLookup(charMap);
            bool charMapChanged = false;

            // Cells whose pixels are unchanged reuse their ORIGINAL tile index + colorMap byte verbatim - the
            // decode->encode mapping is not byte-exact (the render-remap is many-to-one), so recomputing every
            // cell would needlessly allocate tiles and could exhaust the shared 256-slot charMap.
            byte[,] orig = _decoder.BackgroundMatrix(room);
            byte[] origPic = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.PicMapOffset, w * h);
            byte[] origColor = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.ColorMapOffset, w * h);
            bool canReuse = orig != null && origPic != null && origColor != null;

            int r0 = _renderMap[room.Color(0) & 0x0F];
            int r1 = _renderMap[room.Color(1) & 0x0F];
            int r2 = _renderMap[room.Color(2) & 0x0F];

            var picMap = new byte[w * h];
            var colorMap = new byte[w * h];

            // Pass 1: reuse every UNCHANGED cell's original tile and LOCK it BEFORE any allocation - otherwise
            // a changed cell could grab (overwrite) a free slot a later unchanged cell still references,
            // corrupting an unedited region of the same background.
            var changedCells = new List<int>();
            for (int strip = 0; strip < w; strip++)
            {
                for (int y = 0; y < h; y++)
                {
                    int cell = y + strip * h; // column-major (matches the decoder)
                    if (canReuse && CellUnchanged(matrix, orig, strip, y))
                    {
                        picMap[cell] = origPic[cell];
                        colorMap[cell] = origColor[cell];
                        locked[origPic[cell]] = true;
                    }
                    else changedCells.Add(cell);
                }
            }

            // Pass 2: allocate tiles for the changed cells (all reused tiles are now locked).
            foreach (int cell in changedCells)
            {
                int strip = cell / h, y = cell % h;
                var tile = new byte[8];
                int c3 = -1;
                if (!EncodeImageCell(matrix, strip, y, r0, r1, r2, tile, ref c3, out error)) return null;

                int idx;
                if (!AllocateTile(charMap, locked, existing, tile, ref charMapChanged, out idx, out error)) return null;
                picMap[cell] = (byte)idx;
                colorMap[cell] = (byte)(c3 < 0 ? 0 : c3);
            }

            // Rebuild the room COMPACTLY (the 5 maps re-encoded in place, compressed). The mask maps are
            // unchanged by a background edit, so keep the originals.
            byte[] origMaskMap = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.MaskMapOffset, w * h) ?? new byte[w * h];
            byte[] origMaskChar = LoadMaskChar(room).ToArray();
            return AssembleCompactRoom(room, charMap, picMap, colorMap, origMaskMap, origMaskChar, null, out error);
        }

        // --- object image -----------------------------------------------------

        /// <summary>
        /// Rebuilds the room resource with object <paramref name="objectIndex"/>'s image re-encoded from
        /// <paramref name="matrix"/>. Only the tile (plane 0) and colour (plane 1) planes are rebuilt; the
        /// object's walk-behind mask (plane 2) is preserved. New tiles go into the shared charMap's free slots.
        /// </summary>
        public byte[] EncodeObject(ScummV1Room room, int objectIndex, byte[,] matrix, out string error)
        {
            error = null;
            int obim, w, h;
            byte[] objectMap = LoadObjectMap(room, objectIndex, out obim, out w, out h, out error);
            if (objectMap == null) return null;
            if (matrix.GetLength(0) != w * 8 || matrix.GetLength(1) != h * 8)
            {
                error = "The image must be " + (w * 8) + "x" + (h * 8) + " (the original size).";
                return null;
            }

            // Preserve the charMap (the background + every other object share it) and reserve the tiles THOSE
            // images use; this object's own tiles stay free so changed cells can recycle them.
            byte[] charMap = LoadCharMap(room);
            bool[] locked = ReferencedCharIndices(room, includeBackground: true, excludeObject: objectIndex);
            var existing = BuildTileLookup(charMap);
            bool charMapChanged = false;

            // Unchanged cells reuse this object's original tile/colour bytes verbatim (see EncodeBackground).
            byte[,] orig = _decoder.ObjectMatrix(room, objectIndex);
            bool canReuse = orig != null;

            int r0 = _renderMap[room.Color(0) & 0x0F];
            int r1 = _renderMap[room.Color(1) & 0x0F];
            int r2 = _renderMap[room.Color(2) & 0x0F];

            var newMap = new byte[w * h * 3];
            Array.Copy(objectMap, 2 * w * h, newMap, 2 * w * h, w * h); // preserve plane 2 (mask) verbatim

            // Pass 1: reuse unchanged cells and LOCK their tiles before any allocation (see EncodeBackground).
            var changedCells = new List<int>();
            for (int strip = 0; strip < w; strip++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (canReuse && CellUnchanged(matrix, orig, strip, y))
                    {
                        byte t = objectMap[y * w + strip];
                        newMap[y * w + strip] = t;                              // reuse plane 0 (tile)
                        newMap[(y + h) * w + strip] = objectMap[(y + h) * w + strip]; // reuse plane 1 (colour)
                        locked[t] = true;
                    }
                    else changedCells.Add(strip * h + y);
                }
            }

            // Pass 2: allocate tiles for the changed cells (all reused tiles are now locked).
            foreach (int lin in changedCells)
            {
                int strip = lin / h, y = lin % h;
                var tile = new byte[8];
                int c3 = -1;
                if (!EncodeImageCell(matrix, strip, y, r0, r1, r2, tile, ref c3, out error)) return null;

                int idx;
                if (!AllocateTile(charMap, locked, existing, tile, ref charMapChanged, out idx, out error)) return null;
                newMap[y * w + strip] = (byte)idx;                 // plane 0 (tile)
                newMap[(y + h) * w + strip] = (byte)(c3 < 0 ? 0 : c3); // plane 1 (colour)
            }

            // Rebuild the room COMPACTLY: the edited object image (and the possibly-grown shared charMap) are
            // re-encoded in place; the background colour maps and the masks are preserved (re-encoded losslessly).
            int bw = room.WidthInChars, bh = room.HeightInChars, mapLen = Math.Max(1, bw * bh);
            byte[] origPic = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.PicMapOffset, bw * bh) ?? new byte[mapLen];
            byte[] origColor = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.ColorMapOffset, bw * bh) ?? new byte[mapLen];
            byte[] origMaskMap = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.MaskMapOffset, bw * bh) ?? new byte[mapLen];
            byte[] origMaskChar = LoadMaskChar(room).ToArray();
            var objEdits = new Dictionary<int, byte[]> { { objectIndex, newMap } };
            return AssembleCompactRoom(room, charMap, origPic, origColor, origMaskMap, origMaskChar, objEdits, out error);
        }

        // --- background walk-behind (z-plane) mask ----------------------------

        /// <summary>
        /// Rebuilds the room resource with the background walk-behind mask re-encoded from
        /// <paramref name="mask"/> (a [width,height] matrix; 1 = masked/white). New mask tiles are appended to
        /// the shared maskChar (object masks keep their indices). Re-points +16 (maskMap) and +18 (maskData).
        /// </summary>
        public byte[] EncodeBackgroundZPlane(ScummV1Room room, byte[,] mask, out string error)
        {
            error = null;
            int w = room.WidthInChars, h = room.HeightInChars;
            if (w <= 0 || h <= 0) { error = "The room has no decodable background to mask."; return null; }
            if (mask.GetLength(0) != w * 8 || mask.GetLength(1) != h * 8)
            {
                error = "The mask must be " + (w * 8) + "x" + (h * 8) + " (the original size).";
                return null;
            }

            var maskChar = LoadMaskChar(room);
            var existing = BuildMaskLookup(maskChar);
            var maskMap = new byte[w * h];

            for (int strip = 0; strip < w; strip++)
            {
                for (int y = 0; y < h; y++)
                {
                    var tile = new byte[8];
                    EncodeMaskCell(mask, strip, y, tile);
                    int idx;
                    if (!AllocateMaskTile(maskChar, existing, tile, out idx, out error)) return null;
                    maskMap[y + strip * h] = (byte)idx; // column-major (matches the decoder)
                }
            }

            // Rebuild the room COMPACTLY. The colour maps are unchanged by a mask edit, so keep the originals.
            byte[] origChar = LoadCharMap(room);
            byte[] origPic = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.PicMapOffset, w * h) ?? new byte[w * h];
            byte[] origColor = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.ColorMapOffset, w * h) ?? new byte[w * h];
            return AssembleCompactRoom(room, origChar, origPic, origColor, maskMap, maskChar.ToArray(), null, out error);
        }

        // --- object walk-behind (z-plane) mask --------------------------------

        /// <summary>
        /// Rebuilds the room resource with object <paramref name="objectIndex"/>'s walk-behind mask (plane 2)
        /// re-encoded from <paramref name="mask"/>. Planes 0/1 (tile/colour) are preserved. New mask tiles are
        /// appended to the shared maskChar (the background mask keeps its indices). Re-points +18 and OBIM[k].
        /// </summary>
        public byte[] EncodeObjectZPlane(ScummV1Room room, int objectIndex, byte[,] mask, out string error)
        {
            error = null;
            int obim, w, h;
            byte[] objectMap = LoadObjectMap(room, objectIndex, out obim, out w, out h, out error);
            if (objectMap == null) return null;
            if (mask.GetLength(0) != w * 8 || mask.GetLength(1) != h * 8)
            {
                error = "The mask must be " + (w * 8) + "x" + (h * 8) + " (the original size).";
                return null;
            }

            var maskChar = LoadMaskChar(room);
            var existing = BuildMaskLookup(maskChar);

            var newMap = new byte[w * h * 3];
            Array.Copy(objectMap, 0, newMap, 0, 2 * w * h); // preserve planes 0 (tile) and 1 (colour)

            for (int strip = 0; strip < w; strip++)
            {
                for (int y = 0; y < h; y++)
                {
                    var tile = new byte[8];
                    EncodeMaskCell(mask, strip, y, tile);
                    int idx;
                    if (!AllocateMaskTile(maskChar, existing, tile, out idx, out error)) return null;
                    newMap[(y + 2 * h) * w + strip] = (byte)idx; // plane 2 (mask), row-major
                }
            }

            // Rebuild the room COMPACTLY: the edited object mask (plane 2, with the possibly-grown shared
            // maskChar) is re-encoded in place; the charMap, colour maps and the background mask are preserved.
            int bw = room.WidthInChars, bh = room.HeightInChars, mapLen = Math.Max(1, bw * bh);
            byte[] origChar = LoadCharMap(room);
            byte[] origPic = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.PicMapOffset, bw * bh) ?? new byte[mapLen];
            byte[] origColor = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.ColorMapOffset, bw * bh) ?? new byte[mapLen];
            byte[] origMaskMap = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.MaskMapOffset, bw * bh) ?? new byte[mapLen];
            var objEdits = new Dictionary<int, byte[]> { { objectIndex, newMap } };
            return AssembleCompactRoom(room, origChar, origPic, origColor, origMaskMap, maskChar.ToArray(), objEdits, out error);
        }

        // --- cell encoders ----------------------------------------------------

        /// <summary>
        /// Encodes one 8x8 image cell at char column <paramref name="strip"/>, row <paramref name="y"/> of
        /// <paramref name="matrix"/> into a 2bpp tile (the doubled-pixel layout the decoder reads) plus the
        /// cell's single free 4th colour (<paramref name="c3"/>, 0..7). Returns false with an error if a pixel
        /// uses a colour v1 cannot store.
        /// </summary>
        private bool EncodeImageCell(byte[,] matrix, int strip, int y, int r0, int r1, int r2, byte[] tile, ref int c3, out string error)
        {
            error = null;
            for (int i = 0; i < 8; i++)
            {
                int b = 0;
                for (int p = 0; p < 4; p++)
                {
                    int ega = matrix[strip * 8 + p * 2, y * 8 + i]; // left pixel of each doubled pair
                    int v;
                    if (ega == r0) v = 0;
                    else if (ega == r1) v = 1;
                    else if (ega == r2) v = 2;
                    else
                    {
                        int c = _inverseLow8[ega & 0x0F];
                        if (c < 0)
                        {
                            error = "A pixel uses EGA colour " + ega + ", which v1 cannot store as a cell's 4th colour.";
                            return false;
                        }
                        if (c3 < 0) c3 = c; // a cell holds only one free colour; the first one wins
                        v = 3;
                    }
                    b |= v << (6 - p * 2); // p0 = leftmost pair -> bits 6-7 (matches the decoder's (c>>6))
                }
                tile[i] = (byte)b;
            }
            return true;
        }

        /// <summary>True when the edited and original matrices are pixel-identical over the 8x8 cell at (strip,y).</summary>
        private static bool CellUnchanged(byte[,] edited, byte[,] orig, int strip, int y)
        {
            for (int i = 0; i < 8; i++)
                for (int x = 0; x < 8; x++)
                    if (edited[strip * 8 + x, y * 8 + i] != orig[strip * 8 + x, y * 8 + i]) return false;
            return true;
        }

        /// <summary>
        /// Encodes one 8x8 mask cell into an inverted 1bpp tile: bit 7 = leftmost pixel, a set source pixel
        /// (white = masked) becomes a 0 bit after the ^0xFF the decoder undoes.
        /// </summary>
        private static void EncodeMaskCell(byte[,] mask, int strip, int y, byte[] tile)
        {
            for (int i = 0; i < 8; i++)
            {
                int c = 0;
                for (int b = 0; b < 8; b++)
                    if (mask[strip * 8 + b, y * 8 + i] != 0) c |= 1 << (7 - b);
                tile[i] = (byte)(c ^ 0xFF); // v1/v0 masks are stored inverted
            }
        }

        // --- shared-table allocation ------------------------------------------

        /// <summary>
        /// Returns a charMap index for <paramref name="tile"/>: reuses an existing identical tile, otherwise
        /// writes it into a free (unlocked) slot and sets <paramref name="changed"/>. Every index handed out is
        /// locked so a later allocation cannot overwrite it. False with an error when all 256 slots are taken.
        /// </summary>
        private static bool AllocateTile(byte[] charMap, bool[] locked, Dictionary<string, int> existing,
            byte[] tile, ref bool changed, out int index, out string error)
        {
            error = null;
            string key = Convert.ToBase64String(tile);
            if (existing.TryGetValue(key, out index)) { locked[index] = true; return true; }

            for (int i = 0; i < 256; i++)
            {
                if (locked[i]) continue;
                index = i;
                locked[i] = true;
                Array.Copy(tile, 0, charMap, i * 8, 8);
                existing[key] = i;
                changed = true;
                return true;
            }
            error = "The edited image needs more than 256 distinct 8x8 tiles, which v1 cannot store in the shared charMap.";
            index = 0;
            return false;
        }

        /// <summary>
        /// Returns a maskChar index for <paramref name="tile"/>: reuses an existing identical mask tile,
        /// otherwise appends it (so existing tiles keep their indices). False with an error past 256 tiles.
        /// </summary>
        private static bool AllocateMaskTile(List<byte> maskChar, Dictionary<string, int> existing,
            byte[] tile, out int index, out string error)
        {
            error = null;
            string key = Convert.ToBase64String(tile);
            if (existing.TryGetValue(key, out index)) return true;

            index = maskChar.Count / 8;
            if (index > 255)
            {
                error = "The edited mask needs more than 256 distinct 8x8 tiles, which v1 cannot store.";
                return false;
            }
            maskChar.AddRange(tile);
            existing[key] = index;
            return true;
        }

        /// <summary>The set of charMap indices already used by the images we are NOT editing (so they are never overwritten).</summary>
        private static bool[] ReferencedCharIndices(ScummV1Room room, bool includeBackground, int excludeObject)
        {
            var used = new bool[256];
            if (includeBackground)
            {
                int w = room.WidthInChars, h = room.HeightInChars;
                byte[] picMap = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.PicMapOffset, w * h);
                if (picMap != null)
                    for (int k = 0; k < picMap.Length; k++) used[picMap[k]] = true;
            }
            for (int i = 0; i < room.NumObjects; i++)
            {
                if (i == excludeObject) continue;
                int obim = room.ObjectImageOffset(i);
                int wpx = room.ObjectWidth(i), hpx = room.ObjectHeight(i);
                if (obim <= 0 || wpx <= 0 || hpx <= 0) continue;
                if (room.ObjectCodeOffset(i) == obim) continue; // imageless object (OBIM points at its OBCD)
                int ow = wpx / 8, oh = hpx / 8;
                byte[] map = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, obim, ow * oh * 3);
                if (map == null) continue;
                for (int k = 0; k < ow * oh; k++) used[map[k]] = true; // plane 0 (tile) only
            }
            return used;
        }

        // --- room loading / assembly ------------------------------------------

        private static byte[] LoadCharMap(ScummV1Room room)
        {
            byte[] charMap = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, room.CharMapOffset, 2048);
            return charMap ?? new byte[2048];
        }

        private static List<byte> LoadMaskChar(ScummV1Room room)
        {
            int maskPtr = room.MaskDataOffset;
            if (maskPtr > 0 && maskPtr + 2 <= room.Data.Length)
            {
                int storedLen = room.Data[maskPtr] | (room.Data[maskPtr + 1] << 8);
                int maskCharLen = storedLen - 8; // the stored length word is always 8 too big (ScummVM bug #3458)
                if (maskCharLen > 0)
                {
                    byte[] mc = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, maskPtr + 2, maskCharLen);
                    if (mc != null) return new List<byte>(mc);
                }
            }
            return new List<byte>();
        }

        private static byte[] LoadObjectMap(ScummV1Room room, int objectIndex, out int obim, out int w, out int h, out string error)
        {
            error = null; obim = 0; w = 0; h = 0;
            obim = room.ObjectImageOffset(objectIndex);
            int wpx = room.ObjectWidth(objectIndex), hpx = room.ObjectHeight(objectIndex);
            if (obim <= 0 || obim >= room.Data.Length || wpx <= 0 || hpx <= 0) { error = "This object has no image."; return null; }
            // An imageless object leaves its OBIM pointing at SOME object's code (OBCD) block; re-encoding it
            // would splice a garbage image over that code. Reject exactly what the decoder rejects (it guards
            // against aliasing ANY object's OBCD, not just this object's own).
            for (int k = 0; k < room.NumObjects; k++)
                if (room.ObjectCodeOffset(k) == obim) { error = "This object has no image."; return null; }
            w = wpx / 8; h = hpx / 8;
            byte[] map = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, obim, w * h * 3);
            if (map == null) { error = "This object's image could not be decoded."; return null; }
            return map;
        }

        private static Dictionary<string, int> BuildTileLookup(byte[] charMap)
        {
            var map = new Dictionary<string, int>();
            var tile = new byte[8];
            for (int i = 0; i < 256; i++)
            {
                Array.Copy(charMap, i * 8, tile, 0, 8);
                string key = Convert.ToBase64String(tile);
                if (!map.ContainsKey(key)) map[key] = i; // first (lowest) slot wins
            }
            return map;
        }

        private static Dictionary<string, int> BuildMaskLookup(List<byte> maskChar)
        {
            var map = new Dictionary<string, int>();
            var tile = new byte[8];
            for (int i = 0; i * 8 + 8 <= maskChar.Count; i++)
            {
                for (int k = 0; k < 8; k++) tile[k] = maskChar[i * 8 + k];
                string key = Convert.ToBase64String(tile);
                if (!map.ContainsKey(key)) map[key] = i;
            }
            return map;
        }

        /// <summary>The maskData block: a u16 length (the decoded maskChar length + 8 - ScummVM bug #3458) then the RLE.</summary>
        private static byte[] BuildMaskData(byte[] maskChar)
        {
            byte[] rle = CompressV1Gfx(maskChar);
            var outp = new byte[2 + rle.Length];
            int stored = maskChar.Length + 8;
            outp[0] = (byte)(stored & 0xFF);
            outp[1] = (byte)((stored >> 8) & 0xFF);
            Array.Copy(rle, 0, outp, 2, rle.Length);
            return outp;
        }

        // --- compact room assembly (the real-engine-safe write-back) -----------

        /// <summary>
        /// Rebuilds the WHOLE room resource COMPACTLY: it re-encodes the (possibly edited) maps and object
        /// images with real RLE compression and re-lays every offset-referenced region back to back, then
        /// re-points every offset field to its region's new position. Unlike the original write-back it appends
        /// NOTHING and leaves NO dead bytes, so an edit grows the room by only the few bytes the new content
        /// needs - which the real v1 engine (the DOS interpreter + ScummVM) requires; the bloated append-at-end
        /// form black-screened the game.
        ///
        /// A v1 room is a fixed header (with the OBIM/OBCD offset tables + sound/script id lists) followed by
        /// offset-referenced regions: the box, the 5 maps, every object's image (OBIM) and code (OBCD), and the
        /// exit/entry scripts. Sorted + de-duplicated, those offsets tile the room after the header; each region
        /// runs to the next offset (shared offsets - multi-state objects, or an imageless object whose OBIM
        /// aliases an OBCD - collapse to one region kept verbatim). <paramref name="objEdits"/> maps an object
        /// index to its new 3-plane image bytes (null/empty for a background or mask edit). Returns null with an
        /// error if the layout is not the expected header-then-regions form. The caller splices the result with
        /// ScummV2Writer.ReplaceRoomResource.
        /// </summary>
        private byte[] AssembleCompactRoom(ScummV1Room room, byte[] charMap, byte[] picMap, byte[] colorMap,
            byte[] maskMap, byte[] maskChar, Dictionary<int, byte[]> objEdits, out string error)
        {
            error = null;
            int roomSize = RoomSize(room);
            int charOff0 = room.CharMapOffset, picOff0 = room.PicMapOffset, colorOff0 = room.ColorMapOffset;
            int maskMapOff0 = room.MaskMapOffset, maskDataOff0 = room.MaskDataOffset;
            // Every map offset must be a real in-room position (> 0 and < roomSize). Besides catching a
            // non-standard layout, this guarantees each map offset is added as a region below, so the
            // direct newOffsetOf[...] re-pointing at the end cannot miss a key and throw.
            if (charOff0 <= 0 || picOff0 <= 0 || colorOff0 <= 0 || maskMapOff0 <= 0 || maskDataOff0 <= 0 ||
                charOff0 >= roomSize || picOff0 >= roomSize || colorOff0 >= roomSize ||
                maskMapOff0 >= roomSize || maskDataOff0 >= roomSize)
            { error = "This room's map layout is non-standard; its image cannot be re-imported safely."; return null; }

            // The new bytes for every region we re-encode, keyed by the region's ORIGINAL file offset.
            var newBytesByOffset = new Dictionary<int, byte[]>();
            newBytesByOffset[charOff0] = CompressV1Gfx(charMap);
            newBytesByOffset[picOff0] = CompressV1Gfx(picMap);
            newBytesByOffset[colorOff0] = CompressV1Gfx(colorMap);
            newBytesByOffset[maskMapOff0] = CompressV1Gfx(maskMap);
            newBytesByOffset[maskDataOff0] = BuildMaskData(maskChar);
            if (objEdits != null && objEdits.Count > 0)
            {
                // Several v1 objects can point at ONE image stream but read it with their own dimensions (e.g.
                // Zak room 5 objects 0/5/6/11 all start at the same OBIM, reading 12/9/36/192 bytes of the same
                // RLE). So an edit must re-encode the FULL stream (long enough for the largest reader) with each
                // edited object's bytes overlaid on its prefix - re-encoding only the editing object's bytes
                // would truncate the stream and corrupt the longer-reading sharers. A no-op leaves it identical.
                var editsByOffset = new Dictionary<int, List<int>>();
                foreach (KeyValuePair<int, byte[]> kv in objEdits)
                {
                    int objImgOff = room.ObjectImageOffset(kv.Key);
                    if (objImgOff <= 0) { error = "The edited object has no image block to replace."; return null; }
                    if (!editsByOffset.ContainsKey(objImgOff)) editsByOffset[objImgOff] = new List<int>();
                    editsByOffset[objImgOff].Add(kv.Key);
                }
                foreach (KeyValuePair<int, List<int>> grp in editsByOffset)
                {
                    int off = grp.Key;
                    int maxLen = 0;
                    for (int i = 0; i < room.NumObjects; i++)
                        if (room.ObjectImageOffset(i) == off)
                        {
                            int len = (room.ObjectWidth(i) / 8) * (room.ObjectHeight(i) / 8) * 3;
                            if (len > maxLen) maxLen = len;
                        }
                    if (maxLen <= 0) { error = "The edited object has no decodable image block."; return null; }
                    // Decode the WHOLE shared stream (long enough for the largest reader). If it cannot be
                    // decoded, refuse - zero-filling would silently wipe the image data the other sharers read.
                    byte[] full = ScummV1ImageDecoder.DecodeV1Gfx(room.Data, off, maxLen);
                    if (full == null) { error = "This object's shared image stream could not be decoded; its image cannot be re-imported safely."; return null; }
                    foreach (int objIdx in grp.Value)
                    {
                        byte[] em = objEdits[objIdx];
                        Array.Copy(em, 0, full, 0, Math.Min(em.Length, full.Length));
                    }
                    newBytesByOffset[off] = CompressV1Gfx(full);
                }
            }

            // Collect every offset-referenced region. Sorted + de-duplicated, these tile [headerEnd, roomSize).
            int numObj = room.NumObjects;
            int boxOff = room.Data.Length > 0x15 ? room.Data[0x15] : 0; // the box is a 1-byte offset in v1/v2
            var offsets = new SortedSet<int>();
            AddRegionOffset(offsets, boxOff, roomSize);
            AddRegionOffset(offsets, charOff0, roomSize); AddRegionOffset(offsets, picOff0, roomSize);
            AddRegionOffset(offsets, colorOff0, roomSize); AddRegionOffset(offsets, maskMapOff0, roomSize);
            AddRegionOffset(offsets, maskDataOff0, roomSize);
            AddRegionOffset(offsets, room.ExitScriptOffset, roomSize);
            AddRegionOffset(offsets, room.EntryScriptOffset, roomSize);
            for (int i = 0; i < numObj; i++)
            {
                AddRegionOffset(offsets, room.ObjectImageOffset(i), roomSize);
                AddRegionOffset(offsets, room.ObjectCodeOffset(i), roomSize);
            }
            if (offsets.Count == 0) { error = "This room has no relocatable regions; its image cannot be re-imported safely."; return null; }

            var sorted = new List<int>(offsets);
            int headerEnd = sorted[0]; // the fixed header + OBIM/OBCD tables + sound/script id lists live in [0, headerEnd)
            // The header runs: fixed fields, the OBIM+OBCD tables (numObj*2 each), then the 1-byte sound and
            // script id lists (numSounds + numScripts). The first region MUST start at or after all of that,
            // or the verbatim header copy would truncate the id lists and the engine would read garbage ids.
            if (headerEnd < 28 + numObj * 4 + room.NumSounds + room.NumScripts)
            { error = "This room's header/region layout is non-standard; its image cannot be re-imported safely."; return null; }

            // Lay the room out: header verbatim, then each region (re-encoded where edited, else verbatim) in
            // offset order, recording each original offset's NEW position.
            var outp = new List<byte>(roomSize + 1024);
            for (int i = 0; i < headerEnd; i++) outp.Add(room.Data[i]);
            var newOffsetOf = new Dictionary<int, int>();
            for (int s = 0; s < sorted.Count; s++)
            {
                int off = sorted[s];
                newOffsetOf[off] = outp.Count;
                byte[] bytes;
                if (newBytesByOffset.TryGetValue(off, out bytes)) outp.AddRange(bytes);
                else
                {
                    int end = (s + 1 < sorted.Count) ? sorted[s + 1] : roomSize;
                    if (end < off) { error = "This room's region boundaries are inconsistent; its image cannot be re-imported safely."; return null; }
                    for (int b = off; b < end; b++) outp.Add(room.Data[b]);
                }
            }

            byte[] nr = outp.ToArray();
            if (nr.Length > 0xFFFF) { error = "The re-encoded room exceeds the 64 KB room-resource limit."; return null; }

            // Re-point every offset FIELD to its region's new position (an absent field, value 0, stays 0).
            WriteU16Buf(nr, 10, newOffsetOf[charOff0]); WriteU16Buf(nr, 12, newOffsetOf[picOff0]);
            WriteU16Buf(nr, 14, newOffsetOf[colorOff0]); WriteU16Buf(nr, 16, newOffsetOf[maskMapOff0]);
            WriteU16Buf(nr, 18, newOffsetOf[maskDataOff0]);
            if (boxOff > 0)
            {
                int nb;
                if (!newOffsetOf.TryGetValue(boxOff, out nb))
                { error = "This room's box offset is out of range; its image cannot be re-imported safely."; return null; }
                if (nb > 0xFF) { error = "The re-encoded room pushes the 1-byte box offset out of range."; return null; }
                nr[0x15] = (byte)nb;
            }
            SetFieldU16(nr, 0x18, room.ExitScriptOffset, newOffsetOf);  // EXCD
            SetFieldU16(nr, 0x1A, room.EntryScriptOffset, newOffsetOf); // ENCD
            for (int i = 0; i < numObj; i++)
            {
                SetFieldU16(nr, 28 + i * 2, room.ObjectImageOffset(i), newOffsetOf);            // OBIM[i]
                SetFieldU16(nr, 28 + numObj * 2 + i * 2, room.ObjectCodeOffset(i), newOffsetOf); // OBCD[i]
            }
            WriteU16Buf(nr, 0, nr.Length); // room size word
            return nr;
        }

        private static void AddRegionOffset(SortedSet<int> offsets, int off, int roomSize)
        {
            if (off > 0 && off < roomSize) offsets.Add(off);
        }

        private static void SetFieldU16(byte[] buf, int fieldPos, int origValue, Dictionary<int, int> newOffsetOf)
        {
            if (fieldPos + 1 >= buf.Length || origValue <= 0) return;
            int nv;
            if (newOffsetOf.TryGetValue(origValue, out nv)) WriteU16Buf(buf, fieldPos, nv);
        }

        private static void WriteU16Buf(byte[] buf, int pos, int value)
        {
            buf[pos] = (byte)(value & 0xFF);
            buf[pos + 1] = (byte)((value >> 8) & 0xFF);
        }

        /// <summary>
        /// Compresses a buffer into the decodeV1Gfx RLE the engine reads: a 4-byte common-colour header
        /// (the 4 most frequent bytes), then runs - 0x80|(idx&lt;&lt;5)|(n-1) for up to 32 of a common colour,
        /// 0x40|(n-1) + colour for up to 64 of any single colour, and a raw copy (n-1)+bytes for up to 64
        /// varied bytes. Far smaller than the raw-only encoding (it collapses the long repeated runs in the
        /// tile/colour maps), so an edited room stays about its original size. Decodes back identically.
        /// </summary>
        private static byte[] CompressV1Gfx(byte[] data)
        {
            var outp = new List<byte>(data.Length / 2 + 8);
            var freq = new int[256];
            for (int i = 0; i < data.Length; i++) freq[data[i]]++;
            var common = new byte[4];
            var commonIndex = new Dictionary<byte, int>();
            for (int k = 0; k < 4; k++)
            {
                int best = -1, bi = 0;
                for (int b = 0; b < 256; b++) if (freq[b] > best) { best = freq[b]; bi = b; }
                common[k] = (byte)bi;
                freq[bi] = -1;
                if (!commonIndex.ContainsKey((byte)bi)) commonIndex[(byte)bi] = k;
            }
            outp.Add(common[0]); outp.Add(common[1]); outp.Add(common[2]); outp.Add(common[3]);

            var raw = new List<byte>();
            int p = 0;
            while (p < data.Length)
            {
                byte b = data[p];
                int run = 1;
                while (p + run < data.Length && data[p + run] == b) run++;

                int ci;
                if (commonIndex.TryGetValue(b, out ci))
                {
                    FlushRaw(outp, raw);
                    int rem = run;
                    while (rem > 0) { int c = Math.Min(32, rem); outp.Add((byte)(0x80 | (ci << 5) | (c - 1))); rem -= c; }
                    p += run;
                }
                else if (run >= 3)
                {
                    FlushRaw(outp, raw);
                    int rem = run;
                    while (rem > 0) { int c = Math.Min(64, rem); outp.Add((byte)(0x40 | (c - 1))); outp.Add(b); rem -= c; }
                    p += run;
                }
                else
                {
                    raw.Add(b);
                    p++;
                    if (raw.Count >= 64) FlushRaw(outp, raw);
                }
            }
            FlushRaw(outp, raw);
            return outp.ToArray();
        }

        private static void FlushRaw(List<byte> outp, List<byte> raw)
        {
            int i = 0;
            while (i < raw.Count)
            {
                int c = Math.Min(64, raw.Count - i);
                outp.Add((byte)(c - 1)); // raw-copy run (top two bits 0)
                for (int j = 0; j < c; j++) outp.Add(raw[i + j]);
                i += c;
            }
            raw.Clear();
        }

        private static int RoomSize(ScummV1Room room)
        {
            int roomSize = room.Data.Length >= 2 ? (room.Data[0] | (room.Data[1] << 8)) : room.Data.Length;
            if (roomSize <= 0 || roomSize > room.Data.Length) roomSize = room.Data.Length;
            return roomSize;
        }

    }
}
