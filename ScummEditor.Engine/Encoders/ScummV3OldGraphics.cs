using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScummEditor.Engine.Structures;
using ScummEditor.Engine.Structures.DataFile;

namespace ScummEditor.Engine.Encoders
{
    /// <summary>
    /// Batch PNG export of the EGA images of a SCUMM v3 "old bundle" game (Loom EGA, Indy3 EGA). These
    /// games are not the v4/v3small block-tree the ScummV4GraphicsBatch walks - each NN.LFL is a raw
    /// room whose background/object strip tables are reached by file-relative offsets - so they get this
    /// dedicated path. Decoding reuses the validated ScummV3OldImageDecoder; filenames match the v4/v5
    /// scheme ("Room#i", "Room#i Obj#j Img#0") so export/import map 1:1 if import is added later.
    /// </summary>
    public static class ScummV3OldGraphics
    {
        /// <summary>The v3 old-bundle room files in a stable, game-wide order (the DataDisk order).</summary>
        public static List<ScummV3OldBundleDataFile> EnumerateRooms(ScummGameData game)
        {
            var rooms = new List<ScummV3OldBundleDataFile>();
            if (game.DataDisks == null) return rooms;
            foreach (DataDisk disk in game.DataDisks)
            {
                var df = disk.Tree as ScummV3OldBundleDataFile;
                if (df != null) rooms.Add(df);
            }
            return rooms;
        }

        public static int Export(ScummGameData game, string folder, ScummV4GraphicsBatch.ExportOptions options,
            Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            var decoder = new ScummV3OldImageDecoder();
            List<ScummV3OldBundleDataFile> rooms = EnumerateRooms(game);
            int count = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (shouldCancel != null && shouldCancel()) break;

                var room = new ScummV3OldRoom(rooms[i].RawContent);

                if (options.Backgrounds)
                {
                    Bitmap background = decoder.DecodeBackground(room);
                    if (background != null)
                    {
                        Save(background, folder, string.Format("Room#{0:D3}.png", i));
                        count++;
                    }
                }

                if (options.BackgroundZPlanes && decoder.CountBackgroundZPlanes(room) > 0)
                {
                    Bitmap zplane = decoder.DecodeBackgroundZPlane(room);
                    if (zplane != null)
                    {
                        Save(zplane, folder, string.Format("Room#{0:D3} ZP#000.png", i));
                        count++;
                    }
                }

                if (options.Objects || options.ObjectZPlanes)
                {
                    for (int j = 0; j < room.NumObjects; j++)
                    {
                        if (options.Objects)
                        {
                            Bitmap obj = decoder.DecodeObject(room, j);
                            if (obj != null)
                            {
                                Save(obj, folder, string.Format("Room#{0:D3} Obj#{1:D3} Img#000.png", i, j));
                                count++;
                            }
                        }

                        if (options.ObjectZPlanes && decoder.CountObjectZPlanes(room, j) > 0)
                        {
                            Bitmap objZ = decoder.DecodeObjectZPlane(room, j);
                            if (objZ != null)
                            {
                                Save(objZ, folder, string.Format("Room#{0:D3} Obj#{1:D3} Img#000 ZP#000.png", i, j));
                                count++;
                            }
                        }
                    }
                }

                if (onProgress != null) onProgress(i + 1, rooms.Count);
            }

            if (options.Costumes)
            {
                count += ExportCostumes(game, folder);
            }

            return count;
        }

