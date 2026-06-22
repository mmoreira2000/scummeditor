using System;
using System.Drawing;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Imports ONE edited PNG back into a v2 / v3-old room image (background, object image or walk-behind
    /// z-plane), the per-node counterpart of the batch ScummV2Graphics / ScummV3OldGraphics import. It
    /// re-encodes with the same encoders the batch path uses and splices the result with ScummV2Writer /
    /// ScummV3OldWriter.ApplyEdit (which re-points every offset the size change shifts), so a per-node
    /// import is byte-for-byte equivalent to the batch one. The data file's RawContent is updated in
    /// memory; the caller persists it with the normal "Save changes" pipeline.
    ///
    /// Returns false (with a message) instead of throwing, so the GUI can report it: wrong size / non-
    /// indexed PNG, a missing region, or a v2 edit that grows past a 1-byte offset's range.
    /// </summary>
    public static class OldBundleImageImporter
    {
        public static bool Import(ScummV3OldBundleDataFile dataFile, ScummV3OldBundleIndexFile index, int roomNo,
            bool isV2, OldBundleImageKind kind, int objectIndex, Bitmap png, out string error)
        {
            error = null;
            if (dataFile == null || dataFile.RawContent == null) { error = "The room has no data to import into."; return false; }
            if (png == null) { error = "No image was loaded."; return false; }

            try
            {
                // v1 (Maniac/Zak classic, GdiV1 tilemap) shares the isV2=true old-bundle container but needs
                // its own re-encoder; detect it from the data file's version.
                if (dataFile.GameInfo != null && dataFile.GameInfo.ScummVersion == 1)
                    return ImportV1(dataFile, index, roomNo, kind, objectIndex, png, out error);
                return isV2
                    ? ImportV2(dataFile, index, roomNo, kind, objectIndex, png, out error)
                    : ImportV3(dataFile, index, roomNo, kind, objectIndex, png, out error);
            }
            catch (Exception ex) { error = ex.Message; return false; } // incl. ImageEncodeException (wrong size / non-indexed / 1-byte-offset overflow)
        }

        // --- v3 old-bundle (Loom / Indy3 EGA) ---------------------------------

        private static bool ImportV3(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo,
            OldBundleImageKind kind, int objectIndex, Bitmap png, out string error)
        {
            error = null;
            var room = new ScummV3OldRoom(df.RawContent);
            byte[] data = df.RawContent;

            switch (kind)
            {
                case OldBundleImageKind.Background:
                {
                    int off = room.ImageOffset;
                    if (off <= 0 || room.Width <= 0 || room.Height <= 0) { error = "This room has no background image."; return false; }
                    int oldLen = ReadU16(data, off);
                    byte[] newTable = ScummV3OldImageEncoder.Encode(data, off, room.Width, room.Height, png); // checks size + indexed
                    ScummV3OldWriter.ApplyEdit(df, index, roomNo, off, oldLen, newTable, -1);
                    return true;
                }
                case OldBundleImageKind.Object:
                {
                    int obim = room.ObjectImageOffset(objectIndex);
                    int w = room.ObjectWidth(objectIndex), h = room.ObjectHeight(objectIndex);
                    if (obim <= 0 || w <= 0 || h <= 0) { error = "This object has no image."; return false; }
                    int oldLen = ReadU16(data, obim);
                    byte[] newTable = ScummV3OldImageEncoder.Encode(data, obim, w, h, png);
                    ScummV3OldWriter.ApplyEdit(df, index, roomNo, obim, oldLen, newTable, -1);
                    return true;
                }
                case OldBundleImageKind.BackgroundZPlane:
                    return ImportV3ZPlane(df, index, roomNo, room, room.ImageOffset, room.Width, room.Height, png, out error);
                case OldBundleImageKind.ObjectZPlane:
                {
                    int obim = room.ObjectImageOffset(objectIndex);
                    int w = room.ObjectWidth(objectIndex), h = room.ObjectHeight(objectIndex);
                    return ImportV3ZPlane(df, index, roomNo, room, obim, w, h, png, out error);
                }
                default:
                    error = "Unsupported image kind."; return false;
            }
        }

        private static bool ImportV3ZPlane(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo,
            ScummV3OldRoom room, int imageOffset, int width, int height, Bitmap png, out string error)
        {
            error = null;
            if (imageOffset <= 0 || width <= 0 || height <= 0) { error = "There is no z-plane region to import into."; return false; }
            if (!SizeMatches(png, width, height, out error)) return false;

            int smapLen = ReadU16(df.RawContent, imageOffset);
            int zbase = imageOffset + smapLen;
            int regionEnd = room.NextStructuralOffsetAbove(imageOffset);
            int oldLen = regionEnd - zbase;
            if (oldLen <= 0) { error = "There is no z-plane region to import into."; return false; }

            byte[] newRegion = ScummV3OldZPlaneEncoder.Encode(width, height, png);
            ScummV3OldWriter.ApplyEdit(df, index, roomNo, zbase, oldLen, newRegion, -1);
            return true;
        }

        // --- v2 (Maniac / Zak) ------------------------------------------------

        private static bool ImportV2(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo,
            OldBundleImageKind kind, int objectIndex, Bitmap png, out string error)
        {
            error = null;
            var room = new ScummV2Room(df.RawContent);
            byte[] data = df.RawContent;

            switch (kind)
            {
                case OldBundleImageKind.Background:
                {
                    int off = room.ImageOffset;
                    if (off <= 0 || room.Width <= 0 || room.Height <= 0) { error = "This room has no background image."; return false; }
                    if (!SizeMatches(png, room.Width, room.Height, out error)) return false;
                    if (!Indexed(png, out error)) return false;
                    int imageEnd = room.NextStructuralOffsetAbove(off);
                    byte[,] matrix = IndexedImageHelper.GetIndexMatrix(png);
                    byte[] newRegion = ScummV2ImageEncoder.EncodeImage(data, off, imageEnd, room.Width, room.Height, matrix);
                    ScummV2Writer.ApplyEdit(df, index, roomNo, off, imageEnd - off, newRegion, -1);
                    return true;
                }
                case OldBundleImageKind.Object:
                {
                    if (!ScummV2ImageDecoder.ObjectOwnsImage(room, objectIndex)) { error = "This object does not own its image and cannot be imported safely."; return false; }
                    int obim = room.ObjectImageOffset(objectIndex);
                    int w = room.ObjectWidth(objectIndex), h = room.ObjectHeight(objectIndex);
                    if (!SizeMatches(png, w, h, out error)) return false;
                    if (!Indexed(png, out error)) return false;
                    int objEnd = room.NextStructuralOffsetAbove(obim);
                    byte[,] matrix = IndexedImageHelper.GetIndexMatrix(png);
                    byte[] newRegion = ScummV2ImageEncoder.EncodeImage(data, obim, objEnd, w, h, matrix);
                    ScummV2Writer.ApplyEdit(df, index, roomNo, obim, objEnd - obim, newRegion, -1);
                    return true;
                }
                case OldBundleImageKind.BackgroundZPlane:
                {
                    int off = room.ImageOffset;
                    if (off <= 0 || room.Width <= 0 || room.Height <= 0) { error = "This room has no background to mask."; return false; }
                    if (!SizeMatches(png, room.Width, room.Height, out error)) return false;
                    // A mask is intrinsically 1-bit B/W: MaskMatrixFromBitmap thresholds any bitmap by luminance,
                    // so a B/W PNG (indexed OR truecolor) is accepted - no indexed requirement here (unlike images,
                    // whose raw palette indexes only exist on an indexed bitmap).
                    // v2 keeps the graphics and the mask in ONE region; confirm a mask region exists.
                    int gfxLen = ScummV2ImageDecoder.GraphicsRleLength(data, off, room.Width, room.Height);
                    int maskStart = off + gfxLen;
                    int imageEnd = room.NextStructuralOffsetAbove(off);
                    if (maskStart >= imageEnd || maskStart >= data.Length) { error = "This room has no walk-behind mask to import into."; return false; }
                    byte[,] maskMatrix = MaskMatrixFromBitmap(png);
                    byte[] newRegion = ScummV2ImageEncoder.EncodeImageWithMask(data, off, room.Width, room.Height, maskMatrix);
                    ScummV2Writer.ApplyEdit(df, index, roomNo, off, imageEnd - off, newRegion, -1);
                    return true;
                }
                case OldBundleImageKind.ObjectZPlane:
                {
                    if (!ScummV2ImageDecoder.ObjectOwnsImage(room, objectIndex)) { error = "This object does not own its image and cannot be masked safely."; return false; }
                    int obim = room.ObjectImageOffset(objectIndex);
                    int w = room.ObjectWidth(objectIndex), h = room.ObjectHeight(objectIndex);
                    if (!SizeMatches(png, w, h, out error)) return false;
                    // The object's walk-behind mask follows its graphics in the OBIM (same layout as IM00).
                    int gfxLen = ScummV2ImageDecoder.GraphicsRleLength(data, obim, w, h);
                    int maskStart = obim + gfxLen;
                    int objEnd = room.NextStructuralOffsetAbove(obim);
                    if (maskStart >= objEnd || maskStart >= data.Length) { error = "This object has no walk-behind mask to import into."; return false; }
                    byte[,] maskMatrix = MaskMatrixFromBitmap(png);
                    byte[] newRegion = ScummV2ImageEncoder.EncodeImageWithMask(data, obim, w, h, maskMatrix);
                    ScummV2Writer.ApplyEdit(df, index, roomNo, obim, objEnd - obim, newRegion, -1);
                    return true;
                }
                default:
                    error = "Unsupported image kind."; return false;
            }
        }

        // --- v1 (Maniac / Zak classic, GdiV1 tilemap) -------------------------

        private static bool ImportV1(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo,
            OldBundleImageKind kind, int objectIndex, Bitmap png, out string error)
        {
            error = null;
            var room = new ScummV1Room(df.RawContent);
            bool isManiac = df.GameInfo != null && df.GameInfo.LoadedGame == ScummGame.ManiacMansion;
            var enc = new ScummV1ImageEncoder(isManiac);
            byte[] newRoom;

            // The v1 encoder rebuilds the WHOLE room COMPACTLY (the 5 maps re-encoded in place, compressed) so
            // the result stays about its original size - the real v1 engine (the DOS interpreter and ScummVM)
            // cannot run a room that grew. Only the room's 5-map block changes (background pixels or the
            // walk-behind mask), so background + background z-plane go through this; object-image / object
            // z-plane import is temporarily disabled (its OBIM lives after the map block and needs the same
            // compact rewrite - export still works).
            switch (kind)
            {
                case OldBundleImageKind.Background:
                    if (room.WidthInChars <= 0 || room.HeightInChars <= 0) { error = "This room has no background image."; return false; }
                    if (!SizeMatches(png, room.Width, room.Height, out error)) return false;
                    if (!Indexed(png, out error)) return false;
                    newRoom = enc.EncodeBackground(room, IndexedImageHelper.GetIndexMatrix(png), out error);
                    break;
                case OldBundleImageKind.BackgroundZPlane:
                    if (room.WidthInChars <= 0 || room.HeightInChars <= 0) { error = "This room has no background to mask."; return false; }
                    if (!SizeMatches(png, room.Width, room.Height, out error)) return false;
                    newRoom = enc.EncodeBackgroundZPlane(room, MaskMatrixFromBitmap(png), out error);
                    break;
                case OldBundleImageKind.Object:
                case OldBundleImageKind.ObjectZPlane:
                    error = "v1 object-image / object z-plane import is export-only for now (the compact write-back is being finished); the room background and its walk-behind mask import normally.";
                    return false;
                default:
                    error = "Unsupported image kind."; return false;
            }
            if (newRoom == null) return false; // over-limit / unrepresentable / non-standard layout, already reported

            // newRoom is a fully self-consistent, compact room resource; splice it in for the old one and
            // relocate the costume / script / sound sub-resources packed after it in the NN.LFL.
            ScummV2Writer.ReplaceRoomResource(df, index, roomNo, newRoom);
            return true;
        }

        // --- helpers ----------------------------------------------------------

        private static bool SizeMatches(Bitmap png, int width, int height, out string error)
        {
            if (png.Width == width && png.Height == height) { error = null; return true; }
            error = string.Format("The image must be {0}x{1} (the original size), but it is {2}x{3}.",
                width, height, png.Width, png.Height);
            return false;
        }

        private static bool Indexed(Bitmap png, out string error)
        {
            if (IndexedImageHelper.IsIndexed(png)) { error = null; return true; }
            error = "The image must be an indexed (palette-based) PNG so the original colour indexes are preserved. "
                  + "Re-export it from ScummEditor and edit it without converting it to RGB.";
            return false;
        }

        /// <summary>White (a set bit) = masked, matching the z-plane export (index 1 = white).</summary>
        private static byte[,] MaskMatrixFromBitmap(Bitmap bmp)
        {
            var m = new byte[bmp.Width, bmp.Height];
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    m[x, y] = (byte)(bmp.GetPixel(x, y).R > 127 ? 1 : 0);
            return m;
        }

        private static int ReadU16(byte[] data, int p)
        {
            if (p < 0 || p + 1 >= data.Length) return 0;
            return data[p] | (data[p + 1] << 8);
        }
    }
}
