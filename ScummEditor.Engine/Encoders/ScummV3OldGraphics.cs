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
                        Save(background, folder, string.Format("Room#{0}.png", i));
                        count++;
                    }
                }

                if (options.Objects)
                {
                    for (int j = 0; j < room.NumObjects; j++)
                    {
                        Bitmap obj = decoder.DecodeObject(room, j);
                        if (obj != null)
                        {
                            Save(obj, folder, string.Format("Room#{0} Obj#{1} Img#0.png", i, j));
                            count++;
                        }
                    }
                }

                if (onProgress != null) onProgress(i + 1, rooms.Count);
            }

            return count;
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

                // Several objects can share one OBIM image; apply each byte region once.
                var appliedAt = new HashSet<int>();
                edits.Sort((a, b) => b.Offset.CompareTo(a.Offset));
                foreach (ImageEdit e in edits)
                {
                    if (!appliedAt.Add(e.Offset)) continue;
                    ScummV3OldWriter.ApplyEdit(dataFile, index, roomNo, e.Offset, e.OldLen, e.NewBytes);
                    report.Imported++;
                }
            }
            return report;
        }

        private class ImageEdit { public int Offset; public int OldLen; public byte[] NewBytes; }

        private static void CollectBackgroundEdit(ScummV3OldBundleDataFile dataFile, string folder, int roomIndex,
            List<ImageEdit> edits, ScummV4GraphicsBatch.ImportReport report)
        {
            string path = Path.Combine(folder, string.Format("Room#{0}.png", roomIndex));
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
                string path = Path.Combine(folder, string.Format("Room#{0} Obj#{1} Img#0.png", roomIndex, j));
                if (!File.Exists(path)) continue;
                int obim = room.ObjectImageOffset(j);
                int w = room.ObjectWidth(j), h = room.ObjectHeight(j);
                if (obim == 0 || w == 0 || h == 0) continue;
                int oldLen = ReadU16(dataFile.RawContent, obim);
                TryEncode(path, dataFile.RawContent, obim, w, h, oldLen, edits, report,
                    string.Format("Room#{0} Obj#{1}", roomIndex, j));
            }
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

        private static bool SliceEquals(byte[] buf, int offset, byte[] other)
        {
            if (offset + other.Length > buf.Length) return false;
            for (int i = 0; i < other.Length; i++) if (buf[offset + i] != other[i]) return false;
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
    }
}
