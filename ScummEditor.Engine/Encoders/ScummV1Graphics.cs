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
    /// Batch graphics export/import for SCUMM v1 games (Maniac Mansion 1987, Zak McKracken 1988 - classic
    /// DOS floppy). v1 is NOT the v2 GdiV2 vertical-RLE format: it is the C64-style GdiV1 tilemap
    /// (charMap/picMap/colorMap + per-cell mask), so it needs its own batch path with ScummV1ImageDecoder
    /// and the v1 costume codec (format 0x57). Export covers room backgrounds, object images and BOTH the
    /// background and per-object walk-behind (z-plane) masks; import re-uses the per-node OldBundleImageImporter
    /// / OldBundleCostumeImporter (which already detect and handle v1), only re-importing files that changed.
    /// Mirrors ScummV2Graphics / ScummV3OldGraphics.
    /// </summary>
    public static class ScummV1Graphics
    {
        public static int Export(ScummGameData game, string folder, ScummV4GraphicsBatch.ExportOptions options,
            Action<int, int> onProgress, Func<bool> shouldCancel = null)
        {
            bool isManiac = game.LoadedGameInfo != null && game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);
            var rooms = RoomsByNumber(game);
            var roomNumbers = new List<int>(rooms.Keys);
            roomNumbers.Sort();
            int count = 0;

            for (int idx = 0; idx < roomNumbers.Count; idx++)
            {
                if (shouldCancel != null && shouldCancel()) break;
                int roomNo = roomNumbers[idx];
                var room = new ScummV1Room(rooms[roomNo].RawContent);
                if (room.WidthInChars <= 0 || room.HeightInChars <= 0) { if (onProgress != null) onProgress(idx + 1, roomNumbers.Count); continue; }

                if (options.Backgrounds)
                    using (Bitmap bg = decoder.DecodeBackground(room))
                        if (bg != null) { Save(bg, folder, string.Format("Room#{0:D3}.png", roomNo)); count++; }
                if (options.BackgroundZPlanes)
                    using (Bitmap zp = decoder.DecodeBackgroundZPlane(room))
                        if (zp != null) { Save(zp, folder, string.Format("Room#{0:D3} ZP#000.png", roomNo)); count++; }
                if (options.Objects || options.ObjectZPlanes)
                {
                    for (int j = 0; j < room.NumObjects; j++)
                    {
                        if (options.Objects)
                            using (Bitmap obj = decoder.DecodeObject(room, j))
                                if (obj != null) { Save(obj, folder, string.Format("Room#{0:D3} Obj#{1:D3} Img#000.png", roomNo, j)); count++; }
                        if (options.ObjectZPlanes)
                            using (Bitmap objZ = decoder.DecodeObjectZPlane(room, j))
                                if (objZ != null) { Save(objZ, folder, string.Format("Room#{0:D3} Obj#{1:D3} Img#000 ZP#000.png", roomNo, j)); count++; }
                    }
                }
                if (onProgress != null) onProgress(idx + 1, roomNumbers.Count);
            }

            if (options.Costumes) count += ExportCostumes(game, folder, isManiac);
            return count;
        }

        /// <summary>Exports every v1 costume frame (format 0x57, a 4-colour C64 RLE) via the index COSTUME directory.</summary>
        private static int ExportCostumes(ScummGameData game, string folder, bool isManiac)
        {
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            if (index == null || index.CostumeDirectory == null) return 0;

            var byRoom = RoomsByNumber(game);
            var decoder = new CostumeImageDecoderV1();
            byte[] palette = CostumeImageDecoderV1.DefaultPalette(isManiac);

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
                    try { frame = decoder.Decode(costume.Frames[k], palette); }
                    catch { continue; }
                    if (frame != null)
                        using (frame) { Save(frame, folder, string.Format("Costume#{0:D3} FrameIndex#{1:D3}.png", c, k)); count++; }
                }
            }
            return count;
        }

        // ---------------------------------------------------------------------
        // Import (delegates to the per-node v1 importers; only changed files are re-imported)
        // ---------------------------------------------------------------------

        public static ScummV4GraphicsBatch.ImportReport Import(ScummGameData game, string folder, Action<int, int> onProgress)
        {
            var report = new ScummV4GraphicsBatch.ImportReport();
            var index = game.IndexFile as ScummV3OldBundleIndexFile;
            bool isManiac = game.LoadedGameInfo != null && game.LoadedGameInfo.LoadedGame == ScummGame.ManiacMansion;
            var decoder = new ScummV1ImageDecoder(isManiac);
            var rooms = RoomsByNumber(game);
            var roomNumbers = new List<int>(rooms.Keys);
            roomNumbers.Sort();

            report.Found = Directory.GetFiles(folder, "*.png").Length;

            for (int idx = 0; idx < roomNumbers.Count; idx++)
            {
                if (onProgress != null) onProgress(idx + 1, roomNumbers.Count);
                int roomNo = roomNumbers[idx];
                ScummV3OldBundleDataFile df = rooms[roomNo];

                TryImportImage(df, index, roomNo, decoder, folder, OldBundleImageKind.Background, 0,
                    string.Format("Room#{0:D3}.png", roomNo), report);
                TryImportImage(df, index, roomNo, decoder, folder, OldBundleImageKind.BackgroundZPlane, 0,
                    string.Format("Room#{0:D3} ZP#000.png", roomNo), report);

                int numObjects = new ScummV1Room(df.RawContent).NumObjects;
                for (int j = 0; j < numObjects; j++)
                {
                    TryImportImage(df, index, roomNo, decoder, folder, OldBundleImageKind.Object, j,
                        string.Format("Room#{0:D3} Obj#{1:D3} Img#000.png", roomNo, j), report);
                    TryImportImage(df, index, roomNo, decoder, folder, OldBundleImageKind.ObjectZPlane, j,
                        string.Format("Room#{0:D3} Obj#{1:D3} Img#000 ZP#000.png", roomNo, j), report);
                }

                ImportCostumes(df, index, roomNo, folder, isManiac, report);
            }
            return report;
        }

        /// <summary>Re-imports one image node if the PNG exists and its pixels differ from the current decode.</summary>
        private static void TryImportImage(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo,
            ScummV1ImageDecoder decoder, string folder, OldBundleImageKind kind, int objectIndex, string fileName,
            ScummV4GraphicsBatch.ImportReport report)
        {
            string path = BatchImageNaming.ResolveForImport(folder, fileName);
            if (!File.Exists(path)) return;

            try
            {
                using (Bitmap current = Decode(decoder, new ScummV1Room(df.RawContent), kind, objectIndex))
                using (var png = (Bitmap)Image.FromFile(path))
                {
                    if (current == null) { report.Errors.Add(fileName + ": there is no matching image in the room to import into."); return; }
                    if (BitmapsEqual(current, png)) return; // unchanged - skip (avoids needless room growth)

                    string error;
                    if (OldBundleImageImporter.Import(df, index, roomNo, true, kind, objectIndex, png, out error))
                        report.Imported++;
                    else
                        report.Errors.Add(fileName + ": " + error);
                }
            }
            catch (Exception ex) { report.Errors.Add(fileName + ": " + ex.Message); }
        }

        private static void ImportCostumes(ScummV3OldBundleDataFile df, ScummV3OldBundleIndexFile index, int roomNo,
            string folder, bool isManiac, ScummV4GraphicsBatch.ImportReport report)
        {
            if (index == null || index.CostumeDirectory == null) return;
            var decoder = new CostumeImageDecoderV1();
            byte[] palette = CostumeImageDecoderV1.DefaultPalette(isManiac);
            V3OldResourceDirectory dir = index.CostumeDirectory;

            for (int c = 0; c < dir.Count; c++)
            {
                if (dir.RoomNumbers[c] != roomNo) continue;
                int offset = dir.Offsets[c]; // re-read each iteration: a prior import relocates the index in place
                if (offset == 0xFFFF || offset == 0) continue;

                CostumeV3Old costume;
                try { costume = new CostumeV3Old(df.RawContent, offset); }
                catch { continue; }

                for (int k = 0; k < costume.Frames.Count; k++)
                {
                    string path = BatchImageNaming.ResolveForImport(folder, string.Format("Costume#{0:D3} FrameIndex#{1:D3}.png", c, k));
                    if (!File.Exists(path)) continue;
                    try
                    {
                        // Re-parse against the current bytes (an earlier frame import grew this costume).
                        var live = new CostumeV3Old(df.RawContent, dir.Offsets[c]);
                        using (Bitmap current = decoder.Decode(live.Frames[k], palette))
                        using (var png = (Bitmap)Image.FromFile(path))
                        {
                            if (current != null && BitmapsEqual(current, png)) continue; // unchanged
                            string error;
                            if (OldBundleCostumeImporter.ImportFrame(df, index, roomNo, true, dir.Offsets[c], k, png, out error))
                                report.Imported++;
                            else
                                report.Errors.Add(string.Format("Costume#{0} FrameIndex#{1}: {2}", c, k, error));
                        }
                    }
                    catch (Exception ex) { report.Errors.Add(string.Format("Costume#{0} FrameIndex#{1}: {2}", c, k, ex.Message)); }
                }
            }
        }

        private static Bitmap Decode(ScummV1ImageDecoder decoder, ScummV1Room room, OldBundleImageKind kind, int objectIndex)
        {
            switch (kind)
            {
                case OldBundleImageKind.Background: return decoder.DecodeBackground(room);
                case OldBundleImageKind.BackgroundZPlane: return decoder.DecodeBackgroundZPlane(room);
                case OldBundleImageKind.Object: return decoder.DecodeObject(room, objectIndex);
                case OldBundleImageKind.ObjectZPlane: return decoder.DecodeObjectZPlane(room, objectIndex);
                default: return null;
            }
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

        private static bool BitmapsEqual(Bitmap a, Bitmap b)
        {
            if (a == null || b == null || a.Width != b.Width || a.Height != b.Height) return false;
            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                    if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb()) return false;
            return true;
        }
    }
}
