using System;
using System.Collections.Generic;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Re-encodes an edited v1 (GdiV1 tilemap) room background into a rebuilt room resource. It quantizes the
    /// edited pixels back into a charMap (&lt;= 256 distinct 8x8 tiles) plus a per-cell picMap (tile index) and
    /// colorMap (the cell's 4th colour), then APPENDS the three re-encoded maps to the room resource and
    /// re-points the room header's map offsets (+10/+12/+14); the original maps are left as dead bytes and the
    /// walk-behind mask (maskMap/maskData) is preserved untouched.
    ///
    /// LOSSY BY FORMAT: each 8x8 cell can show only 4 colours (3 fixed room colours + 1 free per cell) and the
    /// charMap holds at most 256 distinct tiles, so an edit that exceeds those limits is REJECTED with an error
    /// rather than silently corrupted. Re-encoding an UNEDITED decoded background is pixel-lossless (it satisfies
    /// the constraints by construction). Mirrors the inverse of ScummV1ImageDecoder.
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

        public ScummV1ImageEncoder(bool isManiac)
        {
            _renderMap = isManiac ? ManiacEgaColorMap : ZakEgaColorMap;
            _inverseLow8 = new int[16];
            for (int i = 0; i < 16; i++) _inverseLow8[i] = -1;
            for (int c = 0; c < 8; c++) _inverseLow8[_renderMap[c]] = c; // the first 8 render-map entries are distinct
        }

        /// <summary>
        /// Rebuilds the room RESOURCE bytes with the background re-encoded from <paramref name="matrix"/> (a
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

            int r0 = _renderMap[room.Color(0) & 0x0F];
            int r1 = _renderMap[room.Color(1) & 0x0F];
            int r2 = _renderMap[room.Color(2) & 0x0F];

            var charMap = new byte[2048];
            var picMap = new byte[w * h];
            var colorMap = new byte[w * h];
            var tileIndex = new Dictionary<string, int>();
            int nextTile = 0;

            for (int strip = 0; strip < w; strip++)
            {
                for (int y = 0; y < h; y++)
                {
                    int cell = y + strip * h; // column-major (matches the decoder)
                    var tile = new byte[8];
                    int c3 = -1;
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
                                    return null;
                                }
                                if (c3 < 0) c3 = c; // a cell holds only one free colour; the first one wins
                                v = 3;
                            }
                            b |= v << (6 - p * 2); // p0 = leftmost pair -> bits 6-7 (matches the decoder's (c>>6))
                        }
                        tile[i] = (byte)b;
                    }

                    string key = Convert.ToBase64String(tile);
                    int idx;
                    if (!tileIndex.TryGetValue(key, out idx))
                    {
                        if (nextTile >= 256)
                        {
                            error = "The edited background needs more than 256 distinct 8x8 tiles, which v1 cannot store.";
                            return null;
                        }
                        idx = nextTile++;
                        Array.Copy(tile, 0, charMap, idx * 8, 8);
                        tileIndex[key] = idx;
                    }
                    picMap[cell] = (byte)idx;
                    colorMap[cell] = (byte)(c3 < 0 ? 0 : c3);
                }
            }

            // The room resource is [0, roomSize); other resources may be packed after it in the NN.LFL.
            int roomSize = room.Data.Length >= 2 ? (room.Data[0] | (room.Data[1] << 8)) : room.Data.Length;
            if (roomSize <= 0 || roomSize > room.Data.Length) roomSize = room.Data.Length;

            var outp = new List<byte>(roomSize + 4096);
            for (int i = 0; i < roomSize; i++) outp.Add(room.Data[i]);
            int charOff = outp.Count; outp.AddRange(RawEncode(charMap));
            int picOff = outp.Count; outp.AddRange(RawEncode(picMap));
            int colorOff = outp.Count; outp.AddRange(RawEncode(colorMap));
            byte[] rebuilt = outp.ToArray();

            if (rebuilt.Length > 0xFFFF) { error = "The re-encoded room exceeds the 64 KB room-resource limit."; return null; }

            WriteU16(rebuilt, 10, charOff);   // charMap offset
            WriteU16(rebuilt, 12, picOff);    // picMap offset
            WriteU16(rebuilt, 14, colorOff);  // colorMap offset (+16 maskMap / +18 maskData are preserved)
            WriteU16(rebuilt, 0, rebuilt.Length); // the room resource size word
            return rebuilt;
        }

        /// <summary>
        /// Encodes a byte buffer with the decodeV1Gfx RLE using only raw-copy runs (a 4-byte common-colour
        /// header then [count-1][count bytes] chunks of up to 64) - always valid, decodes back identically.
        /// </summary>
        private static byte[] RawEncode(byte[] data)
        {
            var outp = new List<byte> { 0, 0, 0, 0 }; // 4 common-colour bytes (unused; only raw runs are emitted)
            int i = 0;
            while (i < data.Length)
            {
                int n = Math.Min(64, data.Length - i);
                outp.Add((byte)(n - 1)); // raw-copy run: top two bits 0, count = (run & 0x3F) + 1
                for (int k = 0; k < n; k++) outp.Add(data[i + k]);
                i += n;
            }
            return outp.ToArray();
        }

        private static void WriteU16(byte[] data, int pos, int value)
        {
            data[pos] = (byte)(value & 0xFF);
            data[pos + 1] = (byte)((value >> 8) & 0xFF);
        }
    }
}
