using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;
using ScummEditor.Engine.Structures.IndexFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Batch graphics export for SCUMM v2 games (Maniac Mansion, Zak McKracken): room backgrounds and
    /// object images via the GdiV2 codec (ScummV2ImageDecoder), and costume frames - which are the classic
    /// format 0x58, byte-identical to v3old, so they reuse CostumeV3Old + CostumeImageDecoderV4 + the fixed
    /// 16-colour EGA palette. Mirrors ScummV3OldGraphics.
    /// </summary>
    public static class ScummV2Graphics
    {
        public static int Export(ScummGameData game, string folder, ScummV4GraphicsBatch.ExportOptions options,
            Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            var decoder = new ScummV2ImageDecoder();
            var rooms = RoomsByNumber(game);
            var roomNumbers = new List<int>(rooms.Keys);
            roomNumbers.Sort();
            int count = 0;

            for (int idx = 0; idx < roomNumbers.Count; idx++)
            {
                if (shouldCancel != null && shouldCancel()) break;
                int roomNo = roomNumbers[idx];
                var room = new ScummV2Room(rooms[roomNo].RawContent);
                if (room.Width <= 0 || room.Height <= 0) { if (onProgress != null) onProgress(idx + 1, roomNumbers.Count); continue; }

                if (options.Backgrounds)
                {
                    using (Bitmap background = decoder.DecodeBackground(room))
                        if (background != null) { Save(background, folder, string.Format("Room#{0}.png", roomNo)); count++; }
                }
                if (options.BackgroundZPlanes)
                {
                    using (Bitmap zplane = decoder.DecodeBackgroundZPlane(room))
                        if (zplane != null) { Save(zplane, folder, string.Format("Room#{0} ZPlane#0.png", roomNo)); count++; }
                }
                if (options.Objects)
                {
                    for (int j = 0; j < room.NumObjects; j++)
                    {
                        using (Bitmap obj = decoder.DecodeObject(room, j))
                            if (obj != null) { Save(obj, folder, string.Format("Room#{0} Obj#{1} Img#0.png", roomNo, j)); count++; }
                    }
                }
                if (onProgress != null) onProgress(idx + 1, roomNumbers.Count);
            }

            if (options.Costumes) count += ExportCostumes(game, folder);
            return count;
        }

        /// <summary>Exports every costume frame (format 0x58 = classic, same as v3old), via the index COSTUME directory.</summary>
        private static int ExportCostumes(ScummGameData game, string folder)
        {
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            if (index == null || index.CostumeDirectory == null) return 0;

            var byRoom = RoomsByNumber(game);
            var costumeDecoder = new CostumeImageDecoderV4();
            var ega = new Color[16];
            Array.Copy(EgaColorTable.Colors256, ega, 16);

            int count = 0;
            V3OldResourceDirectory dir = index.CostumeDirectory;
            for (int c = 0; c < dir.Count; c++)
            {
                int offset = dir.Offsets[c];
                if (offset == 0xFFFF || offset == 0) continue;
                ScummV3OldBundleDataFile df;
                if (!byRoom.TryGetValue(dir.RoomNumbers[c], out df)) continue;

                CostumeV3Old costume;
                try { costume = new CostumeV3Old(df.RawContent, offset); }
                catch { continue; }
                for (int k = 0; k < costume.Frames.Count; k++)
                {
                    Bitmap frame;
                    try { frame = costumeDecoder.Decode(costume.Frames[k], 16, ega, false); }
                    catch { continue; }
                    if (frame != null)
                    {
                        using (frame) { Save(frame, folder, string.Format("Costume#{0} FrameIndex#{1}.png", c, k)); count++; }
                    }
                }
            }
            return count;
        }

        private static Dictionary<int, ScummV3OldBundleDataFile> RoomsByNumber(ScummGameData game)
        {
            var map = new Dictionary<int, ScummV3OldBundleDataFile>();
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                int n;
                if (df != null && int.TryParse(Path.GetFileNameWithoutExtension(disk.FilePath), out n)) map[n] = df;
            }
            return map;
        }

        private static void Save(Bitmap bitmap, string folder, string fileName)
        {
            bitmap.Save(Path.Combine(folder, fileName), ImageFormat.Png);
        }

        // ---------------------------------------------------------------------
        // Import
        // ---------------------------------------------------------------------

        private class ImageEdit { public int Offset; public int OldLen; public byte[] NewBytes; public int SizeWordOffset = -1; }

        // A non-indexed (RGB) PNG has no palette indexes to read, so GetIndexMatrix / a luminance mask
        // would silently produce a corrupt region. Reject it with the same message the per-node importer uses.
        private const string NotIndexedMessage = "the image must be an indexed (palette-based) PNG so the original colour indexes are preserved. Re-export it from ScummEditor and edit it without converting it to RGB.";

        /// <summary>
        /// Batch PNG import for a v2 game: room backgrounds (+ their walk-behind mask), object images and
        /// costume frames, re-encoded via ScummV2ImageEncoder / CostumeImageEncoderV4 and spliced with
        /// ScummV2Writer.ApplyEdit. Unlike v3old, a v2 background and its mask share ONE region, so an
        /// edited background and an edited z-plane for the same room are merged into a single edit.
        /// </summary>
        public static ScummV4GraphicsBatch.ImportReport Import(ScummGameData game, string folder, Action<int, int> onProgress)
        {
            var report = new ScummV4GraphicsBatch.ImportReport();
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            var rooms = RoomsByNumber(game);
            var roomNumbers = new List<int>(rooms.Keys);
            roomNumbers.Sort();

            string[] files = Directory.GetFiles(folder, "*.png");
            report.Found = files.Length;

            for (int idx = 0; idx < roomNumbers.Count; idx++)
            {
                if (onProgress != null) onProgress(idx + 1, roomNumbers.Count);
                int roomNo = roomNumbers[idx];
                ScummV3OldBundleDataFile df = rooms[roomNo];

                var edits = new List<ImageEdit>();
                CollectBackgroundEdit(df, folder, roomNo, edits, report);
                CollectObjectEdits(df, folder, roomNo, edits, report);
                CollectCostumeEdits(df, index, roomNo, folder, edits, report);

                ApplyEdits(df, index, roomNo, edits, report);
            }
            return report;
        }

        /// <summary>Collects the combined background image + walk-behind mask edit (they share one region).</summary>
        private static void CollectBackgroundEdit(ScummV3OldBundleDataFile df, string folder, int roomNo,
            List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            var room = new ScummV2Room(df.RawContent);
            if (room.Width <= 0 || room.Height <= 0 || room.ImageOffset <= 0) return;
            int w = room.Width, h = room.Height, imageOffset = room.ImageOffset;
            int imageEnd = room.NextStructuralOffsetAbove(imageOffset);

            string imgPath = Path.Combine(folder, string.Format("Room#{0}.png", roomNo));
            string maskPath = Path.Combine(folder, string.Format("Room#{0} ZPlane#0.png", roomNo));
            bool hasImg = File.Exists(imgPath), hasMask = File.Exists(maskPath);
            if (!hasImg && !hasMask) return;

            byte[,] origMatrix = ScummV2ImageDecoder.DecodeRle(df.RawContent, imageOffset, w, h);
            if (origMatrix == null) return;

            int gfxLen = ScummV2ImageDecoder.GraphicsRleLength(df.RawContent, imageOffset, w, h);
            int maskStart = imageOffset + gfxLen;
            bool roomHasMask = maskStart < imageEnd && maskStart < df.RawContent.Length;
            byte[,] origMask = roomHasMask ? ScummV2ImageDecoder.DecodeMaskRle(df.RawContent, maskStart, w, h) : null;

            try
            {
                byte[,] newMatrix = origMatrix;
                bool imgChanged = false;
                if (hasImg)
                {
                    using (var bmp = (Bitmap)Image.FromFile(imgPath))
                    {
                        if (bmp.Width != w || bmp.Height != h)
                            report.Errors.Add(string.Format("Room#{0}: the image must be {1}x{2}, but it is {3}x{4}.", roomNo, w, h, bmp.Width, bmp.Height));
                        else if (!IndexedImageHelper.IsIndexed(bmp))
                            report.Errors.Add(string.Format("Room#{0}: {1}", roomNo, NotIndexedMessage));
                        else
                        {
                            byte[,] m = IndexedImageHelper.GetIndexMatrix(bmp);
                            if (!MatrixEqual(m, origMatrix)) { newMatrix = m; imgChanged = true; }
                        }
                    }
                }

                byte[,] newMask = origMask;
                bool maskChanged = false;
                if (hasMask && roomHasMask)
                {
                    using (var bmp = (Bitmap)Image.FromFile(maskPath))
                    {
                        if (bmp.Width != w || bmp.Height != h)
                            report.Errors.Add(string.Format("Room#{0} ZPlane#0: the mask must be {1}x{2}, but it is {3}x{4}.", roomNo, w, h, bmp.Width, bmp.Height));
                        else
                        {
                            // A mask is 1-bit B/W (thresholded by luminance), so a B/W PNG of any format is fine
                            // - no indexed requirement here, unlike the image branch above.
                            byte[,] mm = MaskMatrixFromBitmap(bmp);
                            if (origMask == null || !MatrixEqual(mm, origMask)) { newMask = mm; maskChanged = true; }
                        }
                    }
                }
                else if (hasMask)
                {
                    report.Errors.Add(string.Format("Room#{0} ZPlane#0: this room has no walk-behind mask to import into.", roomNo));
                }

                if (!imgChanged && !maskChanged) return;

                byte[] newRegion;
                if (imgChanged && maskChanged)
                    newRegion = ScummV2ImageEncoder.EncodeImageAndMask(newMatrix, newMask, w, h);
                else if (maskChanged)
                    newRegion = ScummV2ImageEncoder.EncodeImageWithMask(df.RawContent, imageOffset, w, h, newMask);
                else
                    newRegion = ScummV2ImageEncoder.EncodeImage(df.RawContent, imageOffset, imageEnd, w, h, newMatrix);

                edits.Add(new ImageEdit { Offset = imageOffset, OldLen = imageEnd - imageOffset, NewBytes = newRegion, SizeWordOffset = -1 });
            }
            catch (Exception ex) { report.Errors.Add(string.Format("Room#{0}: {1}", roomNo, ex.Message)); }
        }

        /// <summary>Collects object-image edits (v2 objects carry no walk-behind mask).</summary>
        private static void CollectObjectEdits(ScummV3OldBundleDataFile df, string folder, int roomNo,
            List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            var room = new ScummV2Room(df.RawContent);
            for (int j = 0; j < room.NumObjects; j++)
            {
                string path = Path.Combine(folder, string.Format("Room#{0} Obj#{1} Img#0.png", roomNo, j));
                if (!File.Exists(path)) continue;
                if (!ScummV2ImageDecoder.ObjectOwnsImage(room, j)) continue; // imageless / non-primary multi-state: never splice
                int obim = room.ObjectImageOffset(j);
                int w = room.ObjectWidth(j), h = room.ObjectHeight(j);
                byte[,] orig = ScummV2ImageDecoder.DecodeRle(df.RawContent, obim, w, h);
                if (orig == null) continue;
                int objEnd = room.NextStructuralOffsetAbove(obim);

                try
                {
                    using (var bmp = (Bitmap)Image.FromFile(path))
                    {
                        if (bmp.Width != w || bmp.Height != h)
                        {
                            report.Errors.Add(string.Format("Room#{0} Obj#{1}: the image must be {2}x{3}, but it is {4}x{5}.", roomNo, j, w, h, bmp.Width, bmp.Height));
                            continue;
                        }
                        if (!IndexedImageHelper.IsIndexed(bmp))
                        {
                            report.Errors.Add(string.Format("Room#{0} Obj#{1}: {2}", roomNo, j, NotIndexedMessage));
                            continue;
                        }
                        byte[,] m = IndexedImageHelper.GetIndexMatrix(bmp);
                        if (MatrixEqual(m, orig)) continue; // unchanged
                        byte[] newRegion = ScummV2ImageEncoder.EncodeImage(df.RawContent, obim, objEnd, w, h, m);
                        edits.Add(new ImageEdit { Offset = obim, OldLen = objEnd - obim, NewBytes = newRegion, SizeWordOffset = -1 });
                    }
                }
                catch (Exception ex) { report.Errors.Add(string.Format("Room#{0} Obj#{1}: {2}", roomNo, j, ex.Message)); }
            }
        }

        /// <summary>Collects costume-frame edits (Costume#c FrameIndex#k.png) via the index COSTUME directory.</summary>
        private static void CollectCostumeEdits(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index,
            int roomNo, string folder, List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            if (index == null || index.CostumeDirectory == null) return;
            var encoder = new CostumeImageEncoderV4();
            var decoder = new CostumeImageDecoderV4();
            var ega = new Color[16];
            Array.Copy(EgaColorTable.Colors256, ega, 16);
            V3OldResourceDirectory dir = index.CostumeDirectory;

            for (int c = 0; c < dir.Count; c++)
            {
                if (dir.RoomNumbers[c] != roomNo) continue;
                int offset = dir.Offsets[c];
                if (offset == 0xFFFF || offset == 0) continue;

                CostumeV3Old costume;
                try { costume = new CostumeV3Old(df.RawContent, offset); }
                catch { continue; }
                if (costume.Frames.Count == 0) continue;

                var replacements = new Dictionary<int, byte[]>();
                for (int k = 0; k < costume.Frames.Count; k++)
                {
                    string path = Path.Combine(folder, string.Format("Costume#{0} FrameIndex#{1}.png", c, k));
                    if (!File.Exists(path)) continue;
                    try
                    {
                        using (var bmp = (Bitmap)Image.FromFile(path))
                        {
                            var frame = costume.Frames[k];
                            if (bmp.Width != frame.Width || bmp.Height != frame.Height)
                            {
                                report.Errors.Add(string.Format("Costume#{0} FrameIndex#{1}: must be {2}x{3}", c, k, frame.Width, frame.Height));
                                continue;
                            }
                            if (FrameUnchanged(decoder, frame, ega, bmp)) continue;
                            replacements[k] = encoder.Encode(bmp, 16); // 16-colour EGA
                        }
                    }
                    catch (Exception ex) { report.Errors.Add(string.Format("Costume#{0} FrameIndex#{1}: {2}", c, k, ex.Message)); }
                }
                if (replacements.Count == 0) continue;

                try
                {
                    byte[] rebuilt = costume.BuildWithReplacedFrames(replacements);
                    edits.Add(new ImageEdit { Offset = offset, OldLen = costume.ResourceSize, NewBytes = rebuilt, SizeWordOffset = offset });
                }
                catch (Exception ex) { report.Errors.Add(string.Format("Costume#{0}: {1}", c, ex.Message)); }
            }
        }

        /// <summary>Applies the collected edits high-offset first, reporting (not dropping) conflicting shared regions.</summary>
        private static void ApplyEdits(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo,
            List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            var appliedAt = new Dictionary<int, byte[]>();
            edits.Sort((a, b) => b.Offset.CompareTo(a.Offset));
            foreach (ImageEdit e in edits)
            {
                byte[] already;
                if (appliedAt.TryGetValue(e.Offset, out already))
                {
                    if (!BytesEqual(already, e.NewBytes))
                        report.Errors.Add(string.Format(
                            "Room#{0}: the image region at offset {1} is shared by several resources with conflicting edits; only the first was applied.", roomNo, e.Offset));
                    continue;
                }
                ScummV2Writer.ApplyEdit(df, index, roomNo, e.Offset, e.OldLen, e.NewBytes, e.SizeWordOffset);
                appliedAt[e.Offset] = e.NewBytes;
                report.Imported++;
            }
        }

        /// <summary>True when the imported bitmap's pixels equal what the original CEL decodes to.</summary>
        private static bool FrameUnchanged(CostumeImageDecoderV4 decoder, CostumeImageData frame, Color[] palette, Bitmap imported)
        {
            using (Bitmap original = decoder.Decode(frame, 16, palette, false))
            {
                if (original == null || original.Width != imported.Width || original.Height != imported.Height) return false;
                byte[,] a = IndexedImageHelper.GetIndexMatrix(original);
                byte[,] b = IndexedImageHelper.GetIndexMatrix(imported);
                return MatrixEqual(a, b);
            }
        }

        /// <summary>A v2 walk-behind mask PNG -> a 0/1 matrix (white = masked, the export convention).</summary>
        private static byte[,] MaskMatrixFromBitmap(Bitmap bmp)
        {
            var m = new byte[bmp.Width, bmp.Height];
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                    m[x, y] = (byte)(bmp.GetPixel(x, y).R > 127 ? 1 : 0);
            return m;
        }

        private static bool MatrixEqual(byte[,] a, byte[,] b)
        {
            if (a == null || b == null || a.GetLength(0) != b.GetLength(0) || a.GetLength(1) != b.GetLength(1)) return false;
            for (int x = 0; x < a.GetLength(0); x++)
                for (int y = 0; y < a.GetLength(1); y++)
                    if ((a[x, y] & 0x0F) != (b[x, y] & 0x0F)) return false;
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