        /// <summary>Exports every costume frame, located via the index COSTUME directory (room, offset).</summary>
        private static int ExportCostumes(ScummGameData game, string folder)
        {
            var index = game.IndexFile as Structures.IndexFile.ScummV3OldBundleIndexFile;
            if (index == null || index.CostumeDirectory == null) return 0;

            var byRoom = RoomsByNumber(game);
            var costumeDecoder = new CostumeImageDecoderV4();
            Color[] egaPalette = new Color[16];
            Array.Copy(EgaColorTable.Colors256, egaPalette, 16);

            int count = 0;
            Structures.IndexFile.V3OldResourceDirectory dir = index.CostumeDirectory;
            for (int c = 0; c < dir.Count; c++)
            {
                int offset = dir.Offsets[c];
                if (offset == 0xFFFF || offset == 0) continue;
                ScummV3OldBundleDataFile df;
                if (!byRoom.TryGetValue(dir.RoomNumbers[c], out df)) continue;

                var costume = new CostumeV3Old(df.RawContent, offset);
                for (int k = 0; k < costume.Frames.Count; k++)
                {
                    Bitmap frame = costumeDecoder.Decode(costume.Frames[k], 16, egaPalette, false);
                    if (frame != null)
                    {
                        Save(frame, folder, string.Format("Costume#{0:D3} FrameIndex#{1:D3}.png", c, k));
                        count++;
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
                if (df != null) map[RoomNumberFromPath(disk.FilePath)] = df;
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

        public static ScummV4GraphicsBatch.ImportReport Import(ScummGameData game, string folder, Action<int, int> onProgress)
        {
            var report = new ScummV4GraphicsBatch.ImportReport();
            var index = game.IndexFile as Structures.IndexFile.ScummV3OldBundleIndexFile;
            List<ScummV3OldBundleDataFile> rooms = EnumerateRooms(game);

            string[] files = Directory.GetFiles(folder, "*.png");
            report.Found = files.Length;

            for (int i = 0; i < rooms.Count; i++)
            {
                if (onProgress != null) onProgress(i + 1, rooms.Count);
                ScummV3OldBundleDataFile dataFile = rooms[i];
                int roomNo = RoomNumberFromPath(game.DataDisks[IndexOfDisk(game, dataFile)].FilePath);

                // Collect every image edit for this room FROM THE ORIGINAL bytes, then apply them in
                // descending offset order (a splice never invalidates a not-yet-applied lower edit).
                var edits = new List<ImageEdit>();
                CollectBackgroundEdit(dataFile, folder, i, edits, report);
                CollectObjectEdits(dataFile, folder, i, edits, report);
                CollectBackgroundZPlaneEdit(dataFile, folder, i, edits, report);
                CollectObjectZPlaneEdits(dataFile, folder, i, edits, report);
                CollectCostumeEdits(dataFile, index, roomNo, folder, edits, report);

                // Several objects/costumes can share one byte region (multi-state objects point at one
                // OBIM, hence one image + one z-plane). Applying that region once is correct ONLY when the
                // edits agree; two DIFFERENT edits cannot both be stored in the same bytes, so report the
                // conflict instead of silently discarding one (which would look like a success). Apply in
                // descending offset order so a splice never invalidates a not-yet-applied lower edit.
                var appliedAt = new Dictionary<int, byte[]>();
                edits.Sort((a, b) => b.Offset.CompareTo(a.Offset));
                foreach (ImageEdit e in edits)
                {
                    byte[] already;
                    if (appliedAt.TryGetValue(e.Offset, out already))
                    {
                        if (!BytesEqual(already, e.NewBytes))
                        {
                            report.Errors.Add(string.Format(
                                "Room#{0}: the image/z-plane region at offset {1} is shared by several objects with conflicting edits; only the first was applied. Paint shared objects identically or edit only one.",
                                i, e.Offset));
                        }
                        continue; // identical (consistent) shared edit: already applied
                    }
                    ScummV3OldWriter.ApplyEdit(dataFile, index, roomNo, e.Offset, e.OldLen, e.NewBytes, e.SizeWordOffset);
                    appliedAt[e.Offset] = e.NewBytes;
                    report.Imported++;
                }
            }
            return report;
        }

        private class ImageEdit { public int Offset; public int OldLen; public byte[] NewBytes; public int SizeWordOffset = -1; }

        /// <summary>Collects costume-frame edits for this room (Costume#c FrameIndex#k.png) via the index COSTUME dir.</summary>
        private static void CollectCostumeEdits(ScummV3OldBundleDataFile dataFile, Structures.IndexFile.ScummV3OldBundleIndexFile index,
            int roomNo, string folder, List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            if (index == null || index.CostumeDirectory == null) return;
            var encoder = new CostumeImageEncoderV4();
            var decoder = new CostumeImageDecoderV4();
            Color[] egaPalette = new Color[16];
            Array.Copy(EgaColorTable.Colors256, egaPalette, 16);
            Structures.IndexFile.V3OldResourceDirectory dir = index.CostumeDirectory;

            for (int c = 0; c < dir.Count; c++)
            {
                if (dir.RoomNumbers[c] != roomNo) continue;
                int offset = dir.Offsets[c];
                if (offset == 0xFFFF || offset == 0) continue;

                var costume = new CostumeV3Old(dataFile.RawContent, offset);
                if (costume.Frames.Count == 0) continue;

                var replacements = new Dictionary<int, byte[]>();
                for (int k = 0; k < costume.Frames.Count; k++)
                {
                    string path = BatchImageNaming.ResolveForImport(folder, string.Format("Costume#{0:D3} FrameIndex#{1:D3}.png", c, k));
                    if (!File.Exists(path)) continue;
                    try
                    {
                        using (var bitmap = (Bitmap)Image.FromFile(path))
                        {
                            CostumeImageData frame = costume.Frames[k];
                            if (bitmap.Width != frame.Width || bitmap.Height != frame.Height)
                            {
                                report.Errors.Add(string.Format("Costume#{0} FrameIndex#{1}: must be {2}x{3}", c, k, frame.Width, frame.Height));
                                continue;
                            }
                            // Skip a frame whose pixels are unchanged (the RLE re-encodes differently
                            // than the original, so a byte compare would needlessly rewrite every
                            // costume on a text-only translation); compare the decoded pixels instead.
                            if (FrameUnchanged(decoder, frame, egaPalette, bitmap)) continue;
                            replacements[k] = encoder.Encode(bitmap, 16); // 16-colour EGA
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
                catch (Exceptions.ImageEncodeException ex) { report.Errors.Add(string.Format("Costume#{0}: {1}", c, ex.Message)); }
            }
        }

        private static void CollectBackgroundEdit(ScummV3OldBundleDataFile dataFile, string folder, int roomIndex,
            List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            string path = BatchImageNaming.ResolveForImport(folder, string.Format("Room#{0:D3}.png", roomIndex));
            if (!File.Exists(path)) return;

            var room = new ScummV3OldRoom(dataFile.RawContent);
            if (room.Width == 0 || room.Height == 0 || room.ImageOffset == 0) return;
            int oldLen = ReadU16(dataFile.RawContent, room.ImageOffset);
            TryEncode(path, dataFile.RawContent, room.ImageOffset, room.Width, room.Height, oldLen, edits, report,
                string.Format("Room#{0}", roomIndex));
        }

        private static void CollectObjectEdits(ScummV3OldBundleDataFile dataFile, string folder, int roomIndex,
            List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            var room = new ScummV3OldRoom(dataFile.RawContent);
            for (int j = 0; j < room.NumObjects; j++)
            {
                string path = BatchImageNaming.ResolveForImport(folder, string.Format("Room#{0:D3} Obj#{1:D3} Img#000.png", roomIndex, j));
                if (!File.Exists(path)) continue;
                int obim = room.ObjectImageOffset(j);
                int w = room.ObjectWidth(j), h = room.ObjectHeight(j);
                if (obim == 0 || w == 0 || h == 0) continue;
                int oldLen = ReadU16(dataFile.RawContent, obim);
                TryEncode(path, dataFile.RawContent, obim, w, h, oldLen, edits, report,
                    string.Format("Room#{0} Obj#{1}", roomIndex, j));
            }
        }

        private static void CollectBackgroundZPlaneEdit(ScummV3OldBundleDataFile dataFile, string folder, int roomIndex,
            List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            string path = BatchImageNaming.ResolveForImport(folder, string.Format("Room#{0:D3} ZP#000.png", roomIndex));
            if (!File.Exists(path)) return;

            var room = new ScummV3OldRoom(dataFile.RawContent);
            var decoder = new ScummV3OldImageDecoder();
            if (decoder.CountBackgroundZPlanes(room) == 0)
            {
                report.Errors.Add(string.Format("Room#{0} ZP#0: the room has no z-plane region to import into", roomIndex));
                return;
            }
            using (Bitmap original = decoder.DecodeBackgroundZPlane(room))
            {
                TryEncodeZPlane(path, dataFile.RawContent, room, room.ImageOffset, room.Width, room.Height,
                    original, edits, report, string.Format("Room#{0} ZP#0", roomIndex));
            }
        }

        private static void CollectObjectZPlaneEdits(ScummV3OldBundleDataFile dataFile, string folder, int roomIndex,
            List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            var room = new ScummV3OldRoom(dataFile.RawContent);
            var decoder = new ScummV3OldImageDecoder();
            for (int j = 0; j < room.NumObjects; j++)
            {
                string path = BatchImageNaming.ResolveForImport(folder, string.Format("Room#{0:D3} Obj#{1:D3} Img#000 ZP#000.png", roomIndex, j));
                if (!File.Exists(path)) continue;
                if (decoder.CountObjectZPlanes(room, j) == 0)
                {
                    report.Errors.Add(string.Format("Room#{0} Obj#{1} ZP#0: the object has no z-plane region to import into", roomIndex, j));
                    continue;
                }
                int obim = room.ObjectImageOffset(j);
                int w = room.ObjectWidth(j), h = room.ObjectHeight(j);
                using (Bitmap original = decoder.DecodeObjectZPlane(room, j))
                {
                    TryEncodeZPlane(path, dataFile.RawContent, room, obim, w, h,
                        original, edits, report, string.Format("Room#{0} Obj#{1} ZP#0", roomIndex, j));
                }
            }
        }

        /// <summary>
        /// Encodes an edited z-plane mask and queues a splice over the original z-plane region
        /// [zbase, regionEnd), where zbase = imageOffset + smapLen. The region has no length word of its
        /// own, so SizeWordOffset stays -1 (the room size word and downstream offsets are fixed by
        /// ScummV3OldWriter.ApplyEdit). A mask whose pixels match the original is skipped so a no-op
        /// re-import keeps the file byte-identical.
        /// </summary>
        private static void TryEncodeZPlane(string path, byte[] roomData, ScummV3OldRoom room, int imageOffset,
            int width, int height, Bitmap original, List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report, string label)
        {
            try
            {
                using (var bitmap = (Bitmap)Image.FromFile(path))
                {
                    if (bitmap.Width != width || bitmap.Height != height)
                    {
                        report.Errors.Add(string.Format("{0}: the z-plane must be {1}x{2} (the original size), but it is {3}x{4}.",
                            label, width, height, bitmap.Width, bitmap.Height));
                        return;
                    }
                    if (original != null && MaskUnchanged(original, bitmap)) return;

                    int smapLen = ReadU16(roomData, imageOffset);
                    int zbase = imageOffset + smapLen;
                    int regionEnd = room.NextStructuralOffsetAbove(imageOffset);
                    int oldLen = regionEnd - zbase;
                    if (oldLen <= 0)
                    {
                        report.Errors.Add(label + ": no z-plane region to import into");
                        return;
                    }
                    byte[] newRegion = ScummV3OldZPlaneEncoder.Encode(width, height, bitmap);
                    edits.Add(new ImageEdit { Offset = zbase, OldLen = oldLen, NewBytes = newRegion, SizeWordOffset = -1 });
                }
            }
            catch (Exception ex) { report.Errors.Add(label + ": " + ex.Message); }
        }

        /// <summary>True when the two masks have the same masked (opaque-black) pixels, so re-encoding is a no-op.</summary>
        private static bool MaskUnchanged(Bitmap original, Bitmap imported)
        {
            if (original == null || original.Width != imported.Width || original.Height != imported.Height) return false;
            for (int y = 0; y < original.Height; y++)
                for (int x = 0; x < original.Width; x++)
                    if (ScummV4ImageEncoder.IsMasked(original.GetPixel(x, y)) != ScummV4ImageEncoder.IsMasked(imported.GetPixel(x, y)))
                        return false;
            return true;
        }

        private static void TryEncode(string path, byte[] roomData, int imageOffset, int width, int height,
            int oldLen, List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report, string label)
        {
            try
            {
                using (var bitmap = (Bitmap)Image.FromFile(path))
                {
                    byte[] newTable = ScummV3OldImageEncoder.Encode(roomData, imageOffset, width, height, bitmap);
                    if (newTable == null) return;
                    // Unchanged image: skip (keeps the file byte-identical).
                    if (newTable.Length == oldLen && SliceEquals(roomData, imageOffset, newTable)) return;
                    edits.Add(new ImageEdit { Offset = imageOffset, OldLen = oldLen, NewBytes = newTable });
                }
            }
            catch (Exceptions.ImageEncodeException ex) { report.Errors.Add(label + ": " + ex.Message); }
            catch (Exception ex) { report.Errors.Add(label + ": " + ex.Message); }
        }

        /// <summary>True when the imported bitmap's pixels equal what the original CEL decodes to.</summary>
        private static bool FrameUnchanged(CostumeImageDecoderV4 decoder, CostumeImageData frame, Color[] palette, Bitmap imported)
        {
            using (Bitmap original = decoder.Decode(frame, 16, palette, false))
            {
                if (original == null || original.Width != imported.Width || original.Height != imported.Height) return false;
                byte[,] a = IndexedImageHelper.GetIndexMatrix(original);
                byte[,] b = IndexedImageHelper.GetIndexMatrix(imported);
                for (int x = 0; x < a.GetLength(0); x++)
                    for (int y = 0; y < a.GetLength(1); y++)
                        if ((a[x, y] & 0x0F) != (b[x, y] & 0x0F)) return false;
                return true;
            }
        }

        private static bool SliceEquals(byte[] buf, int offset, byte[] other)
        {
            if (offset + other.Length > buf.Length) return false;
            for (int i = 0; i < other.Length; i++) if (buf[offset + i] != other[i]) return false;
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static int IndexOfDisk(ScummGameData game, ScummV3OldBundleDataFile dataFile)
        {
            for (int i = 0; i < game.DataDisks.Count; i++)
                if (ReferenceEquals(game.DataDisks[i].Tree, dataFile)) return i;
            return 0;
        }

        private static int RoomNumberFromPath(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int n;
            return int.TryParse(name, out n) ? n : 0;
        }

        private static int ReadU16(byte[] data, int p) { return data[p] | (data[p + 1] << 8); }

        // ---------------------------------------------------------------------
        // Sound import (raw AdLib payload replacement)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Replaces the raw AdLib payload of a v3 old-bundle sound (the bytes after the AD chunk's
        /// 4-byte header) with <paramref name="newPayload"/>, re-pointing the offsets the size change
        /// shifts via ScummV3OldWriter.ApplyEdit. This is an asset-level swap of the OPL2 stream
        /// (translation has no text in audio, and there is no MIDI-to-AdLib encoder); the payload must
        /// be a valid AdLib resource (e.g. one exported by the sound viewer or produced for ScummVM).
        /// Returns false (with a reason) when the sound has no AD chunk.
        /// </summary>
        public static bool ImportRawAdLib(ScummV3OldBundleDataFile dataFile, Structures.IndexFile.ScummV3OldBundleIndexFile index,
            int roomNo, int soundOffset, byte[] newPayload, out string error)
        {
            error = null;
            var sound = new ScummV3OldSound(dataFile.RawContent, soundOffset);
            if (sound.AdLibOffset < 0)
            {
                error = "this sound has no AdLib chunk to replace";
                return false;
            }
            int adOffs = sound.AdLibOffset;
            int oldPayloadLen = sound.AdLibSize - 4; // AD chunk = [size:u16][2 bytes][payload]
            // Replace [adOffs+4, adOffs+adSize) and grow the AD size word at adOffs by the delta.
            ScummV3OldWriter.ApplyEdit(dataFile, index, roomNo, adOffs + 4, oldPayloadLen, newPayload, adOffs);
            return true;
        }
    }
}
